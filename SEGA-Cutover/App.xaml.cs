using System;
using System.Threading.Tasks;
using System.Windows;

namespace SEGA_Cutover;

public partial class App : Application
{
    private MainWindow? _window;

    protected override async void OnStartup(StartupEventArgs e)
    {
        Win32API.AttachToParentConsole();
        base.OnStartup(e);
        ModernWebServer _server = new ModernWebServer();
        // 异步启动服务器，不会卡住窗口显示
        _ = Task.Run(async () =>
        {
            try
            {
                await _server.StartAsync();
            }
            catch (Exception ex)
            {
                // 处理端口占用等异常
                Console.WriteLine($"Server failed: {ex.Message}");
            }
        });
        if (!Config.LoadConfig())
        {
            Console.WriteLine("配置文件加载失败，请检查 config.json 是否存在或格式是否正确。");
            try
            {
                Master.startSEGABoot();
            }
            catch (Exception exception)
            {
                Console.WriteLine("启动原逻辑失败");
                Console.WriteLine(exception.Message);
            }
        }
        else
        {
            try {
                Master.initSystem();
            }
            catch (Exception exception) {
                Console.WriteLine(exception);
            }

        }
    }

    public Task ShowMainWindowAsync()
    {
        return Dispatcher.InvokeAsync(() =>
        {
            if (_window is null)
            {
                _window = new MainWindow();
                MainWindow = _window;
                _window.Closed += (_, _) => _window = null;
            }

            _window.Show();

            if (_window.WindowState == WindowState.Minimized)
                _window.WindowState = WindowState.Maximized;

            _window.Activate();
        }).Task;
    }
}