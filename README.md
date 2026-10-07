# SEGA-Cutover

**一种用于通过虚拟磁盘管理游戏环境和配置切换的专业系统工具。**

SEGA-Cutover 是一款专门为需要快速切换不同软件/游戏环境（例如街机游戏机或专用展示机）的场景设计的配置工具。它允许用户选择目标游戏，配置相应的虚拟磁盘 (VHD/VHDX) 或物理磁盘，并设置必要的执行路径，最后通过触发系统重启来应用所有更改。

## ✨ 功能特性

-   **🚀 环境快速切换**：从预定义的游戏列表中快速选择目标环境。
-   **💿 虚拟磁盘支持**：通过 Windows `virtdisk.dll` 无缝管理 VHD 和 VHDX 的挂载与卸载。
-   **🛠️ 自动化配置**：自动更新目标游戏的配置文件、注册表设置及执行路径。
-   **🎮 HID 设备集成**：支持通过 HID (人机接口设备) 的物理按键进行操作，实现无人值守配置。
-   **🔄 系统控制**：自动触发 Windows 重启，确保环境变更得以彻底应用并完成平滑过渡。
-   **🌐 远程监控**：内置轻量级 Web 服务器，支持远程查看状态和进行配置管理。
-   **🔒 配置持久化**：使用结构化的 JSON 配置文件，确保设置可靠且易于维护。

## 🔄 工作流程

典型的操作流程如下：
1.  **选择**：通过界面或连接的 HID 设备从列表中选择目标游戏环境。
2.  **配置**：程序会自动更新 `config.json`，包含正确的磁盘路径、游戏运行路径及相关配置。
3.  **应用**：确认选择后，程序会保存设置并触发系统重启。
4.  **启动**：系统重启后，启动项程序会根据新配置挂载目标磁盘并启动选定的游戏。

## 🛠️ 技术规格

-   **框架**：.NET Framework 4.7.2 (WPF)
-   **核心依赖**：
    -   `HidSharp`：用于 HID 设备通信。
    -   `System.Text.Json`：用于 JSON 配置管理。
    -   `Costura.Fody`：用于将依赖打包进单个可执行文件。
-   **核心 API**：
    -   通过 P/Invoke 调用 `virtdisk.dll` 进行 VHD/VHDX 管理。
    -   使用 Windows Win32 API 进行系统级操作。

## 🚀 安装与使用

1.  **下载**：获取 `SEGA-Cutover.exe` 软件包。
2.  **管理员权限**：运行程序时**必须使用管理员权限**，因为程序需要执行底层磁盘挂载和系统级配置修改。
3.  **环境准备**：确保 `config.json` 已在程序目录中正确配置，定义了可用的游戏列表和目标路径。

## ⚙️ config.json 配置指南

程序启动时会读取可执行文件所在目录的 `config.json`。也可以通过机器级环境变量
`SEGA_CUTOVER_CONFIG` 指定配置文件的绝对路径；配置文件不存在、JSON 格式错误或
`target` 对应的游戏配置不存在时，程序无法按配置初始化。

### 配置结构

根对象包含服务器设置、当前目标和一个或多个游戏配置。`target` 的值必须与某个游戏配置
的键完全一致（示例中的 `SDGB` 或 `SDEZ`）。游戏配置的键会显示在切换界面和 Web API
返回的游戏列表中。

```json
{
  "Listen": "0.0.0.0",
  "Port": 8088,
  "Password": "请修改为强密码",
  "NextStart": "sgxmasterO.exe",
  "target": "SDGB",
  "SDGB": {
    "IsVHD": true,
    "IsOriginal": false,
    "TargetDiskPath": "E:\\SDGB1.55\\internal_0.vhd",
    "IsPassSEGAPxGetHw": true,
    "TargetGameRunPath": "H:\\game.bat",
    "TargetConfigPath": "Y:\\SDGB\\MikuNET"
  },
  "SDEZ": {
    "IsOriginal": true,
    "IsVHD": false,
    "TargetDiskPath": "F:\\SDEZ",
    "IsPassSEGAPxGetHw": true,
    "TargetGameRunPath": "X:\\game.bat",
    "TargetConfigPath": "Y:\\SDEZ\\MikuNET"
  }
}
```

### 根级字段

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `Listen` | 字符串 | Web 服务器监听地址。`0.0.0.0`、`*` 或 `+` 表示监听所有网卡；`localhost` 表示仅监听本机。 |
| `Port` | 整数 | Web 服务器端口，默认 `8088`。请确认端口未被占用，并按需配置防火墙。 |
| `Password` | 字符串 | Web API 的密码。为空时不进行密码校验；对外监听时不建议留空。不要将真实密码提交到公开仓库。 |
| `NextStart` | 字符串 | 初始化完成后启动的程序路径。相对路径以 `SEGA-Cutover.exe` 所在目录为基准。 |
| `target` | 字符串 | 当前要使用的游戏配置键，必须与根对象中的游戏配置名称一致。 |

### 游戏字段

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `IsVHD` | 布尔值 | 为 `true` 时将 `TargetDiskPath` 作为 VHD/VHDX 挂载；为 `false` 时按物理磁盘或现有路径使用。 |
| `IsOriginal` | 布尔值 | 为 `true` 时按原版游戏流程启动，不启动注册表监控；为 `false` 时启用启动路径修正逻辑。 |
| `TargetDiskPath` | 字符串 | VHD/VHDX 文件路径，或非 VHD 模式下使用的目标磁盘/路径。 |
| `IsPassSEGAPxGetHw` | 布尔值 | 为 `true` 时，在检测到包含 `hw` 的启动路径时跳过硬件检测并替换为 `TargetGameRunPath`。 |
| `TargetGameRunPath` | 字符串 | 最终启动的游戏脚本或程序路径，例如 `X:\\game.bat`。 |
| `TargetConfigPath` | 字符串 | AquaMai、segatools 和 MelonLoader 配置所在目录。 |

Windows 路径在 JSON 中必须使用双反斜杠（例如 `E:\\Games\\game.vhd`），或者使用正斜杠。
切换界面确认后会更新根级 `target` 并重启 Windows；其他游戏字段由配置文件维护，不会由界面自动生成。

### Web API

服务器启动后提供以下接口：

| 方法与路径 | 说明 |
| --- | --- |
| `GET /` | 检查服务器是否运行。 |
| `GET /api/info` | 返回当前游戏、游戏列表、操作系统和时间。 |
| `GET /api/list` | 返回当前游戏和可切换的游戏列表。 |
| `GET /api/switch?gameId=SDGB&password=CHANGE_ME` | 校验密码、切换目标并重启系统。 |
| `GET /api/ui?password=CHANGE_ME` | 校验密码并显示桌面界面。 |

除 `/`、`/api/info` 和 `/api/list` 外，接口调用应提供 `password` 参数（配置密码为空时
不校验）。密码会出现在 URL 查询字符串中，请仅在可信网络中使用并做好网络隔离。

## 🧑‍💻 开发指南

### 开发环境

- Windows（项目依赖 `virtdisk.dll`、注册表和 Windows 重启 API）。
- .NET SDK，项目目标框架为 `.NET Framework 4.7.2`。
- Visual Studio 2022 或 JetBrains Rider，安装 WPF 和 .NET Framework 开发组件。
- 具备管理员权限的测试环境；不要在日常工作机上直接测试磁盘挂载和自动重启。

### 获取、构建与运行

```powershell
git clone <repository-url>
cd SEGA-Cutover
dotnet restore
dotnet build .\SEGA-Cutover.slnx --configuration Debug
dotnet run --project .\SEGA-Cutover\SEGA-Cutover.csproj
```

运行前请在 `SEGA-Cutover\config.json` 中准备有效的测试配置。发布版本使用 Release 配置，
并确保发布目录中包含 `config.json`；项目文件已将它设置为复制到输出目录。

### 代码结构

- `App.xaml.cs`：应用启动流程，加载配置并启动 Web 服务器及主逻辑。
- `Config.cs`：读取、解析、保存 `config.json`，以及生成游戏列表。
- `MainWindow.xaml` / `MainWindow.xaml.cs`：WPF 切换界面和键盘/HID 输入处理。
- `Master.cs`：VHD 挂载、环境变量/注册表设置、SEGA Boot 启动和注册表监控。
- `ModernWebServer.cs`：轻量级 TCP Web 服务器及切换、状态接口。
- `VirtualDisk.cs`、`Win32API.cs`、`RegistryHelper.cs`、`Hid.cs`：Windows、虚拟磁盘、
  注册表和 HID 的底层封装。

修改配置模型时，请同步更新 `config.json` 示例和本节字段说明。新增游戏只需在根对象中
添加一个新的游戏配置，并将 `target` 设置为该键；避免把密码、真实磁盘路径或生产环境配置
提交到版本库。涉及磁盘、注册表、重启或 Web API 的改动，应先在隔离环境中验证，再进行
`dotnet build` 和实际运行测试。

## 👥 贡献者
-   **ZCROM | MikuNET**

---
*专为特殊 Windows 部署环境下的高可靠性环境管理而设计。*