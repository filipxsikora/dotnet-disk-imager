using dotNetDiskImager.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

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
        protected bool useEncryption = false;
        protected string password = null;

        protected static readonly byte[] encryptionSignature = { 0xAA, 0x55, 0xAA, 0x55, 0xAA, 0x55, 0xAA, 0x55 };
        protected const int Crypto_Iterations = 1000;

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

            Utils.PreventComputerSleep();
        }

        public void EnableEncryption(string password)
        {
            useEncryption = true;
            this.password = password;
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

            Utils.AllowComputerSleep();
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

                NativeDisk.DeviceIoControl(deviceHandle, NativeDisk.IOCTL_DISK_GET_LENGTH_INFO, IntPtr.Zero, 0, lengthPtr, sizeof(ulong), ref returnLength, IntPtr.Zero);
            }

            NativeDisk.CloseHandle(deviceHandle);

            return length;
        }

        public static ulong GetDeviceLength(IntPtr handle)
        {
            ulong length = 0;

            unsafe
            {
                int returnLength = 0;
                IntPtr lengthPtr = new IntPtr(&length);

                NativeDisk.DeviceIoControl(handle, NativeDisk.IOCTL_DISK_GET_LENGTH_INFO, IntPtr.Zero, 0, lengthPtr, sizeof(ulong), ref returnLength, IntPtr.Zero);
            }

            return length;
        }

        public static bool CheckFileEncryption(string path)
        {
            using (FileStream fs = new FileStream(path, FileMode.Open))
            {
                byte[] buffer = new byte[encryptionSignature.Length];
                fs.Read(buffer, 0, encryptionSignature.Length);
                if (NativeDiskWrapper.ByteArrayCompare(buffer, encryptionSignature))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool CheckFilePassword(string path, string password)
        {
            bool result = false;
            byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
            byte[] salt = new byte[32];

            using (FileStream fsCrypt = new FileStream(path, FileMode.Open))
            {
                fsCrypt.Read(salt, 0, encryptionSignature.Length);
                fsCrypt.Read(salt, 0, salt.Length);

                using (RijndaelManaged rijndael = new RijndaelManaged())
                {
                    rijndael.KeySize = 256;
                    rijndael.BlockSize = 128;
                    using (var key = new Rfc2898DeriveBytes(passwordBytes, salt, Crypto_Iterations))
                    {
                        rijndael.Key = key.GetBytes(rijndael.KeySize / 8);
                        rijndael.IV = key.GetBytes(rijndael.BlockSize / 8);
                    }
                    rijndael.Padding = PaddingMode.Zeros;
                    rijndael.Mode = CipherMode.CFB;

                    try
                    {
                        using (CryptoStream cryptoStream = new CryptoStream(fsCrypt, rijndael.CreateDecryptor(), CryptoStreamMode.Read))
                        {
                            int passwordLength = cryptoStream.ReadByte();

                            if (passwordBytes.Length != passwordLength)
                            {
                                return false;
                            }

                            byte[] buff = new byte[passwordLength];
                            cryptoStream.Read(buff, 0, passwordLength);
                            result = NativeDiskWrapper.ByteArrayCompare(buff, passwordBytes);
                        }
                    }
                    catch (CryptographicException)
                    {
                        return false;
                    }
                }
            }

            return result;
        }

        protected static byte[] GenerateRandomSalt()
        {
            byte[] data = new byte[32];

            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                for (int i = 0; i < 10; i++)
                {
                    rng.GetBytes(data);
                }
            }

            return data;
        }

        public async Task WipeFileSystemAndPartitions()
        {
            List<Task> taskList = new List<Task>(volumeHandles.Length);

            Dispose();

            for (int i = 0; i < volumeHandles.Length; i++)
            {
                int x = i;
                var task = Task.Run(() =>
                {
                    ReCreateMbr(x);
                });

                taskList.Add(task);
            }

            await Task.WhenAll(taskList);
        }

        void ReCreateMbr(int x)
        {
            deviceHandles[x] = NativeDiskWrapper.GetHandleOnDevice(deviceIDs[x], NativeDisk.GENERIC_READ | NativeDisk.GENERIC_WRITE);
            var driveLayout = NativeDiskWrapper.DiskGetDriveLayoutEx(deviceHandles[x]);
            NativeDiskWrapper.DiskCreateDiskMBR(deviceHandles[x], 0xA5A5A5);
            NativeDiskWrapper.DiskUpdateProperties(deviceHandles[x]);
            driveLayout = NativeDiskWrapper.DiskGetDriveLayoutEx(deviceHandles[x]);
            driveLayout.PartitionEntry[0].PartitionNumber = 1;
            driveLayout.PartitionEntry[0].StartingOffset = 4 * 512;
            driveLayout.PartitionEntry[0].PartitionLength = (long)GetDeviceLength(deviceHandles[x]) - driveLayout.PartitionEntry[0].StartingOffset;
            driveLayout.PartitionEntry[0].RewritePartition = true;
            driveLayout.PartitionEntry[0].Mbr.PartitionType = (byte)PARTITION_TYPE.PARTITION_FAT32;
            NativeDiskWrapper.DiskSetDriveLayoutEx(deviceHandles[x], driveLayout);
            NativeDiskWrapper.DiskUpdateProperties(deviceHandles[x]);
            driveLayout = NativeDiskWrapper.DiskGetDriveLayoutEx(deviceHandles[x]);
        }

        public static DiskPartitionInfo GetDiskPartitionInfo(char driveLetter)
        {
            int deviceID = NativeDiskWrapper.CheckDriveType(string.Format(@"\\.\{0}:\", driveLetter));
            IntPtr deviceHandle = NativeDiskWrapper.GetHandleOnDevice(deviceID, NativeDisk.GENERIC_READ);
            uint partitionTableSize = 0;

            DiskPartitionInfo partitionsInfo = new DiskPartitionInfo();

            var partitionInfo = NativeDiskWrapper.GetDiskPartitionInfo(deviceHandle);

            partitionsInfo.DiskTotalSize = GetDeviceLength(deviceID);

            if (partitionInfo.PartitionStyle == PARTITION_STYLE.MasterBootRecord)
            {
                partitionsInfo.PartitionType = PartitionType.MBR;
                partitionTableSize = 512;

                unsafe
                {
                    byte* data = NativeDiskWrapper.ReadSectorDataPointerFromHandle(deviceHandle, 0, 1, 512);

                    for (int i = 0; i < 4; i++)
                    {
                        ulong partitionStartSector = (uint)Marshal.ReadInt32(new IntPtr(data), 0x1BE + 8 + 16 * i);
                        ulong partitionNumSectors = (uint)Marshal.ReadInt32(new IntPtr(data), 0x1BE + 12 + 16 * i);

                        if (partitionStartSector + partitionNumSectors > 0)
                        {
                            partitionsInfo.PartitionSizes.Add(partitionNumSectors * 512);
                        }
                    }
                }
            }
            else if (partitionInfo.PartitionStyle == PARTITION_STYLE.GuidPartitionTable)
            {
                partitionsInfo.PartitionType = PartitionType.GPT;
                partitionTableSize = 17408;

                unsafe
                {
                    byte* data = NativeDiskWrapper.ReadSectorDataPointerFromHandle(deviceHandle, 0, 1, 512);
                    uint gptHeaderOffset = (uint)Marshal.ReadInt32(new IntPtr(data), 0x1C6);
                    data = NativeDiskWrapper.ReadSectorDataPointerFromHandle(deviceHandle, gptHeaderOffset, 1, 512);
                    ulong partitionTableSector = (ulong)Marshal.ReadInt64(new IntPtr(data), 0x48);
                    uint noOfPartitionEntries = (uint)Marshal.ReadInt32(new IntPtr(data), 0x50);
                    uint sizeOfPartitionEntry = (uint)Marshal.ReadInt32(new IntPtr(data), 0x54);

                    data = NativeDiskWrapper.ReadSectorDataPointerFromHandle(deviceHandle, partitionTableSector, (512 / sizeOfPartitionEntry) * noOfPartitionEntries, 512);

                    for (int i = 0; i < noOfPartitionEntries; i++)
                    {
                        ulong partitionStartSector = (ulong)Marshal.ReadInt64(new IntPtr(data), (int)(0x20 + sizeOfPartitionEntry * i));
                        ulong partitionNumSectors = (ulong)Marshal.ReadInt64(new IntPtr(data), (int)(0x28 + sizeOfPartitionEntry * i));

                        if (partitionStartSector + partitionNumSectors > 0)
                        {
                            partitionsInfo.PartitionSizes.Add(partitionNumSectors * 512);
                        }
                    }
                }
            }
            else
            {
                partitionsInfo.PartitionType = PartitionType.RAW;
            }

            NativeDisk.CloseHandle(deviceHandle);

            partitionsInfo.UnallocatedSize = partitionsInfo.DiskTotalSize - (partitionsInfo.PartitionSizes.Sum() + partitionTableSize);

            return partitionsInfo;
        }
    }
}
