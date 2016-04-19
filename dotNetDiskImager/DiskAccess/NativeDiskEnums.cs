using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace dotNetDiskImager.DiskAccess
{
    enum PARTITION_TYPE : byte
    {
        PARTITION_ENTRY_UNUSED = 0x00, // Entry unused
        PARTITION_FAT_12 = 0x01, // 12-bit FAT entries
        PARTITION_XENIX_1 = 0x02, // Xenix
        PARTITION_XENIX_2 = 0x03, // Xenix
        PARTITION_FAT_16 = 0x04, // 16-bit FAT entries
        PARTITION_EXTENDED = 0x05, // Extended partition entry
        PARTITION_HUGE = 0x06, // Huge partition MS-DOS V4
        PARTITION_IFS = 0x07, // IFS Partition
        PARTITION_OS2BOOTMGR = 0x0A, // OS/2 Boot Manager/OPUS/Coherent swap
        PARTITION_FAT32 = 0x0B, // FAT32
        PARTITION_FAT32_XINT13 = 0x0C, // FAT32 using extended int13 services
        PARTITION_XINT13 = 0x0E, // Win95 partition using extended int13 services
        PARTITION_XINT13_EXTENDED = 0x0F, // Same as type 5 but uses extended int13 services
        PARTITION_PREP = 0x41, // PowerPC Reference Platform (PReP) Boot Partition
        PARTITION_LDM = 0x42, // Logical Disk Manager partition
        PARTITION_UNIX = 0x63, // Unix
        VALID_NTFT = 0xC0, // NTFT uses high order bits
        PARTITION_NTFT = 0x80,  // NTFT partition
        PARTITION_LINUX_SWAP = 0x82, //An ext2/ext3/ext4 swap partition
        PARTITION_LINUX_NATIVE = 0x83 //An ext2/ext3/ext4 native partition
    }

    [Flags]
    enum EFIPartitionAttributes : ulong
    {
        GPT_ATTRIBUTE_PLATFORM_REQUIRED = 0x0000000000000001,
        LegacyBIOSBootable = 0x0000000000000004,
        GPT_BASIC_DATA_ATTRIBUTE_NO_DRIVE_LETTER = 0x8000000000000000,
        GPT_BASIC_DATA_ATTRIBUTE_HIDDEN = 0x4000000000000000,
        GPT_BASIC_DATA_ATTRIBUTE_SHADOW_COPY = 0x2000000000000000,
        GPT_BASIC_DATA_ATTRIBUTE_READ_ONLY = 0x1000000000000000
    }

    enum PARTITION_STYLE : int
    {
        MasterBootRecord = 0,
        GuidPartitionTable = 1,
        Raw = 2
    }

    enum STORAGE_PROPERTY_ID
    {
        StorageDeviceProperty = 0,
        StorageAdapterProperty = 1,
        StorageDeviceIdProperty = 2,
        StorageDeviceUniqueIdProperty = 3,
        StorageDeviceWriteCacheProperty = 4,
        StorageMiniportProperty = 5,
        StorageAccessAlignmentProperty = 6,
        StorageDeviceSeekPenaltyProperty = 7,
        StorageDeviceTrimProperty = 8,
        StorageDeviceWriteAggregationProperty = 9,
        StorageDeviceDeviceTelemetryProperty = 10, // 0xA
        StorageDeviceLBProvisioningProperty = 11, // 0xB
        StorageDevicePowerProperty = 12, // 0xC
        StorageDeviceCopyOffloadProperty = 13, // 0xD
        StorageDeviceResiliencyProperty = 14 // 0xE
    }

    enum STORAGE_QUERY_TYPE
    {
        /// <summary>Instructs the driver to return an appropriate descriptor.</summary>
        PropertyStandardQuery = 0,
        /// <summary>Instructs the driver to report whether the descriptor is supported.</summary>
        PropertyExistsQuery = 1,
        /// <summary>Used to retrieve a mask of writeable fields in the descriptor. Not currently supported. Do not use.</summary>
        PropertyMaskQuery = 2,
        /// <summary>Specifies the upper limit of the list of query types. This is used to validate the query type.</summary>
        PropertyQueryMaxDefined = 3
    }

    enum MEDIA_TYPE : uint
    {
        Unknown,
        F5_1Pt2_512,
        F3_1Pt44_512,
        F3_2Pt88_512,
        F3_20Pt8_512,
        F3_720_512,
        F5_360_512,
        F5_320_512,
        F5_320_1024,
        F5_180_512,
        F5_160_512,
        RemovableMedia,
        FixedMedia,
        F3_120M_512,
        F3_640_512,
        F5_640_512,
        F5_720_512,
        F3_1Pt2_512,
        F3_1Pt23_1024,
        F5_1Pt23_1024,
        F3_128Mb_512,
        F3_230Mb_512,
        F8_256_128,
        F3_200Mb_512,
        F3_240M_512,
        F3_32M_512
    }

    enum DetectionType : uint
    {
        /// <summary>
        /// Indicates that the disk contains neither an INT 13h partition nor an extended INT 13h partition.
        /// </summary>
        DetectNone = 0,

        /// <summary>
        /// Indicates that the disk has a standard INT 13h partition.
        /// </summary>
        DetectInt13 = 1,

        /// <summary>
        /// Indicates that the disk contains an extended INT 13h partition.
        /// </summary>
        DetectExInt13 = 2
    }

    enum EMoveMethod : uint
    {
        Begin = 0,
        Current = 1,
        End = 2
    }

    enum DriveType : uint
    {
        /// <summary>The drive type cannot be determined.</summary>
        Unknown = 0,
        /// <summary>The root path is invalid, for example, no volume is mounted at the path.</summary>
        Error = 1,
        /// <summary>The drive is a type that has removable media, for example, a floppy drive or removable hard disk.</summary>
        Removable = 2,
        /// <summary>The drive is a type that cannot be removed, for example, a fixed hard drive.</summary>
        Fixed = 3,
        /// <summary>The drive is a remote (network) drive.</summary>
        Remote = 4,
        /// <summary>The drive is a CD-ROM drive.</summary>
        CDROM = 5,
        /// <summary>The drive is a RAM disk.</summary>
        RAMDisk = 6
    }

    [Flags]
    enum FileSystemFeature : uint
    {
        /// <summary>
        /// The file system supports case-sensitive file names.
        /// </summary>
        CaseSensitiveSearch = 1,
        /// <summary>
        /// The file system preserves the case of file names when it places a name on disk.
        /// </summary>
        CasePreservedNames = 2,
        /// <summary>
        /// The file system supports Unicode in file names as they appear on disk.
        /// </summary>
        UnicodeOnDisk = 4,
        /// <summary>
        /// The file system preserves and enforces access control lists (ACL).
        /// </summary>
        PersistentACLS = 8,
        /// <summary>
        /// The file system supports file-based compression.
        /// </summary>
        FileCompression = 0x10,
        /// <summary>
        /// The file system supports disk quotas.
        /// </summary>
        VolumeQuotas = 0x20,
        /// <summary>
        /// The file system supports sparse files.
        /// </summary>
        SupportsSparseFiles = 0x40,
        /// <summary>
        /// The file system supports re-parse points.
        /// </summary>
        SupportsReparsePoints = 0x80,
        /// <summary>
        /// The specified volume is a compressed volume, for example, a DoubleSpace volume.
        /// </summary>
        VolumeIsCompressed = 0x8000,
        /// <summary>
        /// The file system supports object identifiers.
        /// </summary>
        SupportsObjectIDs = 0x10000,
        /// <summary>
        /// The file system supports the Encrypted File System (EFS).
        /// </summary>
        SupportsEncryption = 0x20000,
        /// <summary>
        /// The file system supports named streams.
        /// </summary>
        NamedStreams = 0x40000,
        /// <summary>
        /// The specified volume is read-only.
        /// </summary>
        ReadOnlyVolume = 0x80000,
        /// <summary>
        /// The volume supports a single sequential write.
        /// </summary>
        SequentialWriteOnce = 0x100000,
        /// <summary>
        /// The volume supports transactions.
        /// </summary>
        SupportsTransactions = 0x200000,
    }
}
