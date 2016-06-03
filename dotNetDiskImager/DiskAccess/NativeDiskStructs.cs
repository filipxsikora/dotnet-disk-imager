using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace dotNetDiskImager.DiskAccess
{
    [StructLayout(LayoutKind.Explicit)]
    internal struct PARTITION_INFORMATION_UNION
    {
        [FieldOffset(0)]
        public PARTITION_INFORMATION_GPT Gpt;
        [FieldOffset(0)]
        public PARTITION_INFORMATION_MBR Mbr;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PARTITION_INFORMATION_MBR
    {
        public byte PartitionType;
        [MarshalAs(UnmanagedType.U1)]
        public bool BootIndicator;
        [MarshalAs(UnmanagedType.U1)]
        public bool RecognizedPartition;
        public uint HiddenSectors;
    }

    [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode)]
    internal struct PARTITION_INFORMATION_GPT
    {
        // Strangely, this works as sequential if you build x86,
        // But for x64 you must use explicit.
        [FieldOffset(0)]
        internal Guid PartitionType;
        [FieldOffset(16)]
        internal Guid PartitionId;
        [FieldOffset(32)]
        //DWord64
        internal ulong Attributes;
        [FieldOffset(40)]
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 36)]
        internal string Name;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct PARTITION_INFORMATION_EX
    {
        [FieldOffset(0)]
        internal PARTITION_STYLE PartitionStyle;
        [FieldOffset(8)]
        internal long StartingOffset;
        [FieldOffset(16)]
        internal long PartitionLength;
        [FieldOffset(24)]
        internal int PartitionNumber;
        [FieldOffset(28)]
        [MarshalAs(UnmanagedType.U1)]
        internal bool RewritePartition;
        [FieldOffset(32)]
        internal PARTITION_INFORMATION_MBR Mbr;
        [FieldOffset(32)]
        internal PARTITION_INFORMATION_GPT Gpt;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DEV_BROADCAST_VOLUME
    {
        public uint dbch_Size;
        public uint dbch_Devicetype;
        public uint dbch_Reserved;
        public uint dbch_Unitmask;
        public ushort dbch_Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DEV_BROADCAST_HDR
    {
        public uint dbch_Size;
        public uint dbch_DeviceType;
        public uint dbch_Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct STORAGE_PROPERTY_QUERY
    {
        internal STORAGE_PROPERTY_ID PropertyId;
        internal STORAGE_QUERY_TYPE QueryType;
        internal byte[] AdditionalParameters;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct STORAGE_DEVICE_NUMBER
    {
        internal int DeviceType;
        internal int DeviceNumber;
        internal int PartitionNumber;
    }

    enum STORAGE_BUS_TYPE
    {
        BusTypeUnknown = 0x00,
        BusTypeScsi = 0x1,
        BusTypeAtapi = 0x2,
        BusTypeAta = 0x3,
        BusType1394 = 0x4,
        BusTypeSsa = 0x5,
        BusTypeFibre = 0x6,
        BusTypeUsb = 0x7,
        BusTypeRAID = 0x8,
        BusTypeiScsi = 0x9,
        BusTypeSas = 0xA,
        BusTypeSata = 0xB,
        BusTypeSd = 0xC,
        BusTypeMmc = 0xD,
        BusTypeVirtual = 0xE,
        BusTypeFileBackedVirtual = 0xF,
        BusTypeMax = 0x10,
        BusTypeMaxReserved = 0x7F
    }

    [StructLayout(LayoutKind.Sequential)]
    struct STORAGE_DEVICE_DESCRIPTOR
    {
        internal uint Version;
        internal uint Size;
        internal byte DeviceType;
        internal byte DeviceTypeModifier;
        [MarshalAs(UnmanagedType.U1)]
        internal bool RemovableMedia;
        [MarshalAs(UnmanagedType.U1)]
        internal bool CommandQueueing;
        internal uint VendorIdOffset;
        internal uint ProductIdOffset;
        internal uint ProductRevisionOffset;
        internal uint SerialNumberOffset;
        internal STORAGE_BUS_TYPE BusType;
        internal uint RawPropertiesLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x16)]
        internal byte[] RawDeviceProperties;
    }

    [StructLayout(LayoutKind.Explicit, Size = 8)]
    struct LARGE_INTEGER
    {
        [FieldOffset(0)]
        internal long QuadPart;
        [FieldOffset(0)]
        internal int LowPart;
        [FieldOffset(4)]
        internal int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DISK_GEOMETRY
    {
        internal long Cylinders;
        internal MEDIA_TYPE MediaType;
        internal int TracksPerCylinder;
        internal int SectorsPerTrack;
        internal int BytesPerSector;

        internal long DiskSize
        {
            get
            {
                return Cylinders * TracksPerCylinder * SectorsPerTrack * BytesPerSector;
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DISK_GEOMETRY_EX
    {
        internal DISK_GEOMETRY Geometry;
        internal long DiskSize;
        internal DISK_PARTITION_INFO PartitionInformation;
        internal DISK_DETECTION_INFO DiskDetectionInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DISK_DETECTION_INFO
    {
        /// <summary>
        /// Contains the quantity, in bytes, of retrieved detect information.
        /// </summary>
        internal ulong SizeOfDetectInfo;

        /// <summary>
        /// <see cref="DetectionType"/> for member explanation
        /// </summary>
        internal DetectionType DetectionType;

        /// <summary>
        /// Union of <see cref="DISK_INT13_INFO"/> and <see cref="DISK_EX_INT13_INFO"/> structs
        /// If DetectionType == DetectInt13 then it contains structure DISK_INT13_INFO
        /// if DetectionType == DetectExInt13 then it contains structure DISK_EX_INT13_INFO
        /// </summary>
        internal DiskInt13Union DiskInt13Union;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DISK_EX_INT13_INFO
    {
        /// <summary>
        /// Indicates the size of the buffer that the caller provides to the BIOS in which to return the requested drive data. 
        /// ExBufferSize must be 26 or greater. If ExBufferSize is less than 26, the BIOS returns an error . If ExBufferSize is between 30 
        /// and 66, the BIOS sets it to exactly 30 on exit. If ExBufferSize is 66 or greater, the BIOS sets it to exactly 66 on exit.
        /// </summary>
        internal ushort ExBufferSize;

        /// <summary>
        /// Provides information about the drive. The following table describes the significance of each bit, where bit 0 is the least significant bit and bit 15 the most significant bit. A value of one in the indicated bit means that the feature described in the "Meaning" column is available. A value of zero in the indicated bit means that the feature is not available with this drive.
        /// </summary>
        internal ushort ExFlags;

        /// <summary>
        /// Indicates the number of physical cylinders. This is one greater than the maximum cylinder number.
        /// </summary>
        internal uint ExCylinders;

        /// <summary>
        /// Indicates the number of physical heads. This is one greater than the maximum head number.
        /// </summary>
        internal uint ExHeads;

        /// <summary>
        /// Indicates the number of physical sectors per track. This number is the same as the maximum sector number.
        /// </summary>
        internal uint ExSectorsPerTrack;

        /// <summary>
        /// Indicates the total count of sectors on the disk. This is one greater than the maximum logical block address.
        /// </summary>
        internal ulong ExSectorsPerDrive;

        /// <summary>
        /// Indicates the sector size in bytes.
        /// </summary>
        internal ushort ExSectorSize;

        /// <summary>
        /// Reserved
        /// </summary>
        internal ushort ExReserved;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct DiskInt13Union
    {
        /// <summary>
        /// <see cref="DISK_INT13_INFO"/> for member description
        /// </summary>
        [FieldOffset(0)]
        internal DISK_INT13_INFO Int13;

        /// <summary>
        /// <see cref="DISK_EX_INT13_INFO"/> for member description
        /// </summary>
        [FieldOffset(0)]
        internal DISK_EX_INT13_INFO ExInt13;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DISK_INT13_INFO
    {
        /// <summary>
        /// Corresponds to the Device/Head register defined in the AT Attachment (ATA) specification. When zero, the fourth bit of this register 
        /// indicates that drive zero is selected. When 1, it indicates that drive one is selected. The values of bits 0, 1, 2, 3, and 6 depend 
        /// on the command in the command register. Bits 5 and 7 are no longer used. For more information about the values that the Device/Head 
        /// register can hold, see the ATA specification.
        /// </summary>
        internal ushort DriveSelect;

        /// <summary>
        /// Indicates the maximum number of cylinders on the disk.
        /// </summary>
        internal ulong MaxCylinders;

        /// <summary>
        /// Indicates the number of sectors per track.
        /// </summary>
        internal ushort SectorsPerTrack;

        /// <summary>
        /// Indicates the maximum number of disk heads.
        /// </summary>
        internal ushort MaxHeads;

        /// <summary>
        /// Indicates the number of drives.
        /// </summary>
        internal ushort NumberDrives;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct DISK_PARTITION_INFO
    {
        /// <summary>
        /// Size of this structure in bytes. Set to sizeof(DISK_PARTITION_INFO).
        /// </summary>
        [FieldOffset(0)]
        internal int SizeOfPartitionInfo;

        /// <summary>
        /// Takes a <see cref="PARTITION_STYLE"/> enumerated value that specifies the type of partition table the disk contains.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields", Justification = "Struct declaration")]
        [FieldOffset(4)]
        internal PARTITION_STYLE PartitionStyle;

        /// <summary>
        /// If PartitionStyle == MBR
        /// this value could be Signature or CheckSum
        /// 
        /// Signature
        /// Specifies the signature value, which uniquely identifies the disk. The Mbr member of the union is used to specify the disk signature 
        /// data for a disk formatted with a Master Boot Record (MBR) format partition table. Any other value indicates that the partition is 
        /// not a boot partition. This member is valid when PartitionStyle is PARTITION_STYLE_MBR.
        /// 
        /// CheckSum
        /// Specifies the checksum for the master boot record. The Mbr member of the union is used to specify the disk signature data for a disk 
        /// formatted with a Master Boot Record (MBR) format partition table. This member is valid when PartitionStyle is PARTITION_STYLE_MBR.
        /// 
        /// 
        /// if PartitionStyle == GPT
        /// 
        /// DiskId
        /// Specifies the GUID that uniquely identifies the disk. The Gpt member of the union is used to specify the disk signature data for 
        /// a disk that is formatted with a GUID Partition Table (GPT) format partition table. This member is valid when PartitionStyle is 
        /// PARTITION_STYLE_GPT. The GUID data type is described on the Using GUIDs in Drivers reference page.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields", Justification = "Struct declaration")]
        [FieldOffset(8)]
        internal uint MbrSignature;

        /// <summary>
        /// if PartitionStyle == GPT
        /// 
        /// DiskId
        /// Specifies the GUID that uniquely identifies the disk. The Gpt member of the union is used to specify the disk signature data for 
        /// a disk that is formatted with a GUID Partition Table (GPT) format partition table. This member is valid when PartitionStyle is 
        /// PARTITION_STYLE_GPT. The GUID data type is described on the Using GUIDs in Drivers reference page.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields", Justification = "Struct declaration")]
        [FieldOffset(8)]
        internal Guid DiskId;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct DRIVE_LAYOUT_INFORMATION_EX
    {
        [FieldOffset(0)]
        internal PARTITION_STYLE PartitionStyle;
        [FieldOffset(4)]
        internal int PartitionCount;
        [FieldOffset(8)]
        internal DRIVE_LAYOUT_INFORMATION_MBR Mbr;
        [FieldOffset(8)]
        internal DRIVE_LAYOUT_INFORMATION_GPT Gpt;
        // Forget partition entry, we can't marshal it directly
        // as we don't know how big it is.
        [FieldOffset(48)]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        internal PARTITION_INFORMATION_EX[] PartitionEntry;
    }

    internal struct DRIVE_LAYOUT_INFORMATION_MBR
    {
#pragma warning disable 649
        internal uint Signature;
#pragma warning restore
    }

    // Sequential ensures the fields are laid out in memory
    // in the same order as we write them here. Without it,
    // the runtime can arrange them however it likes, and the
    // type may no longer be blittable to the C type.
    [StructLayout(LayoutKind.Sequential)]
    internal struct DRIVE_LAYOUT_INFORMATION_GPT
    {
        internal Guid DiskId;
        // C LARGE_INTEGER is 64 bit
        internal long StartingUsableOffset;
        internal long UsableLength;
        // C ULONG is 32 bit
        internal uint MaxPartitionCount;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct CREATE_DISK
    {
        [FieldOffset(0)]
        internal PARTITION_STYLE PartitionStyle;
        [FieldOffset(4)]
        internal CREATE_DISK_MBR Mbr;
        [FieldOffset(4)]
        internal CREATE_DISK_GPT Gpt;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CREATE_DISK_GPT
    {
        internal Guid DiskId;
        internal uint MaxPartitionCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CREATE_DISK_MBR
    {
        internal uint Signature;
    }

}
