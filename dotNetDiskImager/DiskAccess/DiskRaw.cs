using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
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

        public DiskRaw(char[] driveLetters) : base(driveLetters)
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

            workingThread = new Thread(async () =>
            {
                bool result = false;
                OperationFinishedState state = OperationFinishedState.Error;
                Exception exception = null;

                try
                {
                    if (useEncryption)
                    {
                        result = ReadImageFromDeviceCryptoWorker(sectorSize, numSectors);
                    }
                    else
                    {
                        result = ReadImageFromDeviceWorker(sectorSize, numSectors);
                    }

                    if (verify && !cancelPending)
                    {
                        OperationFinished?.Invoke(this, new OperationFinishedEventArgs(false, result && !cancelPending, state, currentDiskOperation, null));

                        if (useEncryption)
                        {
                            result = await VerifyImageAndDeviceCryptoWorkerAsync(fileHandle, sectorSize, numSectors);
                        }
                        else
                        {
                            result = await VerifyImageAndDeviceWorkerAsync(fileHandle, sectorSize, numSectors);
                        }
                    }

                    Dispose();
                    state = OperationFinishedState.Success;
                }
                catch (Exception e)
                {
                    result = false;
                    state = OperationFinishedState.Error;
                    exception = e;
                }
                finally
                {
                    if (cancelPending)
                        state = OperationFinishedState.Canceled;
                    if (!result && !cancelPending)
                        state = OperationFinishedState.Error;

                    Dispose();
                    OperationFinished?.Invoke(this, new OperationFinishedEventArgs(true, result && !cancelPending, state, currentDiskOperation, exception));
                }
            });
            workingThread.Start();
        }

        public override InitOperationResult InitWriteImageToDevice(string imagePath)
        {
            availibleSectors = 0;
            sectorSize = 0;
            numSectors = 0;
            int smallestDeviceIndex = 0;

            Dispose();

            for (int i = 0; i < volumeHandles.Length; i++)
            {
                volumeHandles[i] = NativeDiskWrapper.GetHandleOnVolume(volumeIDs[i], NativeDisk.GENERIC_WRITE);
                NativeDiskWrapper.GetLockOnVolume(volumeHandles[i]);
                NativeDiskWrapper.UnmountVolume(volumeHandles[i]);
            }

            fileHandle = NativeDiskWrapper.GetHandleOnFile(imagePath, NativeDisk.GENERIC_READ);

            for (int i = 0; i < volumeHandles.Length; i++)
            {
                deviceHandles[i] = NativeDiskWrapper.GetHandleOnDevice(deviceIDs[i], NativeDisk.GENERIC_WRITE | NativeDisk.GENERIC_READ);
            }

            availibleSectors = NativeDiskWrapper.GetNumberOfSectors(deviceHandles[0], ref sectorSize);

            for (int i = 1; i < deviceHandles.Length; i++)
            {
                var sectors = NativeDiskWrapper.GetNumberOfSectors(deviceHandles[i], ref sectorSize);

                if (sectors < availibleSectors)
                {
                    availibleSectors = sectors;
                    smallestDeviceIndex = i;
                }
            }

            if (useEncryption)
            {
                int paddingSize = 16 - ((1 + password.Length) % 16);
                numSectors = NativeDiskWrapper.GetFilesizeInSectors(fileHandle, sectorSize, encryptionSignature.Length + 33 + password.Length + paddingSize);
            }
            else
            {
                numSectors = NativeDiskWrapper.GetFilesizeInSectors(fileHandle, sectorSize);
            }

            _imagePath = imagePath;

            if (numSectors > availibleSectors)
            {
                bool dataFound = false;
                ulong i = availibleSectors;
                ulong nextChunkSize = 0;
                byte[] sectorData = new byte[1024 * sectorSize];

                if (useEncryption)
                {
                    using (FileStream fs = new FileStream(new SafeFileHandle(fileHandle, false), FileAccess.Read))
                    using (RijndaelManaged rijndael = new RijndaelManaged())
                    {
                        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
                        byte[] salt = new byte[32];

                        fs.Read(salt, 0, encryptionSignature.Length);
                        fs.Read(salt, 0, salt.Length);

                        rijndael.KeySize = 256;
                        rijndael.BlockSize = 128;

                        using (var key = new Rfc2898DeriveBytes(passwordBytes, salt, 1000))
                        {
                            rijndael.Key = key.GetBytes(rijndael.KeySize / 8);
                            rijndael.IV = key.GetBytes(rijndael.BlockSize / 8);
                        }

                        rijndael.Padding = PaddingMode.Zeros;
                        rijndael.Mode = CipherMode.CFB;

                        using (CryptoStream cs = new CryptoStream(fs, rijndael.CreateDecryptor(), CryptoStreamMode.Read))
                        {
                            int passLen = cs.ReadByte();
                            cs.Read(sectorData, 0, passLen);

                            {
                                while (i < numSectors && !dataFound)
                                {
                                    nextChunkSize = ((numSectors - i) >= 1024) ? 1024 : (numSectors - i);
                                    int dataLength = cs.Read(sectorData, 0, (int)(nextChunkSize * sectorSize));

                                    for (int x = 0; x < dataLength; x++)
                                    {
                                        if (sectorData[x] != 0)
                                        {
                                            dataFound = true;
                                            break;
                                        }
                                    }

                                    i += nextChunkSize;
                                }
                            }
                        }
                    }
                }
                else
                {
                    while (i < numSectors && !dataFound)
                    {
                        nextChunkSize = ((numSectors - i) >= 1024) ? 1024 : (numSectors - i);
                        int dataLength = NativeDiskWrapper.ReadSectorDataFromHandle(fileHandle, sectorData, i, nextChunkSize, sectorSize);

                        for (int x = 0; x < dataLength; x++)
                        {
                            if (sectorData[x] != 0)
                            {
                                dataFound = true;
                                break;
                            }
                        }

                        i += nextChunkSize;
                    }
                }

                return new InitOperationResult(false, numSectors * sectorSize, availibleSectors * sectorSize, dataFound, DriveLetters[smallestDeviceIndex]);
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

            workingThread = new Thread(async () =>
            {
                bool result = false;
                OperationFinishedState state = OperationFinishedState.Error;
                Exception exception = null;

                try
                {
                    if (useEncryption)
                    {
                        result = await WriteImageToDeviceCryptoWorker(sectorSize, numSectors);
                    }
                    else
                    {
                        result = await WriteImageToDeviceWorker(sectorSize, numSectors);
                    }

                    if (verify && !cancelPending)
                    {
                        OperationFinished?.Invoke(this, new OperationFinishedEventArgs(false, result && !cancelPending, state, currentDiskOperation, null));

                        if (useEncryption)
                        {
                            result = await VerifyImageAndDeviceCryptoWorkerAsync(fileHandle, sectorSize, numSectors);
                        }
                        else
                        {
                            result = await VerifyImageAndDeviceWorkerAsync(fileHandle, sectorSize, numSectors);
                        }
                    }
                    Dispose();
                    state = OperationFinishedState.Success;
                }
                catch (Exception e)
                {
                    result = false;
                    state = OperationFinishedState.Error;
                    exception = e;
                }
                finally
                {
                    if (cancelPending)
                        state = OperationFinishedState.Canceled;
                    if (!result && !cancelPending)
                        state = OperationFinishedState.Error;

                    Dispose();
                    OperationFinished?.Invoke(this, new OperationFinishedEventArgs(true, result && !cancelPending, state, currentDiskOperation, exception));
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

            for (int i = 0; i < volumeHandles.Length; i++)
            {
                volumeHandles[i] = NativeDiskWrapper.GetHandleOnVolume(volumeIDs[i], NativeDisk.GENERIC_WRITE);
                NativeDiskWrapper.GetLockOnVolume(volumeHandles[i]);
                NativeDiskWrapper.UnmountVolume(volumeHandles[i]);
            }

            fileHandle = NativeDiskWrapper.GetHandleOnFile(imagePath, NativeDisk.GENERIC_READ);

            for (int i = 0; i < volumeHandles.Length; i++)
            {
                deviceHandles[i] = NativeDiskWrapper.GetHandleOnDevice(deviceIDs[i], NativeDisk.GENERIC_READ);
            }

            _imagePath = imagePath;

            numSectors = NativeDiskWrapper.GetNumberOfSectors(deviceHandles[0], ref sectorSize);

            if (skipUnallocated)
            {
                numSectors = GetLastUsedPartition();
            }
            else
            {
                for (int i = 0; i < deviceHandles.Length; i++)
                {
                    var sectors = NativeDiskWrapper.GetNumberOfSectors(deviceHandles[i], ref sectorSize);
                    if (sectors < numSectors)
                    {
                        numSectors = sectors;
                    }
                }
            }

            if (useEncryption)
            {
                int paddingSize = 16 - ((1 + password.Length) % 16);
                fileSize = NativeDiskWrapper.GetFilesizeInSectors(fileHandle, sectorSize, encryptionSignature.Length + 33 + password.Length + paddingSize);
            }
            else
            {
                fileSize = NativeDiskWrapper.GetFilesizeInSectors(fileHandle, sectorSize);
            }

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

            workingThread = new Thread(async () =>
            {
                bool result = false;
                OperationFinishedState state = OperationFinishedState.Error;
                Exception exception = null;

                try
                {
                    if (useEncryption)
                    {
                        result = await VerifyImageAndDeviceCryptoWorkerAsync(fileHandle, sectorSize, numSectors);
                    }
                    else
                    {
                        result = await VerifyImageAndDeviceWorkerAsync(fileHandle, sectorSize, numSectors);
                    }

                    Dispose();
                    state = OperationFinishedState.Success;
                }
                catch (Exception e)
                {
                    result = false;
                    state = OperationFinishedState.Error;
                    exception = e;
                }
                finally
                {
                    if (cancelPending)
                        state = OperationFinishedState.Canceled;
                    if (!result && !cancelPending)
                        state = OperationFinishedState.Error;

                    Dispose();
                    OperationFinished?.Invoke(this, new OperationFinishedEventArgs(true, result && !cancelPending, state, currentDiskOperation, exception));
                }
            });
            workingThread.Start();
        }

        protected override bool ReadImageFromDeviceWorker(ulong sectorSize, ulong numSectors)
        {
            Stopwatch sw = new Stopwatch();
            Stopwatch percentStopwatch = new Stopwatch();
            byte[] deviceData = new byte[1024 * sectorSize];
            ulong totalBytesReaded = 0;
            ulong bytesReaded = 0;
            ulong bytesToRead = sectorSize * numSectors;
            ulong bytesReadedPerPercent = 0;
            int lastProgress = 0;
            int progress = 0;
            int readed = 0;

            sw.Start();
            percentStopwatch.Start();

            for (ulong i = 0; i < numSectors; i += 1024)
            {
                if (cancelPending)
                {
                    return false;
                }

                readed = NativeDiskWrapper.ReadSectorDataFromHandle(deviceHandles[0], deviceData, i, (numSectors - i >= 1024) ? 1024 : (numSectors - i), sectorSize);
                NativeDiskWrapper.WriteSectorDataToHandle(fileHandle, deviceData, i, (numSectors - i >= 1024) ? 1024 : (numSectors - i), sectorSize, readed);

                totalBytesReaded += (ulong)readed;
                bytesReaded += (ulong)readed;
                bytesReadedPerPercent += (ulong)readed;
                bytesToRead -= (ulong)readed;

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

        protected bool ReadImageFromDeviceCryptoWorker(ulong sectorSize, ulong numSectors)
        {
            Stopwatch sw = new Stopwatch();
            Stopwatch percentStopwatch = new Stopwatch();
            byte[] deviceData = new byte[1024 * sectorSize];
            ulong totalBytesReaded = 0;
            ulong bytesReaded = 0;
            ulong bytesToRead = sectorSize * numSectors;
            ulong bytesReadedPerPercent = 0;
            int lastProgress = 0;
            int progress = 0;
            int readed = 0;

            sw.Start();
            percentStopwatch.Start();

            using (FileStream fileStream = new FileStream(new SafeFileHandle(fileHandle, false), FileAccess.ReadWrite))
            using (RijndaelManaged rijndael = new RijndaelManaged())
            {
                byte[] salt = GenerateRandomSalt();
                byte[] passwordBytes = Encoding.UTF8.GetBytes(password);

                fileStream.Write(encryptionSignature, 0, encryptionSignature.Length);
                fileStream.Write(salt, 0, salt.Length);

                rijndael.KeySize = 256;
                rijndael.BlockSize = 128;
                rijndael.Padding = PaddingMode.Zeros;

                using (var key = new Rfc2898DeriveBytes(passwordBytes, salt, Crypto_Iterations))
                {
                    rijndael.Key = key.GetBytes(rijndael.KeySize / 8);
                    rijndael.IV = key.GetBytes(rijndael.BlockSize / 8);
                }
                rijndael.Mode = CipherMode.CFB;

                using (CryptoStream cryptoStream = new CryptoStream(fileStream, rijndael.CreateEncryptor(), CryptoStreamMode.Write))
                {
                    cryptoStream.WriteByte((byte)passwordBytes.Length);
                    cryptoStream.Write(passwordBytes, 0, passwordBytes.Length);

                    for (ulong i = 0; i < numSectors; i += 1024)
                    {
                        if (cancelPending)
                        {
                            return false;
                        }

                        readed = NativeDiskWrapper.ReadSectorDataFromHandle(deviceHandles[0], deviceData, i, (numSectors - i >= 1024) ? 1024 : (numSectors - i), sectorSize);
                        cryptoStream.Write(deviceData, 0, readed);

                        totalBytesReaded += (ulong)readed;
                        bytesReaded += (ulong)readed;
                        bytesReadedPerPercent += (ulong)readed;
                        bytesToRead -= (ulong)readed;

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
            }

            return true;
        }

        protected override async Task<bool> WriteImageToDeviceWorker(ulong sectorSize, ulong numSectors)
        {
            Stopwatch msStopwatch = new Stopwatch();
            Stopwatch percentStopwatch = new Stopwatch();
            byte[] imageData = new byte[1024 * sectorSize];
            ulong totalBytesWritten = 0;
            ulong bytesWritten = 0;
            ulong bytesToWrite = sectorSize * numSectors;
            ulong bytesWrittenPerPercent = 0;
            int lastProgress = 0;
            int progress = 0;
            int readed = 0;
            List<Task> taskList = new List<Task>(deviceHandles.Length);

            msStopwatch.Start();
            percentStopwatch.Start();

            for (ulong i = 0; i < numSectors; i += 1024)
            {
                taskList.Clear();

                if (cancelPending)
                {
                    return false;
                }

                readed = NativeDiskWrapper.ReadSectorDataFromHandle(fileHandle, imageData, i, (numSectors - i >= 1024) ? 1024 : (numSectors - i), sectorSize);

                foreach (var deviceHandle in deviceHandles)
                {
                    taskList.Add(NativeDiskWrapper.WriteSectorDataToHandleAsync(deviceHandle, imageData, i, (numSectors - i >= 1024) ? 1024 : (numSectors - i), sectorSize));
                }

                await Task.WhenAll(taskList);

                totalBytesWritten += (ulong)readed;
                bytesWritten += (ulong)readed;
                bytesWrittenPerPercent += (ulong)readed;
                bytesToWrite -= (ulong)readed;

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

        protected async Task<bool> WriteImageToDeviceCryptoWorker(ulong sectorSize, ulong numSectors)
        {
            Stopwatch msStopwatch = new Stopwatch();
            Stopwatch percentStopwatch = new Stopwatch();
            byte[] imageData = new byte[1024 * sectorSize];
            ulong totalBytesWritten = 0;
            ulong bytesWritten = 0;
            ulong bytesToWrite = sectorSize * numSectors;
            ulong bytesWrittenPerPercent = 0;
            int lastProgress = 0;
            int progress = 0;
            int readed = 0;
            List<Task> taskList = new List<Task>(deviceHandles.Length);
            byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
            byte[] salt = new byte[32];

            msStopwatch.Start();
            percentStopwatch.Start();

            using (FileStream inputFile = new FileStream(new SafeFileHandle(fileHandle, false), FileAccess.Read))
            using (RijndaelManaged rijndael = new RijndaelManaged())
            {
                inputFile.Read(salt, 0, encryptionSignature.Length);
                inputFile.Read(salt, 0, salt.Length);

                rijndael.KeySize = 256;
                rijndael.BlockSize = 128;

                using (var key = new Rfc2898DeriveBytes(passwordBytes, salt, 1000))
                {
                    rijndael.Key = key.GetBytes(rijndael.KeySize / 8);
                    rijndael.IV = key.GetBytes(rijndael.BlockSize / 8);
                }

                rijndael.Padding = PaddingMode.Zeros;
                rijndael.Mode = CipherMode.CFB;

                using (CryptoStream cs = new CryptoStream(inputFile, rijndael.CreateDecryptor(), CryptoStreamMode.Read))
                {
                    int passLen = cs.ReadByte();
                    cs.Read(imageData, 0, passLen);

                    for (ulong i = 0; i < numSectors; i += 1024)
                    {
                        taskList.Clear();

                        if (cancelPending)
                        {
                            return false;
                        }

                        readed = cs.Read(imageData, 0, imageData.Length);

                        foreach (var deviceHandle in deviceHandles)
                        {
                            taskList.Add(NativeDiskWrapper.WriteSectorDataToHandleAsync(deviceHandle, imageData, i, (numSectors - i >= 1024) ? 1024 : (numSectors - i), sectorSize));
                        }

                        await Task.WhenAll(taskList);

                        totalBytesWritten += (ulong)readed;
                        bytesWritten += (ulong)readed;
                        bytesWrittenPerPercent += (ulong)readed;
                        bytesToWrite -= (ulong)readed;

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
            }
            return true;
        }

        protected override async Task<bool> VerifyImageAndDeviceWorkerAsync(IntPtr fileHandle, ulong sectorSize, ulong numSectors)
        {
            byte[] fileData = new byte[1024 * sectorSize];
            Stopwatch msStopwatch = new Stopwatch();
            Stopwatch percentStopwatch = new Stopwatch();
            ulong totalBytesVerified = 0;
            ulong bytesVerified = 0;
            ulong bytesToVerify = sectorSize * numSectors;
            ulong bytesVerifiedPerPercent = 0;
            int lastProgress = 0;
            int progress = 0;
            int readed = 0;
            List<Task<bool>> taskList = new List<Task<bool>>(deviceHandles.Length);
            byte[][] deviceData = new byte[deviceHandles.Length][];
            int failedDeviceIndex = 0;

            for (int i = 0; i < deviceHandles.Length; i++)
            {
                deviceData[i] = new byte[1024 * sectorSize];
            }

            msStopwatch.Start();
            percentStopwatch.Start();

            for (ulong i = 0; i < numSectors; i += 1024)
            {
                taskList.Clear();

                if (cancelPending)
                    return false;

                readed = NativeDiskWrapper.ReadSectorDataFromHandle(fileHandle, fileData, i, (numSectors - i >= 1024) ? 1024 : (numSectors - i), sectorSize);
                for (int x = 0; x < deviceHandles.Length; x++)
                {
                    int index = x;
                    taskList.Add(Task.Run(() =>
                    {
                        NativeDiskWrapper.ReadSectorDataFromHandle(deviceHandles[index], deviceData[index], i, (numSectors - i >= 1024) ? 1024 : (numSectors - i), sectorSize);

                        if (!NativeDiskWrapper.ByteArrayCompare(fileData, deviceData[index]))
                        {
                            failedDeviceIndex = index;
                            return false;
                        }
                        else
                        {
                            return true;
                        }
                    }));
                }

                await Task.WhenAll(taskList);

                foreach (var task in taskList)
                {
                    if (!task.Result)
                    {
                        for (ulong x = 0; x < 1024 * sectorSize; x++)
                        {
                            if (deviceData[failedDeviceIndex][x] != fileData[x])
                            {
                                throw new Exception(string.Format("Verify found different data. Device {0}:\\ at byte {1:n0}, file data: 0x{2:X2}, device data: 0x{3:X2}", DriveLetters[failedDeviceIndex], i * sectorSize + x, deviceData[failedDeviceIndex][x], fileData[x]));
                            }
                        }
                        return false;
                    }
                }

                totalBytesVerified += (ulong)readed;
                bytesVerified += (ulong)readed;
                bytesVerifiedPerPercent += (ulong)readed;
                bytesToVerify -= (ulong)readed;

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

        protected async Task<bool> VerifyImageAndDeviceCryptoWorkerAsync(IntPtr fileHandle, ulong sectorSize, ulong numSectors)
        {
            byte[] fileData = new byte[1024 * sectorSize];
            Stopwatch msStopwatch = new Stopwatch();
            Stopwatch percentStopwatch = new Stopwatch();
            ulong totalBytesVerified = 0;
            ulong bytesVerified = 0;
            ulong bytesToVerify = sectorSize * numSectors;
            ulong bytesVerifiedPerPercent = 0;
            int lastProgress = 0;
            int progress = 0;
            int readed = 0;
            List<Task<bool>> taskList = new List<Task<bool>>(deviceHandles.Length);
            byte[][] deviceData = new byte[deviceHandles.Length][];
            int failedDeviceIndex = 0;
            byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
            byte[] salt = new byte[32];

            for (int i = 0; i < deviceHandles.Length; i++)
            {
                deviceData[i] = new byte[1024 * sectorSize];
            }

            msStopwatch.Start();
            percentStopwatch.Start();

            using (FileStream inputFile = new FileStream(new SafeFileHandle(fileHandle, false), FileAccess.Read))
            using (RijndaelManaged rijndael = new RijndaelManaged())
            {
                inputFile.Seek(0, SeekOrigin.Begin);
                inputFile.Read(salt, 0, encryptionSignature.Length);
                inputFile.Read(salt, 0, salt.Length);

                rijndael.KeySize = 256;
                rijndael.BlockSize = 128;

                using (var key = new Rfc2898DeriveBytes(passwordBytes, salt, 1000))
                {
                    rijndael.Key = key.GetBytes(rijndael.KeySize / 8);
                    rijndael.IV = key.GetBytes(rijndael.BlockSize / 8);
                }

                rijndael.Padding = PaddingMode.Zeros;
                rijndael.Mode = CipherMode.CFB;

                using (CryptoStream cs = new CryptoStream(inputFile, rijndael.CreateDecryptor(), CryptoStreamMode.Read))
                {
                    int passLen = cs.ReadByte();
                    cs.Read(fileData, 0, passLen);

                    for (ulong i = 0; i < numSectors; i += 1024)
                    {
                        taskList.Clear();

                        if (cancelPending)
                            return false;

                        readed = cs.Read(fileData, 0, fileData.Length);

                        for (int x = 0; x < deviceHandles.Length; x++)
                        {
                            int index = x;
                            taskList.Add(Task.Run(() =>
                            {
                                NativeDiskWrapper.ReadSectorDataFromHandle(deviceHandles[index], deviceData[index], i, (numSectors - i >= 1024) ? 1024 : (numSectors - i), sectorSize);

                                if (!NativeDiskWrapper.ByteArrayCompare(fileData, deviceData[index]))
                                {
                                    failedDeviceIndex = index;
                                    return false;
                                }
                                else
                                {
                                    return true;
                                }
                            }));
                        }

                        await Task.WhenAll(taskList);

                        foreach (var task in taskList)
                        {
                            if (!task.Result)
                            {
                                for (ulong x = 0; x < 1024 * sectorSize; x++)
                                {
                                    if (deviceData[failedDeviceIndex][x] != fileData[x])
                                    {
                                        throw new Exception(string.Format("Verify found different data. Device {0}:\\ at byte {1:n0}, file data: 0x{2:X2}, device data: 0x{3:X2}", DriveLetters[failedDeviceIndex], i * sectorSize + x, deviceData[failedDeviceIndex][x], fileData[x]));
                                    }
                                }
                                return false;
                            }
                        }

                        totalBytesVerified += (ulong)readed;
                        bytesVerified += (ulong)readed;
                        bytesVerifiedPerPercent += (ulong)readed;
                        bytesToVerify -= (ulong)readed;

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
                }
            }

            return true;
        }
    }
}
