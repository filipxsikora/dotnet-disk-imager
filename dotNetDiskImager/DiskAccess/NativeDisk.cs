using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace dotNetDiskImager.DiskAccess
{
    internal static class NativeDisk
    {
        internal const int FILE_SHARE_READ = 1;
        internal const int FILE_SHARE_WRITE = 2;
        internal const uint CREATE_ALWAYS = 2;
        internal const uint OPEN_EXISTING = 3;
        internal static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);
        internal const uint GENERIC_READ = 0x80000000;
        internal const uint GENERIC_WRITE = 0x40000000;
        internal const uint FSCTL_LOCK_VOLUME = (9 << 16) | (6 << 2);
        internal const uint FSCTL_UNLOCK_VOLUME = (9 << 16) | (7 << 2);
        internal const uint FSCTL_DISMOUNT_VOLUME = (9 << 16) | (8 << 2);
        internal const uint FSCTL_IS_VOLUME_MOUNTED = (9 << 16) | (10 << 2);
        internal const uint IOCTL_DISK_GET_DRIVE_GEOMETRY_EX = (7 << 16) | (0x28 << 2);
        internal const uint IOCTL_DISK_GET_PARTITION_INFO_EX = (7 << 16) | (0x12 << 2);
        internal const uint IOCTL_STORAGE_QUERY_PROPERTY = 0x2D1400;
        internal const uint IOCTL_STORAGE_GET_DEVICE_NUMBER = 0x2D1080;
        internal const uint IOCTL_STORAGE_CHECK_VERIFY2 = 0x2D0800;
        internal const uint IOCTL_STORAGE_CHECK_VERIFY = (0x2D << 16) | (1 << 14) | (0x200 << 2);
        internal const uint FILE_READ_ATTRIBUTES = 0x80;
        internal const uint FILE_READ_DATA = 0x1;

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern IntPtr CreateFileW(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern IntPtr CreateFile(
            [MarshalAs(UnmanagedType.LPTStr)] string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern bool DeviceIoControl(
            IntPtr hDevice,
            uint dwIoControlCode,
            IntPtr InBuffer,
            int nInBufferSize,
            IntPtr OutBuffer,
            int nOutBufferSize,
            ref int pBytesReturned,
            [In] IntPtr lpOverlapped);

        [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern uint SetFilePointer(
            [In] IntPtr hFile,
            [In] int lDistanceToMove,
            [Out] out int lpDistanceToMoveHigh,
            [In] EMoveMethod dwMoveMethod);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool WriteFile(
            IntPtr hFile, byte[] lpBuffer,
            uint nNumberOfBytesToWrite, out uint lpNumberOfBytesWritten,
            [In] IntPtr lpOverlapped);


        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern bool GetVolumeInformationByHandleW(
            IntPtr hDisk,
            StringBuilder volumeNameBuffer,
            int volumeNameSize,
            ref uint volumeSerialNumber,
            ref uint maximumComponentLength,
            ref uint fileSystemFlags,
            StringBuilder fileSystemNameBuffer,
            int nFileSystemNameSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern DriveType GetDriveType([MarshalAs(UnmanagedType.LPStr)] string lpRootPathName);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool ReadFile(
            IntPtr hFile,
            [Out] byte[] lpBuffer,
            uint nNumberOfBytesToRead,
            out uint lpNumberOfBytesRead,
            IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool ReadFile(
            IntPtr hFile,
            IntPtr lpBuffer,
            uint nNumberOfBytesToRead,
            out uint lpNumberOfBytesRead,
            IntPtr lpOverlapped);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern bool GetFileSizeEx(IntPtr hFile, out long lpFileSize);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetDiskFreeSpaceEx(
            [MarshalAs(UnmanagedType.LPTStr)] string lpDirectoryName,
            out ulong lpFreeBytesAvailable,
            out ulong lpTotalNumberOfBytes,
            out ulong lpTotalNumberOfFreeBytes);

        [DllImport("Kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal extern static bool GetVolumeInformation(
            string RootPathName,
            StringBuilder VolumeNameBuffer,
            int VolumeNameSize,
            IntPtr VolumeSerialNumber,
            IntPtr MaximumComponentLength,
            IntPtr FileSystemFlags,
            IntPtr FileSystemNameBuffer,
            int nFileSystemNameSize);

        [DllImport("Kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal extern static bool GetVolumeInformation(
            string RootPathName,
            StringBuilder VolumeNameBuffer,
            int VolumeNameSize,
            IntPtr VolumeSerialNumber,
            IntPtr MaximumComponentLength,
            out FileSystemFeature FileSystemFlags,
            IntPtr FileSystemNameBuffer,
            int nFileSystemNameSize);

        [DllImport("Kernel32.dll", SetLastError = true)]
        internal extern static uint GetLogicalDrives();

        internal static IntPtr StructToPtr<S>(S str)
        {
            IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf(str));
            Marshal.StructureToPtr(str, ptr, false);

            return ptr;
        }

        [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int memcmp(byte[] b1, byte[] b2, long count);
    }
}
