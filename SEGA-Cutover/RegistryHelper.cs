using Microsoft.Win32;
using System;

namespace SEGA_Cutover;

public static class RegistryHelper
{
    /// <summary>
    /// 读取注册表值
    /// </summary>
    /// <param name="root">根键 (如 Registry.LocalMachine)</param>
    /// <param name="subKeyPath">子键路径</param>
    /// <param name="valueName">值名称</param>
    /// <returns>值内容，如果不存在则返回 null</returns>
    public static string? GetValue(RegistryKey root, string subKeyPath, string valueName)
    {
        try
        {
            using RegistryKey? key = root.OpenSubKey(subKeyPath);
            return key?.GetValue(valueName)?.ToString();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"读取注册表错误: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 写入/修改注册表值
    /// </summary>
    /// <param name="root">根键</param>
    /// <param name="subKeyPath">子键路径</param>
    /// <param name="valueName">值名称</param>
    /// <param name="value">要写入的内容</param>
    /// <param name="kind">数据类型 (默认字符串)</param>
    public static bool SetValue(RegistryKey root, string subKeyPath, string valueName, object value, RegistryValueKind kind = RegistryValueKind.String)
    {
        try
        {
            // CreateSubKey 如果键不存在会自动创建，如果存在则打开
            using RegistryKey key = root.CreateSubKey(subKeyPath, writable: true);
            key.SetValue(valueName, value, kind);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            Console.WriteLine("错误: 没有权限写入注册表。请尝试以管理员身份运行程序。");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"写入注册表错误: {ex.Message}");
            return false;
        }
    }

    public static void DeleteValue(RegistryKey root, string subKeyPath, string valueName)
    {
        using RegistryKey? key = root.OpenSubKey(subKeyPath, writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }
}