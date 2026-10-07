using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace SEGA_Cutover;

/// <summary>
/// Config 类用于定义和管理 SEGA_Cutover 应用程序的配置。
/// </summary>
public class Config
{
    /// <summary>
    /// 表示配置中目标游戏的唯一标识符。
    /// 此属性用于指示当前选定的目标游戏，并与配置文件中的目标游戏配置相关联。
    /// </summary>
    public string TargetGameId { get; set; } = "";

    /// <summary>
    /// 表示目标磁盘路径的属性。
    /// 该路径用于指向虚拟磁盘文件或物理磁盘位置，以便在系统初始化时对其进行操作，例如挂载或路径校准。
    /// </summary>
    public string TargetDiskPath { get; set; } = "";

    /// <summary>
    /// 指示目标磁盘路径是否为虚拟硬盘 (VHD) 类型。
    /// 当此属性为 true 时，系统将按照虚拟硬盘的方式处理目标磁盘路径；
    /// 当此属性为 false 时，系统将按照物理磁盘路径处理。
    /// </summary>
    public bool IsVHD { get; set; } = true;

    /// <summary>
    /// 指示是否绕过硬件检测(GetHw)的布尔属性。
    /// </summary>
    /// <remarks>
    /// 当此属性设置为 <c>true</c> 时，系统在检测到包含 "hw" 的路径时，会直接更新注册表中的下一步进程路径 (NextProcess)，
    /// 而不执行硬件检测流程。此功能主要应用于某些特殊场景中，需要跳过硬件检测步骤的情况。
    /// </remarks>
    public bool IsPassSEGAPxGetHw { get; set; }

    /// <summary>
    /// 表示目标游戏程序的运行路径。
    /// </summary>
    /// <remarks>
    /// 此属性用于存储目标游戏程序的执行路径，通常在系统初始化或配置更新时被使用。
    /// 如果启用了虚拟磁盘（IsVHD=true），系统会根据挂载的新磁盘路径动态调整此属性的值。
    /// </remarks>
    public string TargetGameRunPath { get; set; } = "";

    /// <summary>
    /// 表示目标配置文件的路径。
    /// 此属性用来指定与目标系统相关的配置文件的存储路径。
    /// 在初始化系统时，相关环境变量和注册表设置可能会引用此路径以加载或存储配置数据。
    /// </summary>
    public string TargetConfigPath { get; set; } = "";

    /// <summary>
    /// 表示目标游戏是否为原版游戏的属性。
    /// 当设为 true 时，目标游戏被认为是未经修改的原版游戏，按照SEGA正常流程启动。
    /// 当设为 false 时，目标游戏可能包含修改或定制内容。
    /// 此属性通常用于决定是否执行特定的操作，比如加载修改后的配置或启动自定义功能。
    /// </summary>
    public bool IsOriginal { get; set; }

    /// <summary>
    /// 指定服务器的监听地址。
    /// </summary>
    /// <remarks>
    /// 用于配置服务器监听的IP地址，可以是特定的IP地址（如 "127.0.0.1"）或通配符地址（如 "0.0.0.0"）。
    /// 默认值为 "0.0.0.0"，代表监听所有可用的网络接口。
    /// </remarks>
    public string Listen { get; set; } = "0.0.0.0";

    /// <summary>
    /// 表示服务器监听的端口号。
    /// </summary>
    /// <remarks>
    /// 此属性用于配置服务器的监听端口。默认值为 8088。
    /// 在启动服务器时，此值将决定 TcpListener 使用的端口号。
    /// 请确保配置的端口未被其他程序占用。
    /// </remarks>
    public int Port { get; set; } = 8088;

    /// <summary>
    /// 表示应用程序的密码属性。
    /// 用于身份验证和确保访问权限安全。
    /// 此属性可以通过配置文件进行设置，并在相关组件中进行比较验证。
    /// </summary>
    public string Password { get; set; } = "";

    /// <summary>
    /// 表示下一个启动的目标路径配置。
    /// </summary>
    /// <remarks>
    /// 此属性用于指定下一步操作所需执行的文件路径，通常用于确定应用程序的启动顺序。
    /// 配置文件中的 "NextStart" 属性需要正确设置为有效的路径，以确保相关功能的正常运行。
    /// </remarks>
    public string NextStart { get; set; } = "";

    /// <summary>
    /// 表示当前目标游戏配置的标识符，仅在静态上下文中使用。
    /// 此变量存储从配置文件中加载的目标标识。
    /// 当加载配置时，会根据配置文件中的 "target" 项初始化该字段。
    /// </summary>
    private static string _target = "";

    /// <summary>
    /// 用于存储游戏列表的静态字段。
    /// 该列表通常从配置文件中解析并生成，包含配置中定义的游戏标识符。
    /// </summary>
    private static List<string> _appList = new();

    /// <summary>
    /// 表示配置对象的单例实例。
    /// </summary>
    /// <remarks>
    /// 此变量保存当前加载的配置对象，并提供全局访问点。
    /// 配置内容通常从 "config.json" 文件中读取并反序列化。
    /// </remarks>
    private static Config _config = new();


    /// <summary>
    /// 加载配置文件并初始化配置对象。
    /// 配置文件的路径可以通过环境变量 "SEGA_CUTOVER_CONFIG" 指定，
    /// 如果未指定，将默认从应用程序根目录加载 "config.json" 文件。
    /// 如果配置文件不存在或格式不正确，将返回 false。
    /// </summary>
    /// <returns>如果配置文件成功加载并解析，返回 true；否则返回 false。</returns>
    public static bool LoadConfig() {
        string configPath;
        configPath = Environment.GetEnvironmentVariable("SEGA_CUTOVER_CONFIG", EnvironmentVariableTarget.Machine);
        if (string.IsNullOrEmpty(configPath))
        {
            configPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "config.json"
            );
        }

        if (!File.Exists(configPath))
            return false;

        string jsonContent = File.ReadAllText(configPath);

        using JsonDocument document = JsonDocument.Parse(jsonContent);

        JsonElement root = document.RootElement;

        _target = root.GetProperty("target").GetString() ?? "";

        if (!root.TryGetProperty(_target, out JsonElement targetConfig))
            throw new Exception($"找不到目标配置：{_target}");

        _config = targetConfig.Deserialize<Config>()
                  ?? throw new Exception("配置反序列化失败");
        _config.TargetGameId = _target;
        return true;
    }

    /// 获取当前配置信息实例。
    /// 此方法返回配置的单例对象，以便能在应用程序的各个部分访问全局配置数据。
    /// <returns>包含配置信息的Config单例对象。</returns>
    public static Config GetConfig()
    {
        return _config;
    }

    /// <summary>
    /// 获取配置文件的根元素。
    /// </summary>
    /// <returns>
    /// 配置文件的根元素，以JsonElement形式返回。
    /// 如果配置文件不存在，则抛出FileNotFoundException。
    /// </returns>
    public static JsonElement GetConfigRoot()
    {
        String configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException("配置文件未找到");
        }
        
        String jsonContent = File.ReadAllText(configPath);
        using JsonDocument document = JsonDocument.Parse(jsonContent);

        return document.RootElement.Clone();
    }

    /// <summary>
    /// 保存当前配置到配置文件 "config.json"。
    /// 方法会将内存中的配置属性写入到磁盘中的配置文件中，覆盖原有数据。
    /// </summary>
    /// <remarks>
    /// 在保存过程中，会根据内存中当前配置的属性值，更新文件对应字段。
    /// 特别是更新 `target` 属性值为当前的目标游戏 ID（TargetGameId）
    /// </remarks>
    /// <exception cref="System.IO.IOException">
    /// 当文件无法被读取或写入时抛出此异常。
    /// </exception>
    /// <exception cref="System.Text.Json.JsonException">
    /// 当 JSON 格式不正确或解析时出错时抛出此异常。
    /// </exception>
    public static void SaveConfig()
    {
        string configPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "config.json"
        );

        string jsonContent = File.ReadAllText(configPath);

        using JsonDocument document = JsonDocument.Parse(jsonContent);

        using MemoryStream stream = new();
        using Utf8JsonWriter writer = new(stream, new JsonWriterOptions
        {
            Indented = true
        });

        writer.WriteStartObject();

        foreach (JsonProperty property in document.RootElement.EnumerateObject())
        {
            // 修改 target
            if (property.Name == "target")
            {
                writer.WriteString("target", _config.TargetGameId);
            }
            // 修改 target 对应的配置
            // else if (property.Name == _config.TargetGameId)
            // {
            //     writer.WritePropertyName(_config.TargetGameId);
            //     JsonSerializer.Serialize(writer, _config);
            // }
            else
            {
                property.WriteTo(writer);
            }
        }

        writer.WriteEndObject();
        writer.Flush();

        File.WriteAllText(
            configPath,
            System.Text.Encoding.UTF8.GetString(stream.ToArray())
        );
    }

    /// <summary>
    /// 获取应用程序名称列表。
    /// 该方法从配置文件中读取应用程序的名称，并排除指定的字段后返回剩余的名称列表。
    /// </summary>
    /// <returns>包含应用程序名称的字符串列表。</returns>
    public List<string> GetAppList()
    {
        List<String> excludedField = new List<string>() { "target", "Listen", "Port", "Password", "NextStart" };
        JsonElement configRoot = GetConfigRoot();

        if (configRoot.ValueKind != JsonValueKind.Undefined)
        {
            foreach ( JsonProperty p in configRoot.EnumerateObject())
            {
                if ( !excludedField.Contains(p.Name) )
                {
                    _appList.Add(p.Name ?? "");
                }
            }
        }
        return _appList;
    }

}