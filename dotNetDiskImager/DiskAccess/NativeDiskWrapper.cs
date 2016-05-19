using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace dotNetDiskImager.DiskAccess
{
    internal class NativeDiskWrapper
    {
        internal static IntPtr GetHandleOnFile(string filePath, uint access)
        {
            IntPtr _handle;

            _handle = NativeDisk.CreateFileW(filePath, access, (access == NativeDisk.GENERIC_READ) ? NativeDisk.FILE_SHARE_READ : 0U, IntPtr.Zero, access == NativeDisk.GENERIC_READ ? NativeDisk.OPEN_EXISTING : NativeDisk.CREATE_ALWAYS, 0, IntPtr.Zero);

            if (_handle == NativeDisk.INVALID_HANDLE_VALUE)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return _handle;
        }

        internal static IntPtr GetHandleOnDevice(int device, uint access)
        {
            IntPtr _handle;

            _handle = NativeDisk.CreateFile(string.Format(@"\\.\PhysicalDrive{0}", device), access, NativeDisk.FILE_SHARE_READ | NativeDisk.FILE_SHARE_WRITE, IntPtr.Zero, NativeDisk.OPEN_EXISTING, 0, IntPtr.Zero);

            if (_handle == NativeDisk.INVALID_HANDLE_VALUE)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return _handle;
        }

        internal static IntPtr GetHandleOnVolume(int volume, uint access)
        {
            IntPtr _handle;
            string test = string.Format(@"\\.\{0}:", (char)(volume + 'A'));
            _handle = NativeDisk.CreateFile(test, access, NativeDisk.FILE_SHARE_READ | NativeDisk.FILE_SHARE_WRITE, IntPtr.Zero, NativeDisk.OPEN_EXISTING, 0, IntPtr.Zero);

            if (_handle == NativeDisk.INVALID_HANDLE_VALUE)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return _handle;
        }

        internal static bool GetLockOnVolume(IntPtr handle)
        {
            int bytesReturned = 0;
            bool result;

            result = NativeDisk.DeviceIoControl(handle, NativeDisk.FSCTL_LOCK_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, ref bytesReturned, IntPtr.Zero);

            if (!result)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return result;
        }

        internal static bool RemoveLockOnVolume(IntPtr handle)
        {
            int bytesReturned = 0;
            bool result;

            result = NativeDisk.DeviceIoControl(handle, NativeDisk.FSCTL_UNLOCK_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, ref bytesReturned, IntPtr.Zero);

            if (!result)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return result;
        }

        internal static bool UnmountVolume(IntPtr handle)
        {
            int bytesReturned = 0;
            bool result;

            result = NativeDisk.DeviceIoControl(handle, NativeDisk.FSCTL_DISMOUNT_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, ref bytesReturned, IntPtr.Zero);

            if (!result)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return result;
        }

        internal static bool IsVolumeMounted(IntPtr handle)
        {
            int bytesReturned = 0;
            bool result;

            result = NativeDisk.DeviceIoControl(handle, NativeDisk.FSCTL_IS_VOLUME_MOUNTED, IntPtr.Zero, 0, IntPtr.Zero, 0, ref bytesReturned, IntPtr.Zero);

            return !result;
        }

        internal static int ReadSectorDataFromHandle(IntPtr handle, byte[] data, ulong startSector, ulong numSectors, ulong sectorSize)
        {
            uint bytesRead;
            LARGE_INTEGER li;
            li.LowPart = 0;
            li.QuadPart = (long)(startSector * sectorSize);

            NativeDisk.SetFilePointer(handle, li.LowPart, out li.HighPart, EMoveMethod.Begin);
            if (!NativeDisk.ReadFile(handle, data, (uint)(sectorSize * numSectors), out bytesRead, IntPtr.Zero))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            if (bytesRead < (sectorSize * numSectors))
            {
                for (uint i = bytesRead; i < (sectorSize * numSectors); i++)
                {
                    data[i] = 0;
                }
            }

            return (int)bytesRead;
        }

        internal static Task<int> ReadSectorDataFromHandleAsync(IntPtr handle, byte[] data, ulong startSector, ulong numSectors, ulong sectorSize)
        {
            return Task.Run(() =>
            {
                uint bytesRead;
                LARGE_INTEGER li;
                li.LowPart = 0;
                li.QuadPart = (long)(startSector * sectorSize);

                NativeDisk.SetFilePointer(handle, li.LowPart, out li.HighPart, EMoveMethod.Begin);
                if (!NativeDisk.ReadFile(handle, data, (uint)(sectorSize * numSectors), out bytesRead, IntPtr.Zero))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                if (bytesRead < (sectorSize * numSectors))
                {
                    for (uint i = bytesRead; i < (sectorSize * numSectors); i++)
                    {
                        data[i] = 0;
                    }
                }

                return (int)bytesRead;
            });
        }

        internal static unsafe byte* ReadSectorDataPointerFromHandle(IntPtr handle, ulong startSector, ulong numSectors, ulong sectorSize)
        {
            uint bytesRead;
            fixed (byte* data = new byte[sectorSize * numSectors])
            {
                LARGE_INTEGER li;
                li.LowPart = 0;
                li.QuadPart = (long)(startSector * sectorSize);

                NativeDisk.SetFilePointer(handle, li.LowPart, out li.HighPart, EMoveMethod.Begin);
                if (!NativeDisk.ReadFile(handle, new IntPtr(data), (uint)(sectorSize * numSectors), out bytesRead, IntPtr.Zero))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                if (bytesRead < (sectorSize * numSectors))
                {
                    for (uint i = bytesRead; i < (sectorSize * numSectors); i++)
                    {
                        data[i] = 0;
                    }
                }

                return data;
            }
        }

        internal static void WriteSectorDataToHandle(IntPtr handle, byte[] data, ulong startSector, ulong numSectors, ulong sectorSize)
        {
            uint bytesWritten;
            bool result;
            LARGE_INTEGER li;
            li.LowPart = 0;
            li.QuadPart = (long)(startSector * sectorSize);

            NativeDisk.SetFilePointer(handle, li.LowPart, out li.HighPart, EMoveMethod.Begin);
            result = NativeDisk.WriteFile(handle, data, (uint)(sectorSize * numSectors), out bytesWritten, IntPtr.Zero);

            if (!result)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        internal static Task WriteSectorDataToHandleAsync(IntPtr handle, byte[] data, ulong startSector, ulong numSectors, ulong sectorSize)
        {
            return Task.Run(() =>
            {
                uint bytesWritten;
                bool result;
                LARGE_INTEGER li;
                li.LowPart = 0;
                li.QuadPart = (long)(startSector * sectorSize);

                NativeDisk.SetFilePointer(handle, li.LowPart, out li.HighPart, EMoveMethod.Begin);
                result = NativeDisk.WriteFile(handle, data, (uint)(sectorSize * numSectors), out bytesWritten, IntPtr.Zero);

                if (!result)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            });
        }

        internal static ulong GetNumberOfSectors(IntPtr handle, ref ulong sectorSize)
        {
            int foo = 0;
            DISK_GEOMETRY_EX diskGeometry = new DISK_GEOMETRY_EX();
            bool result;
            IntPtr diskGeometryPtr = NativeDisk.StructToPtr(diskGeometry);

            result = NativeDisk.DeviceIoControl(handle, NativeDisk.IOCTL_DISK_GET_DRIVE_GEOMETRY_EX, IntPtr.Zero, 0, diskGeometryPtr, Marshal.SizeOf(diskGeometry), ref foo, IntPtr.Zero);

            if (!result)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            diskGeometry = Marshal.PtrToStructure<DISK_GEOMETRY_EX>(diskGeometryPtr);
            Marshal.FreeHGlobal(diskGeometryPtr);

            sectorSize = (ulong)diskGeometry.Geometry.BytesPerSector;

            return (ulong)(diskGeometry.DiskSize / diskGeometry.Geometry.BytesPerSector);
        }

        internal static ulong GetFilesizeInSectors(IntPtr handle, ulong sectorSize)
        {
            ulong retVal = 0;
            long fileSize = 0;

            if (!NativeDisk.GetFileSizeEx(handle, out fileSize))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            retVal = (ulong)((fileSize / (long)sectorSize) + ((fileSize % (long)sectorSize) > 0 ? 1 : 0));

            return retVal;
        }

        internal static bool SpaceAvailible(string location, ulong spaceNeeded, out ulong freeSpace)
        {
            ulong foo = 0;
            bool result;

            result = NativeDisk.GetDiskFreeSpaceEx(location, out foo, out foo, out freeSpace);
            if (!result)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return spaceNeeded <= freeSpace;
        }

        internal static string GetDriveLabel(string drive)
        {
            StringBuilder sb = new StringBuilder(261);

            if (!NativeDisk.GetVolumeInformation(drive, sb, 261, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return sb.ToString();
        }

        internal static bool CheckReadOnly(string drive)
        {
            StringBuilder sb = new StringBuilder(261);

            FileSystemFeature flags;

            if (!NativeDisk.GetVolumeInformation(drive, sb, 261, IntPtr.Zero, IntPtr.Zero, out flags, IntPtr.Zero, 0))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return (flags & FileSystemFeature.ReadOnlyVolume) > 0;
        }

        internal static STORAGE_DEVICE_NUMBER GetDiskProperty(IntPtr handle, ref STORAGE_DEVICE_DESCRIPTOR devDescriptor)
        {
            STORAGE_PROPERTY_QUERY query = new STORAGE_PROPERTY_QUERY();
            STORAGE_DEVICE_NUMBER devInfo = new STORAGE_DEVICE_NUMBER();
            int outBytes = 0;
            bool result;
            IntPtr queryStructPtr;
            IntPtr devDescriptorStructPtr;
            IntPtr devInfoStructPtr;

            query.PropertyId = STORAGE_PROPERTY_ID.StorageDeviceProperty;
            query.QueryType = STORAGE_QUERY_TYPE.PropertyStandardQuery;

            queryStructPtr = NativeDisk.StructToPtr(query);
            devDescriptorStructPtr = NativeDisk.StructToPtr(devDescriptor);
            devInfoStructPtr = NativeDisk.StructToPtr(devInfo);

            result = NativeDisk.DeviceIoControl(handle, NativeDisk.IOCTL_STORAGE_QUERY_PROPERTY, queryStructPtr, Marshal.SizeOf(query), devDescriptorStructPtr, Marshal.SizeOf(devDescriptor), ref outBytes, IntPtr.Zero);

            Marshal.FreeHGlobal(queryStructPtr);
            Marshal.FreeHGlobal(devDescriptorStructPtr);

            if (result)
            {
                result = NativeDisk.DeviceIoControl(handle, NativeDisk.IOCTL_STORAGE_GET_DEVICE_NUMBER, IntPtr.Zero, 0, devInfoStructPtr, Marshal.SizeOf(devInfo), ref outBytes, IntPtr.Zero);
                if (!result)
                {
                    Marshal.FreeHGlobal(devInfoStructPtr);
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
            else
            {
                if (NativeDisk.DeviceIoControl(handle, NativeDisk.IOCTL_STORAGE_CHECK_VERIFY2, IntPtr.Zero, 0, IntPtr.Zero, 0, ref outBytes, IntPtr.Zero))
                {
                    Marshal.FreeHGlobal(devInfoStructPtr);
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
            var resultStruct = Marshal.PtrToStructure<STORAGE_DEVICE_NUMBER>(devInfoStructPtr);
            Marshal.FreeHGlobal(devInfoStructPtr);

            return resultStruct;
        }

        internal static int CheckDriveType(string name)
        {
            int retVal = 0;
            int bytesReaded = 0;
            IntPtr handle;

            string nameNoSlash = name.Substring(0, name.Length - 1);
            var driveType = NativeDisk.GetDriveType(name);
            switch (driveType)
            {
                case DriveType.Fixed:
                case DriveType.Removable:
                    handle = NativeDisk.CreateFile(nameNoSlash, NativeDisk.FILE_READ_ATTRIBUTES, NativeDisk.FILE_SHARE_READ | NativeDisk.FILE_SHARE_WRITE, IntPtr.Zero, NativeDisk.OPEN_EXISTING, 0, IntPtr.Zero);
                    if (handle == NativeDisk.INVALID_HANDLE_VALUE)
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }
                    else
                    {
                        STORAGE_DEVICE_DESCRIPTOR devDescriptor = new STORAGE_DEVICE_DESCRIPTOR();
                        STORAGE_DEVICE_NUMBER devNumber = GetDiskProperty(handle, ref devDescriptor);

                        if ((driveType == DriveType.Removable && devDescriptor.BusType != STORAGE_BUS_TYPE.BusTypeSata) ||
                            (driveType == DriveType.Fixed && devDescriptor.BusType == STORAGE_BUS_TYPE.BusTypeUsb) ||
                            (devDescriptor.BusType == STORAGE_BUS_TYPE.BusTypeSd) || (devDescriptor.BusType == STORAGE_BUS_TYPE.BusTypeMmc))
                        {
                            if (NativeDisk.DeviceIoControl(handle, NativeDisk.IOCTL_STORAGE_CHECK_VERIFY2, IntPtr.Zero, 0, IntPtr.Zero, 0, ref bytesReaded, IntPtr.Zero))
                            {
                                retVal = devNumber.DeviceNumber;
                            }
                            else
                            {
                                NativeDisk.CloseHandle(handle);
                                handle = NativeDisk.CreateFile(name, NativeDisk.FILE_READ_DATA, NativeDisk.FILE_SHARE_READ | NativeDisk.FILE_SHARE_WRITE, IntPtr.Zero, NativeDisk.OPEN_EXISTING, 0, IntPtr.Zero);
                                if (handle == NativeDisk.INVALID_HANDLE_VALUE)
                                {
                                    throw new Win32Exception(Marshal.GetLastWin32Error());
                                }
                                if (NativeDisk.DeviceIoControl(handle, NativeDisk.IOCTL_STORAGE_CHECK_VERIFY, IntPtr.Zero, 0, IntPtr.Zero, 0, ref bytesReaded, IntPtr.Zero))
                                {
                                    retVal = devNumber.DeviceNumber;
                                }
                            }
                        }

                        NativeDisk.CloseHandle(handle);
                    }
                    break;
                default:
                    throw new Win32Exception("Invalid device type");
            }

            return retVal;
        }

        internal static PARTITION_INFORMATION_EX GetDiskPartitionInfo(IntPtr handle)
        {
            bool result;
            int outBytes = 0;
            PARTITION_INFORMATION_EX partitionInfo = new PARTITION_INFORMATION_EX();
            IntPtr partitionInfoStrPtr;

            partitionInfoStrPtr = NativeDisk.StructToPtr(partitionInfo);

            result = NativeDisk.DeviceIoControl(handle, NativeDisk.IOCTL_DISK_GET_PARTITION_INFO_EX, IntPtr.Zero, 0, partitionInfoStrPtr, Marshal.SizeOf(partitionInfo), ref outBytes, IntPtr.Zero);
            if (!result)
            {
                Marshal.FreeHGlobal(partitionInfoStrPtr);
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            var resultStruct = partitionInfo = Marshal.PtrToStructure<PARTITION_INFORMATION_EX>(partitionInfoStrPtr);
            Marshal.FreeHGlobal(partitionInfoStrPtr);
            return resultStruct;
        }

        internal static uint GetLogicalDrives()
        {
            return NativeDisk.GetLogicalDrives();
        }

        internal static bool ByteArrayCompare(byte[] b1, byte[] b2)
        {
            // Validate buffers are the same length.
            // This also ensures that the count does not exceed the length of either buffer.  
            return b1.Length == b2.Length && NativeDisk.memcmp(b1, b2, b1.Length) == 0;
        }
    }
}
