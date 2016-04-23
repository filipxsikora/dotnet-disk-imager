using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace dotNetDiskImager.DiskAccess
{
    public class Disk
    {
        public delegate void OperationFinishedEventHandler(object sender, OperationFinishedEventArgs eventArgs);
        public delegate void OperationProgressChangedEventHandler(object sender, OperationProgressChangedEventArgs eventArgs);
        public delegate void OperationProgressReportEventHandler(object sender, OperationProgressReportEventArgs eventArgs);

        public event OperationFinishedEventHandler OperationFinished;
        public event OperationProgressChangedEventHandler OperationProgressChanged;
        public event OperationProgressReportEventHandler OperationProgressReport;

        public char DriveLetter { get; }

        DiskOperation currentDiskOperation;
        int deviceID;
        int volumeID;
        IntPtr volumeHandle = IntPtr.Zero;
        IntPtr deviceHandle = IntPtr.Zero;
        IntPtr fileHandle = IntPtr.Zero;
        Thread workingThread;
        ulong sectorSize = 0;
        ulong numSectors = 0;
        ulong availibleSectors = 0;
        volatile bool cancelPending = false;
        string _imagePath;

        public Disk(char driveLetter)
        {
            DriveLetter = driveLetter;
            deviceID = NativeDiskWrapper.CheckDriveType(string.Format(@"\\.\{0}:\", DriveLetter));
            volumeID = DriveLetter - 'A';
        }

        public InitOperationResult InitReadImageFromDevice(string imagePath, bool skipUnallocated)
        {
            ulong fileSize;
            ulong spaceNeeded;
            availibleSectors = 0;
            sectorSize = 0;
            numSectors = 0;
            ulong freeSpace = 0;

            Dispose();

            volumeHandle = NativeDiskWrapper.GetHandleOnVolume(volumeID, NativeDisk.GENERIC_WRITE);
            NativeDiskWrapper.GetLockOnVolume(volumeHandle);
            NativeDiskWrapper.UnmountVolume(volumeHandle);

            fileHandle = NativeDiskWrapper.GetHandleOnFile(imagePath, NativeDisk.GENERIC_WRITE | NativeDisk.GENERIC_READ);
            deviceHandle = NativeDiskWrapper.GetHandleOnDevice(deviceID, NativeDisk.GENERIC_READ);

            numSectors = NativeDiskWrapper.GetNumberOfSectors(deviceHandle, ref sectorSize);

            _imagePath = imagePath;

            if (skipUnallocated)
            {
                var partitionInfo = NativeDiskWrapper.GetDiskPartitionInfo(deviceHandle);
                if (partitionInfo.PartitionStyle == PARTITION_STYLE.MasterBootRecord)
                {
                    numSectors = 1;
                    unsafe
                    {
                        byte* data = NativeDiskWrapper.ReadSectorDataPointerFromHandle(deviceHandle, 0, 1, sectorSize);

                        for (int i = 0; i < 4; i++)
                        {
                            ulong partitionStartSector = (uint)Marshal.ReadInt32(new IntPtr(data), 0x1BE + 8 + 16 * i);
                            ulong partitionNumSectors = (uint)Marshal.ReadInt32(new IntPtr(data), 0x1BE + 12 + 16 * i);

                            if (partitionStartSector + partitionNumSectors > numSectors)
                            {
                                numSectors = partitionStartSector + partitionNumSectors;
                            }
                        }
                    }
                }
                else if (partitionInfo.PartitionStyle == PARTITION_STYLE.GuidPartitionTable)
                {
                    numSectors = 1;
                    unsafe
                    {
                        byte* data = NativeDiskWrapper.ReadSectorDataPointerFromHandle(deviceHandle, 0, 1, sectorSize);
                        uint gptHeaderOffset = (uint)Marshal.ReadInt32(new IntPtr(data), 0x1C6);
                        data = NativeDiskWrapper.ReadSectorDataPointerFromHandle(deviceHandle, gptHeaderOffset, 1, sectorSize);
                        ulong partitionTableSector = (ulong)Marshal.ReadInt64(new IntPtr(data), 0x48);
                        uint noOfPartitionEntries = (uint)Marshal.ReadInt32(new IntPtr(data), 0x50);
                        uint sizeOfPartitionEntry = (uint)Marshal.ReadInt32(new IntPtr(data), 0x54);

                        data = NativeDiskWrapper.ReadSectorDataPointerFromHandle(deviceHandle, partitionTableSector, (sectorSize / sizeOfPartitionEntry) * noOfPartitionEntries, sectorSize);

                        for (int i = 0; i < noOfPartitionEntries; i++)
                        {
                            ulong partitionStartSector = (ulong)Marshal.ReadInt64(new IntPtr(data), (int)(0x20 + sizeOfPartitionEntry * i));
                            ulong partitionNumSectors = (ulong)Marshal.ReadInt64(new IntPtr(data), (int)(0x28 + sizeOfPartitionEntry * i));

                            if (partitionStartSector + partitionNumSectors > numSectors)
                            {
                                numSectors = partitionStartSector + partitionNumSectors;
                            }
                        }
                    }
                }
            }

            fileSize = NativeDiskWrapper.GetFilesizeInSectors(fileHandle, sectorSize);
            if (fileSize >= numSectors)
            {
                spaceNeeded = 0;
            }
            else
            {
                spaceNeeded = (numSectors - fileSize) * sectorSize;
            }

            if (!NativeDiskWrapper.SpaceAvailible(imagePath.Substring(0, 3), spaceNeeded, out freeSpace))
            {
                return new InitOperationResult(false, spaceNeeded, freeSpace, false);
            }

            return new InitOperationResult(true, spaceNeeded, freeSpace, false);
        }

        public void BeginReadImageFromDevice(bool verify)
        {
            cancelPending = false;
            currentDiskOperation = DiskOperation.Read;
            if (verify)
                currentDiskOperation |= DiskOperation.Verify;

            workingThread = new Thread(() =>
            {
                bool result = false;
                OperationFinishedState state = OperationFinishedState.Error;

                try
                {
                    result = ReadImageFromDeviceWorker(sectorSize, numSectors);
                    if (verify && !cancelPending)
                    {
                        OperationFinished?.Invoke(this, new OperationFinishedEventArgs(false, result && !cancelPending, state, currentDiskOperation));
                        result = InternalVerifyWorker(deviceHandle, fileHandle, sectorSize, numSectors);
                    }
                    Dispose();
                    state = OperationFinishedState.Success;
                }
                catch
                {
                    result = false;
                    state = OperationFinishedState.Error;
                }
                finally
                {
                    if (cancelPending)
                        state = OperationFinishedState.Canceled;
                    if (!result && !cancelPending)
                        state = OperationFinishedState.Error;

                    Dispose();
                    OperationFinished?.Invoke(this, new OperationFinishedEventArgs(true, result && !cancelPending, state, currentDiskOperation));
                }
            });
            workingThread.Start();
        }

        public InitOperationResult InitWriteImageToDevice(string imagePath)
        {
            availibleSectors = 0;
            sectorSize = 0;
            numSectors = 0;

            Dispose();

            volumeHandle = NativeDiskWrapper.GetHandleOnVolume(volumeID, NativeDisk.GENERIC_WRITE);
            NativeDiskWrapper.GetLockOnVolume(volumeHandle);
            NativeDiskWrapper.UnmountVolume(volumeHandle);

            fileHandle = NativeDiskWrapper.GetHandleOnFile(imagePath, NativeDisk.GENERIC_READ);
            deviceHandle = NativeDiskWrapper.GetHandleOnDevice(deviceID, NativeDisk.GENERIC_WRITE | NativeDisk.GENERIC_READ);

            availibleSectors = NativeDiskWrapper.GetNumberOfSectors(deviceHandle, ref sectorSize);
            numSectors = NativeDiskWrapper.GetFilesizeInSectors(fileHandle, sectorSize);

            if (numSectors > availibleSectors)
            {
                bool dataFound = false;
                ulong i = availibleSectors;
                ulong nextChunkSize = 0;

                while (i < numSectors && !dataFound)
                {
                    nextChunkSize = ((numSectors - i) >= 1024) ? 1024 : (numSectors - i);
                    var sectorData = NativeDiskWrapper.ReadSectorDataFromHandle(fileHandle, i, nextChunkSize, sectorSize);
                    foreach (var data in sectorData)
                    {
                        if (data != 0)
                        {
                            dataFound = true;
                            break;
                        }
                    }
                }

                return new InitOperationResult(false, numSectors * sectorSize, availibleSectors * sectorSize, dataFound);
            }

            return new InitOperationResult(true, numSectors * sectorSize, availibleSectors * sectorSize, false);
        }

        public void BeginWriteImageToDevice(bool verify, bool cropData = false)
        {
            cancelPending = false;
            currentDiskOperation = DiskOperation.Write;

            if (verify)
                currentDiskOperation |= DiskOperation.Verify;

            if (cropData)
                numSectors = availibleSectors;

            workingThread = new Thread(() =>
            {
                bool result = false;
                OperationFinishedState state = OperationFinishedState.Error;

                try
                {
                    result = WriteImageToDeviceWorker(sectorSize, numSectors);
                    if (verify && !cancelPending)
                    {
                        OperationFinished?.Invoke(this, new OperationFinishedEventArgs(false, result && !cancelPending, state, currentDiskOperation));
                        result = InternalVerifyWorker(deviceHandle, fileHandle, sectorSize, numSectors);
                    }
                    Dispose();
                    state = OperationFinishedState.Success;
                }
                catch
                {
                    result = false;
                    state = OperationFinishedState.Error;
                }
                finally
                {
                    if (cancelPending)
                        state = OperationFinishedState.Canceled;
                    if (!result && !cancelPending)
                        state = OperationFinishedState.Error;

                    Dispose();
                    OperationFinished?.Invoke(this, new OperationFinishedEventArgs(true, result && !cancelPending, state, currentDiskOperation));
                }
            });
            workingThread.Start();
        }

        public VerifyInitOperationResult InitVerifyImageAndDevice(string imagePath, bool skipUnallocated)
        {
            ulong fileSize;
            availibleSectors = 0;
            sectorSize = 0;
            numSectors = 0;

            Dispose();

            volumeHandle = NativeDiskWrapper.GetHandleOnVolume(volumeID, NativeDisk.GENERIC_WRITE);
            NativeDiskWrapper.GetLockOnVolume(volumeHandle);
            NativeDiskWrapper.UnmountVolume(volumeHandle);

            fileHandle = NativeDiskWrapper.GetHandleOnFile(imagePath, NativeDisk.GENERIC_READ);
            deviceHandle = NativeDiskWrapper.GetHandleOnDevice(deviceID, NativeDisk.GENERIC_READ);

            numSectors = NativeDiskWrapper.GetNumberOfSectors(deviceHandle, ref sectorSize);

            _imagePath = imagePath;

            if (skipUnallocated)
            {
                var partitionInfo = NativeDiskWrapper.GetDiskPartitionInfo(deviceHandle);
                if (partitionInfo.PartitionStyle == PARTITION_STYLE.MasterBootRecord)
                {
                    numSectors = 1;
                    unsafe
                    {
                        byte* data = NativeDiskWrapper.ReadSectorDataPointerFromHandle(deviceHandle, 0, 1, sectorSize);

                        for (int i = 0; i < 4; i++)
                        {
                            ulong partitionStartSector = (uint)Marshal.ReadInt32(new IntPtr(data), 0x1BE + 8 + 16 * i);
                            ulong partitionNumSectors = (uint)Marshal.ReadInt32(new IntPtr(data), 0x1BE + 12 + 16 * i);

                            if (partitionStartSector + partitionNumSectors > numSectors)
                            {
                                numSectors = partitionStartSector + partitionNumSectors;
                            }
                        }
                    }
                }
                else if (partitionInfo.PartitionStyle == PARTITION_STYLE.GuidPartitionTable)
                {
                    numSectors = 1;
                    unsafe
                    {
                        byte* data = NativeDiskWrapper.ReadSectorDataPointerFromHandle(deviceHandle, 0, 1, sectorSize);
                        uint gptHeaderOffset = (uint)Marshal.ReadInt32(new IntPtr(data), 0x1C6);
                        data = NativeDiskWrapper.ReadSectorDataPointerFromHandle(deviceHandle, gptHeaderOffset, 1, sectorSize);
                        ulong partitionTableSector = (ulong)Marshal.ReadInt64(new IntPtr(data), 0x48);
                        uint noOfPartitionEntries = (uint)Marshal.ReadInt32(new IntPtr(data), 0x50);
                        uint sizeOfPartitionEntry = (uint)Marshal.ReadInt32(new IntPtr(data), 0x54);

                        data = NativeDiskWrapper.ReadSectorDataPointerFromHandle(deviceHandle, partitionTableSector, (sectorSize / sizeOfPartitionEntry) * noOfPartitionEntries, sectorSize);

                        for (int i = 0; i < noOfPartitionEntries; i++)
                        {
                            ulong partitionStartSector = (ulong)Marshal.ReadInt64(new IntPtr(data), (int)(0x20 + sizeOfPartitionEntry * i));
                            ulong partitionNumSectors = (ulong)Marshal.ReadInt64(new IntPtr(data), (int)(0x28 + sizeOfPartitionEntry * i));

                            if (partitionStartSector + partitionNumSectors > numSectors)
                            {
                                numSectors = partitionStartSector + partitionNumSectors;
                            }
                        }
                    }
                }
            }

            fileSize = NativeDiskWrapper.GetFilesizeInSectors(fileHandle, sectorSize);
            if (fileSize == numSectors)
            {
                return new VerifyInitOperationResult(true, fileSize * sectorSize, numSectors * sectorSize);
            }
            else
            {
                return new VerifyInitOperationResult(false, fileSize * sectorSize, numSectors * sectorSize);
            }
        }

        public void BeginVerifyImageAndDevice(ulong numBytesToVerify)
        {
            cancelPending = false;
            currentDiskOperation = DiskOperation.Verify;

            numSectors = numBytesToVerify / sectorSize;

            workingThread = new Thread(() =>
            {
                bool result = false;
                OperationFinishedState state = OperationFinishedState.Error;

                try
                {
                    result = InternalVerifyWorker(deviceHandle, fileHandle, sectorSize, numSectors);
                    Dispose();
                    state = OperationFinishedState.Success;
                }
                catch
                {
                    result = false;
                    state = OperationFinishedState.Error;
                }
                finally
                {
                    if (cancelPending)
                        state = OperationFinishedState.Canceled;
                    if (!result && !cancelPending)
                        state = OperationFinishedState.Error;

                    Dispose();
                    OperationFinished?.Invoke(this, new OperationFinishedEventArgs(true, result && !cancelPending, state, currentDiskOperation));
                }
            });
            workingThread.Start();
        }

        public void CancelOperation()
        {
            cancelPending = true;
            workingThread = null;
        }

        public void Dispose()
        {
            if (volumeHandle != IntPtr.Zero)
            {
                try
                {
                    NativeDiskWrapper.RemoveLockOnVolume(volumeHandle);
                }
                catch { }
                NativeDisk.CloseHandle(volumeHandle);
                volumeHandle = IntPtr.Zero;
            }
            if (fileHandle != IntPtr.Zero)
            {
                NativeDisk.CloseHandle(fileHandle);
                fileHandle = IntPtr.Zero;
            }
            if (deviceHandle != IntPtr.Zero)
            {
                NativeDisk.CloseHandle(deviceHandle);
                deviceHandle = IntPtr.Zero;
            }
        }

        bool ReadImageFromDeviceWorker(ulong sectorSize, ulong numSectors)
        {
            Stopwatch sw = new Stopwatch();
            Stopwatch percentStopwatch = new Stopwatch();
            byte[] deviceData;
            ulong totalBytesReaded = 0;
            ulong bytesReaded = 0;
            ulong bytesToRead = sectorSize * numSectors;
            ulong bytesReadedPerPercent = 0;
            int lastProgress = 0;
            int progress = 0;

            sw.Start();
            percentStopwatch.Start();

            for (ulong i = 0; i < numSectors; i += 1024)
            {
                if (cancelPending)
                {
                    return false;
                }

                deviceData = NativeDiskWrapper.ReadSectorDataFromHandle(deviceHandle, i, (numSectors - i >= 1024) ? 1024 : (numSectors - i), sectorSize);
                NativeDiskWrapper.WriteSectorDataToHandle(fileHandle, deviceData, i, (numSectors - i >= 1024) ? 1024 : (numSectors - i), sectorSize);

                totalBytesReaded += (ulong)deviceData.Length;
                bytesReaded += (ulong)deviceData.Length;
                bytesReadedPerPercent += (ulong)deviceData.Length;
                bytesToRead -= (ulong)deviceData.Length;

                progress = (int)(i / (numSectors / 100.0)) + 1;

                if (progress != lastProgress)
                {
                    ulong averageBps = (ulong)(bytesReadedPerPercent / (percentStopwatch.ElapsedMilliseconds / 1000.0));
                    OperationProgressChanged?.Invoke(this, new OperationProgressChangedEventArgs(progress, averageBps, currentDiskOperation));
                    lastProgress = progress;
                    bytesReadedPerPercent = 0;
                    percentStopwatch.Restart();
                }

                if (sw.ElapsedMilliseconds >= 1000)
                {
                    ulong averageBps = (ulong)(bytesReaded / (sw.ElapsedMilliseconds / 1000.0));
                    OperationProgressReport?.Invoke(this, new OperationProgressReportEventArgs(averageBps, totalBytesReaded, bytesToRead));
                    bytesReaded = 0;
                    sw.Restart();
                }
            }

            return true;
        }

        bool WriteImageToDeviceWorker(ulong sectorSize, ulong numSectors)
        {
            Stopwatch msStopwatch = new Stopwatch();
            Stopwatch percentStopwatch = new Stopwatch();
            byte[] imageData;
            ulong totalBytesWritten = 0;
            ulong bytesWritten = 0;
            ulong bytesToWrite = sectorSize * numSectors;
            ulong bytesWrittenPerPercent = 0;
            int lastProgress = 0;
            int progress = 0;

            msStopwatch.Start();
            percentStopwatch.Start();

            for (ulong i = 0; i < numSectors; i += 1024)
            {
                if (cancelPending)
                {
                    return false;
                }

                imageData = NativeDiskWrapper.ReadSectorDataFromHandle(fileHandle, i, (numSectors - i >= 1024) ? 1024 : (numSectors - i), sectorSize);
                NativeDiskWrapper.WriteSectorDataToHandle(deviceHandle, imageData, i, (numSectors - i >= 1024) ? 1024 : (numSectors - i), sectorSize);
                totalBytesWritten += (ulong)imageData.Length;
                bytesWritten += (ulong)imageData.Length;
                bytesWrittenPerPercent += (ulong)imageData.Length;
                bytesToWrite -= (ulong)imageData.Length;

                progress = (int)(i / (numSectors / 100.0)) + 1;

                if (progress != lastProgress)
                {
                    ulong averageBps = (ulong)(bytesWrittenPerPercent / (percentStopwatch.ElapsedMilliseconds / 1000.0));
                    OperationProgressChanged?.Invoke(this, new OperationProgressChangedEventArgs(progress, averageBps, currentDiskOperation));
                    lastProgress = progress;
                    bytesWrittenPerPercent = 0;
                    percentStopwatch.Restart();
                }

                if (msStopwatch.ElapsedMilliseconds >= 1000)
                {
                    ulong averageBps = (ulong)(bytesWritten / (msStopwatch.ElapsedMilliseconds / 1000.0));
                    OperationProgressReport?.Invoke(this, new OperationProgressReportEventArgs(averageBps, totalBytesWritten, bytesToWrite));
                    bytesWritten = 0;
                    msStopwatch.Restart();
                }
            }

            return true;
        }

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

        private bool InternalVerifyWorker(IntPtr deviceHandle, IntPtr fileHandle, ulong sectorSize, ulong numSectors)
        {
            byte[] fileData;
            byte[] deviceData;
            Stopwatch msStopwatch = new Stopwatch();
            Stopwatch percentStopwatch = new Stopwatch();
            ulong totalBytesVerified = 0;
            ulong bytesVerified = 0;
            ulong bytesToVerify = sectorSize * numSectors;
            ulong bytesVerifiedPerPercent = 0;
            int lastProgress = 0;
            int progress = 0;

            msStopwatch.Start();
            percentStopwatch.Start();

            for (ulong i = 0; i < numSectors; i += 1024)
            {
                if (cancelPending)
                    return false;

                fileData = NativeDiskWrapper.ReadSectorDataFromHandle(fileHandle, i, (numSectors - i >= 1024) ? 1024 : (numSectors - i), sectorSize);
                deviceData = NativeDiskWrapper.ReadSectorDataFromHandle(deviceHandle, i, (numSectors - i >= 1024) ? 1024 : (numSectors - i), sectorSize);

                if (!NativeDiskWrapper.ByteArrayCompare(fileData, deviceData))
                    return false;

                totalBytesVerified += (ulong)fileData.Length;
                bytesVerified += (ulong)fileData.Length;
                bytesVerifiedPerPercent += (ulong)fileData.Length;
                bytesToVerify -= (ulong)fileData.Length;

                progress = (int)(i / (numSectors / 100.0)) + 1;

                if (progress != lastProgress)
                {
                    ulong averageBps = (ulong)(bytesVerifiedPerPercent / (percentStopwatch.ElapsedMilliseconds / 1000.0));
                    OperationProgressChanged?.Invoke(this, new OperationProgressChangedEventArgs(progress, averageBps, currentDiskOperation));
                    lastProgress = progress;
                    bytesVerifiedPerPercent = 0;
                    percentStopwatch.Restart();
                }

                if (msStopwatch.ElapsedMilliseconds >= 1000)
                {
                    ulong averageBps = (ulong)(bytesVerified / (msStopwatch.ElapsedMilliseconds / 1000.0));
                    OperationProgressReport?.Invoke(this, new OperationProgressReportEventArgs(averageBps, totalBytesVerified, bytesToVerify));
                    bytesVerified = 0;
                    msStopwatch.Restart();
                }
            }

            return true;
        }
    }
}
