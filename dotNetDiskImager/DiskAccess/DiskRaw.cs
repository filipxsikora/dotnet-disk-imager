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
    public class DiskRaw : Disk
    {
        public override event OperationFinishedEventHandler OperationFinished;
        public override event OperationProgressChangedEventHandler OperationProgressChanged;
        public override event OperationProgressReportEventHandler OperationProgressReport;

        public DiskRaw(char[] driveLetter) : base(driveLetter)
        {

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

            volumeHandles[0] = NativeDiskWrapper.GetHandleOnVolume(volumeIDs[0], NativeDisk.GENERIC_WRITE);
            NativeDiskWrapper.GetLockOnVolume(volumeHandles[0]);
            NativeDiskWrapper.UnmountVolume(volumeHandles[0]);

            fileHandle = NativeDiskWrapper.GetHandleOnFile(imagePath, NativeDisk.GENERIC_WRITE | NativeDisk.GENERIC_READ);
            deviceHandles[0] = NativeDiskWrapper.GetHandleOnDevice(deviceIDs[0], NativeDisk.GENERIC_READ);

            numSectors = NativeDiskWrapper.GetNumberOfSectors(deviceHandles[0], ref sectorSize);

            _imagePath = imagePath;

            if (skipUnallocated)
            {
                numSectors = GetLastUsedPartition();
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
                        result = VerifyImageAndDeviceWorker(deviceHandles[0], fileHandle, sectorSize, numSectors);
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

        public override InitOperationResult InitWriteImageToDevice(string imagePath)
        {
            availibleSectors = 0;
            sectorSize = 0;
            numSectors = 0;

            Dispose();

            volumeHandles[0] = NativeDiskWrapper.GetHandleOnVolume(volumeIDs[0], NativeDisk.GENERIC_WRITE);
            NativeDiskWrapper.GetLockOnVolume(volumeHandles[0]);
            NativeDiskWrapper.UnmountVolume(volumeHandles[0]);

            fileHandle = NativeDiskWrapper.GetHandleOnFile(imagePath, NativeDisk.GENERIC_READ);
            deviceHandles[0] = NativeDiskWrapper.GetHandleOnDevice(deviceIDs[0], NativeDisk.GENERIC_WRITE | NativeDisk.GENERIC_READ);

            availibleSectors = NativeDiskWrapper.GetNumberOfSectors(deviceHandles[0], ref sectorSize);
            numSectors = NativeDiskWrapper.GetFilesizeInSectors(fileHandle, sectorSize);

            _imagePath = imagePath;

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

                    i += nextChunkSize;
                }

                return new InitOperationResult(false, numSectors * sectorSize, availibleSectors * sectorSize, dataFound);
            }

            return new InitOperationResult(true, numSectors * sectorSize, availibleSectors * sectorSize, false);
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
                        result = VerifyImageAndDeviceWorker(deviceHandles[0], fileHandle, sectorSize, numSectors);
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

        public override VerifyInitOperationResult InitVerifyImageAndDevice(string imagePath, bool skipUnallocated)
        {
            ulong fileSize;
            availibleSectors = 0;
            sectorSize = 0;
            numSectors = 0;

            Dispose();

            volumeHandles[0] = NativeDiskWrapper.GetHandleOnVolume(volumeIDs[0], NativeDisk.GENERIC_WRITE);
            NativeDiskWrapper.GetLockOnVolume(volumeHandles[0]);
            NativeDiskWrapper.UnmountVolume(volumeHandles[0]);

            fileHandle = NativeDiskWrapper.GetHandleOnFile(imagePath, NativeDisk.GENERIC_READ);
            deviceHandles[0] = NativeDiskWrapper.GetHandleOnDevice(deviceIDs[0], NativeDisk.GENERIC_READ);

            numSectors = NativeDiskWrapper.GetNumberOfSectors(deviceHandles[0], ref sectorSize);

            _imagePath = imagePath;

            if (skipUnallocated)
            {
                numSectors = GetLastUsedPartition();
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

        public override void BeginVerifyImageAndDevice(ulong numBytesToVerify)
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
                    result = VerifyImageAndDeviceWorker(deviceHandles[0], fileHandle, sectorSize, numSectors);
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

            for (ulong i = 0; i < numSectors; i += 1024)
            {
                if (cancelPending)
                {
                    return false;
                }

                deviceData = NativeDiskWrapper.ReadSectorDataFromHandle(deviceHandles[0], i, (numSectors - i >= 1024) ? 1024 : (numSectors - i), sectorSize);
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

        protected override bool WriteImageToDeviceWorker(ulong sectorSize, ulong numSectors)
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
                NativeDiskWrapper.WriteSectorDataToHandle(deviceHandles[0], imageData, i, (numSectors - i >= 1024) ? 1024 : (numSectors - i), sectorSize);
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

        protected override bool VerifyImageAndDeviceWorker(IntPtr deviceHandle, IntPtr fileHandle, ulong sectorSize, ulong numSectors)
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
