using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace SEGA_Cutover
{
    public class ModernWebServer
    {
        private TcpListener? _listener;
        private CancellationTokenSource? _cts;

        private static IPAddress ResolveListenAddress(string? listen)
        {
            if (string.IsNullOrWhiteSpace(listen) || listen == "0.0.0.0" || listen == "+" || listen == "*")
            {
                return IPAddress.Any;
            }

            if (string.Equals(listen, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                return IPAddress.Loopback;
            }

            if (IPAddress.TryParse(listen, out var ip))
            {
                return ip;
            }

            return IPAddress.Any;
        }

        public async Task StartAsync()
        {
            var config = Config.GetConfig();
            var ip = ResolveListenAddress(config.Listen);

            _listener = new TcpListener(ip, config.Port);
            _listener.Start();

            _cts = new CancellationTokenSource();

            Console.WriteLine($"Server on {ip}:{config.Port}");

            try
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    var client = await _listener.AcceptTcpClientAsync();
                    _ = Task.Run(() => HandleClient(client));
                }
            }
            catch (ObjectDisposedException) { }
            catch (SocketException) { }
            catch (Exception ex)
            {
                if (_cts != null && !_cts.IsCancellationRequested)
                {
                    Console.WriteLine($"Server error: {ex.Message}");
                }
            }
        }

        private bool CheckPw(string? pw)
        {
            var config = Config.GetConfig();

            if (string.IsNullOrEmpty(config.Password))
                return true;

            if (string.IsNullOrEmpty(pw))
                return false;

            byte[] a = Encoding.UTF8.GetBytes(pw);
            byte[] b = Encoding.UTF8.GetBytes(config.Password);

            if (a.Length != b.Length)
                return false;

            int diff = 0;

            for (int i = 0; i < a.Length; i++)
            {
                diff |= a[i] ^ b[i];
            }

            return diff == 0;
        }

        private static Dictionary<string, string> ParseQueryString(string query)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(query))
                return dict;

            var pairs = query.Split('&');
            foreach (var pair in pairs)
            {
                if (string.IsNullOrWhiteSpace(pair))
                    continue;

                var kv = pair.Split(new[] { '=' }, 2);
                string key = Uri.UnescapeDataString(kv[0]);
                string value = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : "";
                dict[key] = value;
            }

            return dict;
        }

        private static string GetStatusText(int statusCode) => statusCode switch
        {
            200 => "OK",
            400 => "Bad Request",
            401 => "Unauthorized",
            404 => "Not Found",
            500 => "Internal Server Error",
            _ => "OK"
        };

        private async Task HandleClient(TcpClient client)
        {
            using (client)
            using (var stream = client.GetStream())
            {
                try
                {
                    stream.ReadTimeout = 5000;
                    stream.WriteTimeout = 5000;

                    using var reader = new StreamReader(stream, Encoding.UTF8, false, 4096, leaveOpen: true);
                    string? requestLine = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(requestLine))
                        return;

                    // Consume headers
                    string? headerLine;
                    while (!string.IsNullOrEmpty(headerLine = await reader.ReadLineAsync()))
                    {
                    }

                    string[] reqParts = requestLine.Split(' ');
                    if (reqParts.Length < 2)
                        return;

                    string httpMethod = reqParts[0].ToUpperInvariant();
                    string rawUrl = reqParts[1];

                    string path = rawUrl;
                    string queryString = "";
                    int qIndex = rawUrl.IndexOf('?');
                    if (qIndex >= 0)
                    {
                        path = rawUrl.Substring(0, qIndex);
                        queryString = rawUrl.Substring(qIndex + 1);
                    }

                    var query = ParseQueryString(queryString);
                    var config = Config.GetConfig();
                    var appList = config.GetAppList();

                    int statusCode = 200;
                    object result;

                    if (httpMethod == "GET" && path == "/")
                    {
                        result = "SEGA-Cutover Server is Running!";
                    }
                    else if (httpMethod == "GET" && path == "/api/info")
                    {
                        result = new
                        {
                            App = "SEGA-Cutover",
                            RunApp = config.TargetGameId,
                            AppList = appList,
                            OS = Environment.OSVersion.ToString(),
                            Time = DateTime.Now
                        };
                    }
                    else if (httpMethod == "GET" && path == "/api/list")
                    {
                        result = new
                        {
                            RunApp = config.TargetGameId,
                            AppList = appList
                        };
                    }
                    else if (httpMethod == "GET" && path == "/api/switch")
                    {
                        string? gameId = query.TryGetValue("gameId", out var gid) ? gid.ToUpper() : null;
                        string? password = query.TryGetValue("password", out var pwd) ? pwd : null;

                        if (!CheckPw(password))
                        {
                            statusCode = 401;
                            result = new
                            {
                                error = "Invalid password"
                            };
                        }
                        else if (!string.IsNullOrEmpty(gameId) && appList.Contains(gameId!))
                        {
                            config.TargetGameId = gameId;
                            Config.SaveConfig();
                            Win32API.RebootWindows();
                            result = new
                            {
                                message = $"Switched to {gameId}"
                            };
                        }
                        else
                        {
                            statusCode = 400;
                            result = new
                            {
                                error = $"Invalid gameId: {gameId}"
                            };
                        }
                    }
                    else if (httpMethod == "GET" && path == "/api/ui")
                    {
                        string? password = query.TryGetValue("password", out var pwd) ? pwd : null;

                        if (!CheckPw(password))
                        {
                            statusCode = 401;
                            result = new
                            {
                                error = "Invalid password"
                            };
                        }
                        else
                        {
                            try
                            {
                                await Application.Current.Dispatcher.InvokeAsync(() =>
                                {
                                    ((App)Application.Current).ShowMainWindowAsync();
                                });

                                result = new
                                {
                                    message = "UI shown"
                                };
                            }
                            catch (Exception ex)
                            {
                                statusCode = 500;
                                result = new
                                {
                                    error = ex.ToString()
                                };
                            }
                        }
                    }
                    else if (httpMethod == "GET" && path == "/api/next")
                    {
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            if (Application.Current.MainWindow is MainWindow mainWindow)
                            {
                                mainWindow.NextSelect();
                            }
                        });

                        result = new
                        {
                            message = "UI next"
                        };
                    }
                    else
                    {
                        statusCode = 404;
                        result = new
                        {
                            error = "Not Found"
                        };
                    }

                    string json;
                    if (result is string str)
                    {
                        json = str;
                    }
                    else
                    {
                        json = JsonSerializer.Serialize(result);
                    }

                    byte[] bodyBytes = Encoding.UTF8.GetBytes(json);
                    string header =
                        $"HTTP/1.1 {statusCode} {GetStatusText(statusCode)}\r\n" +
                        $"Content-Type: application/json; charset=utf-8\r\n" +
                        $"Content-Length: {bodyBytes.Length}\r\n" +
                        $"Connection: close\r\n" +
                        $"Access-Control-Allow-Origin: *\r\n" +
                        $"\r\n";

                    byte[] headerBytes = Encoding.ASCII.GetBytes(header);
                    await stream.WriteAsync(headerBytes, 0, headerBytes.Length);
                    await stream.WriteAsync(bodyBytes, 0, bodyBytes.Length);
                    await stream.FlushAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
        }

        public Task StopAsync()
        {
            _cts?.Cancel();
            try
            {
                _listener?.Stop();
            }
            catch { }

            return Task.CompletedTask;
        }
    }

    public class ControlRequest
    {
        public string? Command { get; set; }

        public int Value { get; set; }
    }
}