using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace SEGA_Cutover;

public class Win32API
{
    public const uint EWX_REBOOT = 0x00000002;
    public const uint SHTDN_REASON_MAJOR_APPLICATION = 0x00040000;

    private const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
    private const uint TOKEN_QUERY = 0x0008;
    private const uint SE_PRIVILEGE_ENABLED = 0x00000002;
    private const string SE_SHUTDOWN_NAME = "SeShutdownPrivilege";
    private const int ERROR_NOT_ALL_ASSIGNED = 1300;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool ExitWindowsEx(uint uFlags, uint dwReason);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool LookupPrivilegeValue(string? systemName, string name, out Luid luid);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool AdjustTokenPrivileges(
        IntPtr tokenHandle,
        bool disableAllPrivileges,
        ref TokenPrivileges newState,
        uint bufferLength,
        IntPtr previousState,
        IntPtr returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    public static void RebootWindows()
    {
        EnableShutdownPrivilege();

        if (!ExitWindowsEx(EWX_REBOOT, SHTDN_REASON_MAJOR_APPLICATION))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows reboot request failed.");
        }
    }

    private static void EnableShutdownPrivilege()
    {
        if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out IntPtr tokenHandle))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to open current process token.");
        }

        try
        {
            if (!LookupPrivilegeValue(null, SE_SHUTDOWN_NAME, out Luid shutdownPrivilegeLuid))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to look up shutdown privilege.");
            }

            TokenPrivileges tokenPrivileges = new()
            {
                PrivilegeCount = 1,
                Luid = shutdownPrivilegeLuid,
                Attributes = SE_PRIVILEGE_ENABLED,
            };

            if (!AdjustTokenPrivileges(tokenHandle, false, ref tokenPrivileges, 0, IntPtr.Zero, IntPtr.Zero))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to adjust shutdown privilege.");
            }

            int lastError = Marshal.GetLastWin32Error();
            if (lastError == ERROR_NOT_ALL_ASSIGNED)
            {
                throw new Win32Exception(lastError, "Current process does not have shutdown privilege assigned.");
            }
        }
        finally
        {
            CloseHandle(tokenHandle);
        }
    }


    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(uint dwProcessId);

    private const uint ATTACH_PARENT_PROCESS = 0xFFFFFFFF;

    public static void AttachToParentConsole()
    {
        AttachConsole(ATTACH_PARENT_PROCESS);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Luid
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TokenPrivileges
    {
        public uint PrivilegeCount;
        public Luid Luid;
        public uint Attributes;
    }
}