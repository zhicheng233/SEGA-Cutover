using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace SEGA_Cutover;

/// <summary>
/// 表示系统的主控类，提供了一系列方法来初始化系统，启动SEGA Boot程序并管理注册表轮询功能。
/// </summary>
public class Master
{
    public static bool isStopPolling = false;

    /// <summary>
    /// 表示对注册表键 "SYSTEM\SEGA\SystemProperty\master" 的引用，用于监控注册表变更。
    /// </summary>
    /// <remarks>
    /// 此变量被用于启动和停止对注册表键的监控任务，以确定是否需要执行特定操作。
    /// 在启动轮询任务时初始化，并在结束时释放资源。
    /// </remarks>
    private static RegistryKey? _watchKey;

    /// <summary>
    /// 初始化系统并执行必要的配置，包括虚拟磁盘挂载、环境变量设置、注册表修改等操作。
    /// 根据配置文件判断是否启动轮询任务，并启动SEGA Boot程序。
    /// </summary>
    public static async void initSystem()
    {
        //挂载
        Config config = Config.GetConfig();
        if (config.IsVHD)
        {
            try {
                String newDisk = VirtualDisk.Mount(config.TargetDiskPath, false);
                if (String.IsNullOrEmpty(newDisk)) throw new Exception("挂载虚拟磁盘失败");
                if (config.TargetGameRunPath[0].ToString() != newDisk)
                {
                    //更正盘符
                    config.TargetGameRunPath.Replace(config.TargetGameRunPath[0].ToString(), newDisk);
                }
            }
            catch (Exception e) {
                Console.WriteLine(e);
            }
        }
        
        //修改配置文件路径
        try {
            Environment.SetEnvironmentVariable("AQUAMAI_CONFIG", Path.Combine(config.TargetConfigPath, "AquaMai.toml") , EnvironmentVariableTarget.Machine);

            Environment.SetEnvironmentVariable("SEGATOOLS_CONFIG_PATH", Path.Combine(config.TargetConfigPath, "segatools.ini"), EnvironmentVariableTarget.Machine);

            Environment.SetEnvironmentVariable("MELONLOADER", config.TargetConfigPath, EnvironmentVariableTarget.Machine);
            //历史遗留问题
            Console.WriteLine("设置环境变量成功");
            RegistryHelper.SetValue(Registry.LocalMachine, "SYSTEM\\MelonLoad", "configPath", config.TargetConfigPath);
        }
        catch (Exception e) {
            Console.WriteLine("设置环境变量/注册表失败");
            Console.WriteLine(e);
        }


        if (!config.IsOriginal)
        {
            StartPolling();
        }

        await Task.Delay(1000);
        
        Console.WriteLine("启动SEGA Boot");
        startSEGABoot();
    }


    /// <summary>
    /// 启动 SEGA Boot 程序
    /// </summary>
    /// <remarks>
    /// 此方法负责根据配置文件指定的路径，启动下一步操作所需的 SEGA Boot 应用程序。
    /// 如果路径配置错误或应用程序启动失败，需要检查配置文件的 "NextStart" 属性是否正确。
    /// </remarks>
    public static void startSEGABoot()
    {
        String Path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Config.GetConfig().NextStart);
        Process.Start(Path);
    }

    /// <summary>
    /// 启动异步轮询任务以动态监控和处理注册表中的变化。
    /// </summary>
    /// <remarks>
    /// 该方法会启动异步任务，通过查询和监听注册表路径 SYSTEM\SEGA\SystemProperty\master 的变更，
    /// 对检测到的 NextProcess 值进行相关处理。如路径符合特定条件，则根据配置进行更新操作。
    /// 如果检测到停止标志 isStopPolling 为 true，轮询任务将终止。
    /// </remarks>
    public static void StartPolling() {
        isStopPolling = false;
        _watchKey?.Dispose();
        _watchKey = Registry.LocalMachine.OpenSubKey("SYSTEM\\SEGA\\SystemProperty\\master", writable: false);
        if (_watchKey == null)
        {
            Console.WriteLine("无法打开注册表键 SYSTEM\\SEGA\\SystemProperty\\master");
            return;
        }

        // 使用注册表变更通知替代轮询
        Task.Run(async () =>
        {
            try
            {
                while (!isStopPolling)
                {
                    String nextProcess = RegistryHelper.GetValue(Registry.LocalMachine, "SYSTEM\\SEGA\\SystemProperty\\master",
                        "NextProcess");
                    
                    
                    if (nextProcess.Contains("x:") && !nextProcess.Contains("segaboot"))
                    {
                        Config config = Config.GetConfig();
                        if (config.IsPassSEGAPxGetHw && nextProcess.ToLower().Contains("hw"))
                        {
                            Console.WriteLine("检测到 X:\\ 路径，正在更新...");
                            RegistryHelper.SetValue(Registry.LocalMachine, "SYSTEM\\SEGA\\SystemProperty\\master", "NextProcess", config.TargetGameRunPath);
                        }else if (nextProcess.Contains("game.bat"))
                        {
                            Console.WriteLine("检测到 X:\\ 路径，正在更新...");
                            RegistryHelper.SetValue(Registry.LocalMachine, "SYSTEM\\SEGA\\SystemProperty\\master", "NextProcess", config.TargetGameRunPath);
                        }
                    }

                    if (isStopPolling) break;
                    await Task.Run(() => WaitRegistryChanged(_watchKey.Handle));
                }
                Console.WriteLine("轮询已停止。");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"轮询发生异常: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// 停止异步轮询任务
    /// </summary>
    public static void StopPolling()
    {
        isStopPolling = true;
        _watchKey?.Dispose();
        _watchKey = null;
    }

    /// <summary>
    /// 表示用于注册表变更通知的标志枚举。
    /// </summary>
    [Flags]
    private enum RegChangeNotifyFilter : uint
    {
        /// <summary>
        /// 表示注册表键值的最后一次设置日期时间发生变更的通知筛选器。
        /// </summary>
        LastSet = 4
    }

    /// <summary>
    /// 注册通知以监视注册表项的更改
    /// </summary>
    /// <param name="hKey">要监视的注册表项句柄</param>
    /// <param name="bWatchSubtree">指示是否监视子项的更改</param>
    /// <param name="dwNotifyFilter">变更通知的筛选条件</param>
    /// <param name="hEvent">事件句柄，用于同步通知</param>
    /// <param name="fAsynchronous">是否以异步方式提供更改通知</param>
    /// <return>如果操作成功返回0；如果失败，返回非0错误代码</return>
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int RegNotifyChangeKeyValue(
        SafeRegistryHandle hKey,
        bool bWatchSubtree,
        RegChangeNotifyFilter dwNotifyFilter,
        IntPtr hEvent,
        bool fAsynchronous);

    /// <summary>
    /// 等待注册表键值发生变化的通知
    /// </summary>
    /// <param name="handle">用于监视注册表键的安全句柄</param>
    private static void WaitRegistryChanged(SafeRegistryHandle handle)
    {
        int result = RegNotifyChangeKeyValue(
            handle,
            false,
            RegChangeNotifyFilter.LastSet,
            IntPtr.Zero,
            false);

        if (result != 0 && !isStopPolling)
        {
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
    }
}