using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

public static class VirtualDisk
{
    private static readonly Guid MicrosoftVendorId =
        new("EC984AEC-A0F9-47E9-901F-71415A66345B");

    [StructLayout(LayoutKind.Sequential)]
    private struct VirtualStorageType
    {
        public uint DeviceId;
        public Guid VendorId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct OpenVirtualDiskParameters
    {
        public uint Version;
        public uint RWDepth;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AttachVirtualDiskParameters
    {
        public uint Version;
        public uint Reserved;
    }

    [Flags]
    private enum VirtualDiskAccessMask : uint
    {
        AttachReadOnly = 0x00010000,
        AttachReadWrite = 0x00020000,
        Detach = 0x00040000
    }

    [Flags]
    private enum AttachVirtualDiskFlags : uint
    {
        None = 0,
        ReadOnly = 1,
        PermanentLifetime = 4
    }
    
    private static DriveInfo[] GetDrives() => DriveInfo.GetDrives();
    
    [DllImport("virtdisk.dll", CharSet = CharSet.Unicode)]
    private static extern int OpenVirtualDisk(
        ref VirtualStorageType virtualStorageType,
        string path,
        VirtualDiskAccessMask accessMask,
        uint flags,
        ref OpenVirtualDiskParameters parameters,
        out SafeFileHandle handle);

    [DllImport("virtdisk.dll")]
    private static extern int AttachVirtualDisk(
        SafeFileHandle handle,
        IntPtr securityDescriptor,
        AttachVirtualDiskFlags flags,
        uint providerSpecificFlags,
        ref AttachVirtualDiskParameters parameters,
        IntPtr overlapped);

    [DllImport("virtdisk.dll")]
    private static extern int DetachVirtualDisk(
        SafeFileHandle handle,
        uint flags,
        uint providerSpecificFlags);

    public static string Mount(string path, bool readOnly = false)
    {
        HashSet<string> oldDrivesList = GetDrives()
            .Select(d => d.Name)
            .ToHashSet();

        path = Path.GetFullPath(path);

        var storageType = new VirtualStorageType
        {
            DeviceId = Path.GetExtension(path).Equals(
                ".vhdx", StringComparison.OrdinalIgnoreCase) ? 3u : 2u,
            VendorId = MicrosoftVendorId
        };

        var openParameters = new OpenVirtualDiskParameters
        {
            Version = 1,
            RWDepth = 1
        };

        var access = readOnly
            ? VirtualDiskAccessMask.AttachReadOnly
            : VirtualDiskAccessMask.AttachReadWrite;

        int error = OpenVirtualDisk(
            ref storageType,
            path,
            access,
            0,
            ref openParameters,
            out SafeFileHandle handle);

        if (error != 0)
            throw new Win32Exception(error);

        using (handle)
        {
            var attachParameters = new AttachVirtualDiskParameters
            {
                Version = 1
            };

            var flags = AttachVirtualDiskFlags.PermanentLifetime;
            if (readOnly)
                flags |= AttachVirtualDiskFlags.ReadOnly;

            error = AttachVirtualDisk(
                handle,
                IntPtr.Zero,
                flags,
                0,
                ref attachParameters,
                IntPtr.Zero);

            if (error != 0)
                throw new Win32Exception(error);
        }
        HashSet<string> nowDrivesList = GetDrives()
            .Select(d => d.Name)
            .ToHashSet();
        string newDrives = nowDrivesList.Except(oldDrivesList).FirstOrDefault();
        return newDrives.Replace(":\\\\","");

    }

    public static void Unmount(string path)
    {
        path = Path.GetFullPath(path);

        var storageType = new VirtualStorageType
        {
            DeviceId = Path.GetExtension(path).Equals(
                ".vhdx", StringComparison.OrdinalIgnoreCase) ? 3u : 2u,
            VendorId = MicrosoftVendorId
        };

        var parameters = new OpenVirtualDiskParameters
        {
            Version = 1,
            RWDepth = 1
        };

        int error = OpenVirtualDisk(
            ref storageType,
            path,
            VirtualDiskAccessMask.Detach,
            0,
            ref parameters,
            out SafeFileHandle handle);

        if (error != 0)
            throw new Win32Exception(error);

        using (handle)
        {
            error = DetachVirtualDisk(handle, 0, 0);

            if (error != 0)
                throw new Win32Exception(error);
        }
    }
}