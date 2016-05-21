using dotNetDiskImager.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace dotNetDiskImager.DiskAccess
{
    public abstract class Disk : IDisposable
    {
        public delegate void OperationFinishedEventHandler(object sender, OperationFinishedEventArgs e);
        public delegate void OperationProgressChangedEventHandler(object sender, OperationProgressChangedEventArgs e);
        public delegate void OperationProgressReportEventHandler(object sender, OperationProgressReportEventArgs e);

        public virtual event OperationFinishedEventHandler OperationFinished;
        public virtual event OperationProgressChangedEventHandler OperationProgressChanged;
        public virtual event OperationProgressReportEventHandler OperationProgressReport;

        public char[] DriveLetters { get; }

        protected DiskOperation currentDiskOperation;
        protected int[] deviceIDs;
        protected int[] volumeIDs;
        protected IntPtr[] volumeHandles;
        protected IntPtr[] deviceHandles;
        protected IntPtr fileHandle = IntPtr.Zero;
        protected Thread workingThread;
        protected ulong sectorSize = 0;
        protected ulong numSectors = 0;
        protected ulong availibleSectors = 0;
        protected volatile bool cancelPending = false;
        protected string _imagePath;

        public Disk(char[] driveLetters)
        {
            deviceIDs = new int[driveLetters.Length];
            volumeIDs = new int[driveLetters.Length];
            volumeHandles = new IntPtr[driveLetters.Length];
            deviceHandles = new IntPtr[driveLetters.Length];

            DriveLetters = driveLetters;

            for (int i = 0; i < driveLetters.Length; i++)
            {
                deviceIDs[i] = NativeDiskWrapper.CheckDriveType(string.Format(@"\\.\{0}:\", DriveLetters[i]));
                volumeIDs[i] = DriveLetters[i] - 'A';
                volumeHandles[i] = deviceHandles[i] = IntPtr.Zero;
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < volumeHandles.Length; i++)
            {
                if (volumeHandles[i] != IntPtr.Zero)
                {
                    try
                    {
                        NativeDiskWrapper.RemoveLockOnVolume(volumeHandles[i]);
                    }
                    catch { }
                    NativeDisk.CloseHandle(volumeHandles[i]);
                    volumeHandles[i] = IntPtr.Zero;
                }
            }

            if (fileHandle != IntPtr.Zero)
            {
                NativeDisk.CloseHandle(fileHandle);
                fileHandle = IntPtr.Zero;
            }

            for (int i = 0; i < deviceHandles.Length; i++)
            {
                if (deviceHandles[i] != IntPtr.Zero)
                {
                    NativeDisk.CloseHandle(deviceHandles[i]);
                    deviceHandles[i] = IntPtr.Zero;
                }
            }
        }

        public void CancelOperation()
        {
            cancelPending = true;
            workingThread = null;
        }

        public ulong GetLastUsedPartition()
        {
            ulong[] numSectorsArr = new ulong[deviceHandles.Length];

            for (int x = 0; x < deviceHandles.Length; x++)
            {
                var partitionInfo = NativeDiskWrapper.GetDiskPartitionInfo(deviceHandles[x]);

                if (partitionInfo.PartitionStyle == PARTITION_STYLE.MasterBootRecord)
                {
                    numSectorsArr[x] = 1;
                    unsafe
                    {
                        byte* data = NativeDiskWrapper.ReadSectorDataPointerFromHandle(deviceHandles[x], 0, 1, sectorSize);

                        for (int i = 0; i < 4; i++)
                        {
                            ulong partitionStartSector = (uint)Marshal.ReadInt32(new IntPtr(data), 0x1BE + 8 + 16 * i);
                            ulong partitionNumSectors = (uint)Marshal.ReadInt32(new IntPtr(data), 0x1BE + 12 + 16 * i);

                            if (partitionStartSector + partitionNumSectors > numSectorsArr[x])
                            {
                                numSectorsArr[x] = partitionStartSector + partitionNumSectors;
                            }
                        }
                    }
                }
                else if (partitionInfo.PartitionStyle == PARTITION_STYLE.GuidPartitionTable)
                {
                    numSectorsArr[x] = 1;
                    unsafe
                    {
                        byte* data = NativeDiskWrapper.ReadSectorDataPointerFromHandle(deviceHandles[x], 0, 1, sectorSize);
                        uint gptHeaderOffset = (uint)Marshal.ReadInt32(new IntPtr(data), 0x1C6);
                        data = NativeDiskWrapper.ReadSectorDataPointerFromHandle(deviceHandles[x], gptHeaderOffset, 1, sectorSize);
                        ulong partitionTableSector = (ulong)Marshal.ReadInt64(new IntPtr(data), 0x48);
                        uint noOfPartitionEntries = (uint)Marshal.ReadInt32(new IntPtr(data), 0x50);
                        uint sizeOfPartitionEntry = (uint)Marshal.ReadInt32(new IntPtr(data), 0x54);

                        data = NativeDiskWrapper.ReadSectorDataPointerFromHandle(deviceHandles[x], partitionTableSector, (sectorSize / sizeOfPartitionEntry) * noOfPartitionEntries, sectorSize);

                        for (int i = 0; i < noOfPartitionEntries; i++)
                        {
                            ulong partitionStartSector = (ulong)Marshal.ReadInt64(new IntPtr(data), (int)(0x20 + sizeOfPartitionEntry * i));
                            ulong partitionNumSectors = (ulong)Marshal.ReadInt64(new IntPtr(data), (int)(0x28 + sizeOfPartitionEntry * i));

                            if (partitionStartSector + partitionNumSectors > numSectorsArr[x])
                            {
                                numSectorsArr[x] = partitionStartSector + partitionNumSectors;
                            }
                        }
                    }
                }
            }

            for (int i = 1; i < deviceHandles.Length; i++)
            {
                if (numSectorsArr[0] != numSectorsArr[i])
                    throw new Exception("Devices have different partitions size.\nVerifying will not be started.");
            }

            return numSectorsArr[0];
        }

        public abstract InitOperationResult InitReadImageFromDevice(string imagePath, bool skipUnallocated);

        public abstract void BeginReadImageFromDevice(bool verify);

        public abstract InitOperationResult InitWriteImageToDevice(string imagePath);

        public abstract void BeginWriteImageToDevice(bool verify, bool cropData = false);

        public abstract VerifyInitOperationResult InitVerifyImageAndDevice(string imagePath, bool skipUnallocated);

        public abstract void BeginVerifyImageAndDevice(ulong numBytesToVerify);

        protected abstract bool ReadImageFromDeviceWorker(ulong sectorSize, ulong numSectors);

        protected abstract Task<bool> WriteImageToDeviceWorker(ulong sectorSize, ulong numSectors);

        protected abstract Task<bool> VerifyImageAndDeviceWorkerAsync(IntPtr fileHandle, ulong sectorSize, ulong numSectors);

        public static char[] GetLogicalDrives()
        {
            List<char> drives = new List<char>();
            uint drivesMask = NativeDiskWrapper.GetLogicalDrives();

            for (int i = 0; drivesMask != 0; i++)
            {
                if ((drivesMask & 0x01) != 0)
                {
                    try
                    {
                        if (NativeDiskWrapper.CheckDriveType(string.Format(@"\\.\{0}:\", (char)('A' + i))) != 0)
                        {
                            drives.Add((char)('A' + i));
                        }
                    }
                    catch { }
                }
                drivesMask >>= 1;
            }

            return drives.ToArray();
        }

        public static char GetFirstDriveLetterFromMask(uint driveMask, bool checkDriveType = true)
        {
            for (int i = 0; driveMask != 0; i++)
            {
                if ((driveMask & 0x01) != 0)
                {
                    try
                    {
                        if (checkDriveType)
                        {
                            if (NativeDiskWrapper.CheckDriveType(string.Format(@"\\.\{0}:\", (char)('A' + i))) != 0)
                            {
                                return (char)('A' + i);
                            }
                        }
                        else
                        {
                            return (char)('A' + i);
                        }
                    }
                    catch { }
                }
                driveMask >>= 1;
            }

            return (char)0;
        }

        public static string GetModelFromDrive(char driveLetter)
        {
            try
            {
                using (var partitions = new ManagementObjectSearcher("ASSOCIATORS OF {Win32_LogicalDisk.DeviceID='" + driveLetter + ":" +
                                                 "'} WHERE ResultClass=Win32_DiskPartition"))
                {
                    foreach (var partition in partitions.Get())
                    {
                        using (var drives = new ManagementObjectSearcher("ASSOCIATORS OF {Win32_DiskPartition.DeviceID='" +
                                                             partition["DeviceID"] +
                                                             "'} WHERE ResultClass=Win32_DiskDrive"))
                        {
                            foreach (var drive in drives.Get())
                            {
                                return (string)drive["Model"];
                            }
                        }
                    }
                }
            }
            catch
            {
                return "";
            }
            return "";
        }

        public static bool IsDriveReadOnly(string drive)
        {
            bool result = false;
            try
            {
                result = NativeDiskWrapper.CheckReadOnly(drive);
            }
            catch
            {

            }
            return result;
        }

        public static ulong GetDeviceLength(int deviceID)
        {
            ulong length = 0;

            IntPtr deviceHandle = NativeDiskWrapper.GetHandleOnDevice(deviceID, NativeDisk.GENERIC_READ);

            unsafe
            {
                int returnLength = 0;
                IntPtr lengthPtr = new IntPtr(&length);

                NativeDisk.DeviceIoControl(deviceHandle, NativeDisk.IOCTL_DISK_GET_LENGTH_INFO, IntPtr.Zero, 0, lengthPtr, 8, ref returnLength, IntPtr.Zero);
            }

            NativeDisk.CloseHandle(deviceHandle);

            return length;
        }

        public async Task WipeFileSystemAndPartitions()
        {
            byte[] emptyMBR = new byte[512];
            byte[] emptyData = new byte[512 * 1024];
            List<Task> taskList = new List<Task>(volumeHandles.Length);

            emptyData.Fill(0xFF);
            emptyMBR.Fill(0x00);

            emptyMBR[440] = 1;
            emptyMBR[510] = 85;
            emptyMBR[511] = 170;

            Dispose();

            for (int i = 0; i < volumeHandles.Length; i++)
            {
                volumeHandles[i] = NativeDiskWrapper.GetHandleOnVolume(volumeIDs[i], NativeDisk.GENERIC_WRITE);
                NativeDiskWrapper.GetLockOnVolume(volumeHandles[i]);
                NativeDiskWrapper.UnmountVolume(volumeHandles[i]);
            }

            for (int i = 0; i < volumeHandles.Length; i++)
            {
                deviceHandles[i] = NativeDiskWrapper.GetHandleOnDevice(deviceIDs[i], NativeDisk.GENERIC_WRITE);
            }

            for (int i = 0; i < volumeHandles.Length; i++)
            {
                taskList.Add(NativeDiskWrapper.WriteSectorDataToHandleAsync(deviceHandles[i], emptyMBR, 0, 1, 512));
            }

            await Task.WhenAll(taskList);

            taskList.Clear();

            for (int i = 0; i < volumeHandles.Length; i++)
            {
                taskList.Add(NativeDiskWrapper.WriteSectorDataToHandleAsync(deviceHandles[i], emptyData, 0, 1, (ulong)emptyData.Length));
            }

            await Task.WhenAll(taskList);
        }
    }
}
