using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace dotNetDiskImager.DiskAccess
{
    public class DiskZip : Disk
    {
        private const int ZIP_LEAD_BYTES = 0x04034b50;

        public override event OperationFinishedEventHandler OperationFinished;
        public override event OperationProgressChangedEventHandler OperationProgressChanged;
        public override event OperationProgressReportEventHandler OperationProgressReport;

        public DiskZip(char driveLetter) : base(driveLetter)
        {
        }

        public override void BeginReadImageFromDevice(bool verify)
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
                        result = VerifyImageAndDeviceWorker(deviceHandle, fileHandle, sectorSize, numSectors);
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

        public override void BeginVerifyImageAndDevice(ulong numBytesToVerify)
        {
            throw new NotImplementedException();
        }

        public override void BeginWriteImageToDevice(bool verify, bool cropData = false)
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
                        result = VerifyImageAndDeviceWorker(deviceHandle, fileHandle, sectorSize, numSectors);
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

        public override InitOperationResult InitReadImageFromDevice(string imagePath, bool skipUnallocated)
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

        public override VerifyInitOperationResult InitVerifyImageAndDevice(string imagePath, bool skipUnallocated)
        {
            throw new NotImplementedException();
        }

        public override InitOperationResult InitWriteImageToDevice(string imagePath)
        {
            bool entryFound = false;
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

            _imagePath = imagePath;

            if (!VerifyZipFile())
            {
                throw new FileFormatException(string.Format("File {0} isn't valid zip file.", new FileInfo(_imagePath).Name));
            }

            using (FileStream fs = new FileStream(new SafeFileHandle(fileHandle, false), FileAccess.Read))
            using (ZipArchive archive = new ZipArchive(fs, ZipArchiveMode.Read))
            {
                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName.EndsWith(".img"))
                    {
                        entryFound = true;
                        numSectors = (ulong)((entry.Length / (long)sectorSize) + ((entry.Length % (long)sectorSize) > 0 ? 1 : 0));

                        if (numSectors > availibleSectors)
                        {
                            bool dataFound = false;
                            ulong i = 0;
                            ulong nextChunkSize = 0;
                            int readedLength = 0;
                            byte[] sectorData = new byte[(int)(1024 * sectorSize)];

                            Stream zipEntryStream = entry.Open();

                            while (i < numSectors && !dataFound)
                            {
                                nextChunkSize = ((numSectors - i) >= 1024) ? 1024 : (numSectors - i);
                                readedLength = zipEntryStream.Read(sectorData, 0, (int)(nextChunkSize * sectorSize));

                                i += nextChunkSize;

                                if (i < availibleSectors)
                                {
                                    continue;
                                }

                                for (int x = 0; x < readedLength; x++)
                                {
                                    if (sectorData[x] != 0)
                                    {
                                        dataFound = true;
                                        break;
                                    }
                                }
                            }

                            return new InitOperationResult(false, numSectors * sectorSize, availibleSectors * sectorSize, dataFound);
                        }
                        break;
                    }
                }
            }

            if (!entryFound)
            {
                throw new FileNotFoundException(string.Format("File {0} doesn't contain any *.img file.", new FileInfo(_imagePath).Name));
            }

            return new InitOperationResult(true, numSectors * sectorSize, availibleSectors * sectorSize, false);
        }

        protected override bool ReadImageFromDeviceWorker(ulong sectorSize, ulong numSectors)
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

            using (FileStream fileStream = new FileStream(new SafeFileHandle(fileHandle, false), FileAccess.ReadWrite))
            using (ZipArchive archive = new ZipArchive(fileStream, ZipArchiveMode.Create))
            {
                Stream zipEntryStream = archive.CreateEntry(string.Format("{0}.img", Path.GetFileNameWithoutExtension(_imagePath)), CompressionLevel.Fastest).Open();
                for (ulong i = 0; i < numSectors; i += 1024)
                {
                    if (cancelPending)
                    {
                        return false;
                    }

                    deviceData = NativeDiskWrapper.ReadSectorDataFromHandle(deviceHandle, i, (numSectors - i >= 1024) ? 1024 : (numSectors - i), sectorSize);
                    zipEntryStream.Write(deviceData, 0, deviceData.Length);

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
            }

            return true;
        }

        protected override bool VerifyImageAndDeviceWorker(IntPtr deviceHandle, IntPtr fileHandle, ulong sectorSize, ulong numSectors)
        {
            throw new NotImplementedException();
        }

        protected override bool WriteImageToDeviceWorker(ulong sectorSize, ulong numSectors)
        {
            Stopwatch msStopwatch = new Stopwatch();
            Stopwatch percentStopwatch = new Stopwatch();
            byte[] imageData = new byte[sectorSize * 1024];
            ulong totalBytesWritten = 0;
            ulong bytesWritten = 0;
            ulong bytesToWrite = sectorSize * numSectors;
            ulong bytesWrittenPerPercent = 0;
            int lastProgress = 0;
            int progress = 0;

            msStopwatch.Start();
            percentStopwatch.Start();

            using (FileStream fileStream = new FileStream(new SafeFileHandle(fileHandle, false), FileAccess.Read))
            using (ZipArchive archive = new ZipArchive(fileStream, ZipArchiveMode.Read))
            {
                Stream zipEntryStream = null;

                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName.EndsWith(".img"))
                    {
                        zipEntryStream = entry.Open();
                        break;
                    }
                }

                for (ulong i = 0; i < numSectors; i += 1024)
                {
                    if (cancelPending)
                    {
                        return false;
                    }

                    zipEntryStream.Read(imageData, 0, (int)(((numSectors - i >= 1024) ? 1024 : (numSectors - i)) * sectorSize));
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
            }

            return true;
        }

        bool VerifyZipFile()
        {
            var data = NativeDiskWrapper.ReadSectorDataFromHandle(fileHandle, 0, 1, sectorSize);

            if (BitConverter.ToInt32(data, 0) == ZIP_LEAD_BYTES)
                return true;
            else
                return false;
        }
    }
}
