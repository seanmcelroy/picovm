using System;
using System.ComponentModel;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

namespace picovm.Packager.PE
{
    public struct PEHeaderOption32
    {
        // Magic number for optional 32-bit PE sub-header
        public static readonly ImmutableArray<byte> MAGIC = ImmutableArray<byte>.Empty.AddRange(new byte[] { 0x0b, 0x01 });

        [Description("The major version number of the linker.")]
        public byte mMajorLinkerVersion;

        [Description("The minor version number of the linker.")]
        public byte mMinorLinkerVersion;

        [Description("The size of the code section, in bytes, or the sum of all such sections if there are multiple code sections.")]
        public UInt32 mSizeOfCode;

        [Description("The size of the initialized data section, in bytes, or the sum of all such sections if there are multiple initialized data sections.")]
        public UInt32 mSizeOfInitializedData;

        [Description("The size of the uninitialized data section, in bytes, or the sum of all such sections if there are multiple uninitialized data sections.")]
        public UInt32 mSizeOfUninitializedData;

        [Description("A pointer to the entry point function, relative to the image base address. For executable files, this is the starting address. For device drivers, this is the address of the initialization function. The entry point function is optional for DLLs. When no entry point is present, this member is zero.")]
        public UInt32 mAddressOfEntryPoint;

        [Description("A pointer to the beginning of the code section, relative to the image base.")]
        public UInt32 mBaseOfCode;

        [Description("A pointer to the beginning of the data section, relative to the image base.")]
        public UInt32 mBaseOfData;

        [Description("The preferred address of the first byte of the image when it is loaded in memory. This value is a multiple of 64K bytes. The default value for DLLs is 0x10000000. The default value for applications is 0x00400000, except on Windows CE where it is 0x00010000.")]
        public UInt32 mImageBase;

        [Description("The alignment of sections loaded in memory, in bytes. This value must be greater than or equal to the FileAlignment member. The default value is the page size for the system.")]

        public UInt32 mSectionAlignment;

        [Description("The alignment of the raw data of sections in the image file, in bytes. The value should be a power of 2 between 512 and 64K (inclusive). The default is 512. If the SectionAlignment member is less than the system page size, this member must be the same as SectionAlignment.")]
        public UInt32 mFileAlignment;

        [Description("The major version number of the required operating system.")]

        public UInt16 mMajorOperatingSystemVersion;

        [Description("The minor version number of the required operating system.")]
        public UInt16 mMinorOperatingSystemVersion;

        [Description("The major version number of the image.")]
        public UInt16 mMajorImageVersion;

        [Description("The minor version number of the image.")]
        public UInt16 mMinorImageVersion;

        [Description("The major version number of the subsystem.")]
        public UInt16 mMajorSubsystemVersion;

        [Description("The minor version number of the subsystem.")]
        public UInt16 mMinorSubsystemVersion;

        [Description("This member is reserved and must be 0.")]
        public UInt32 mWin32VersionValue;

        [Description("The size of the image, in bytes, including all headers. Must be a multiple of SectionAlignment.")]
        public UInt32 mSizeOfImage;

        [Description("The combined size of the following items, rounded to a multiple of the value specified in the FileAlignment member.")]

        public UInt32 mSizeOfHeaders;
        [Description("The image file checksum. The following files are validated at load time: all drivers, any DLL loaded at boot time, and any DLL loaded into a critical system process.")]
        public UInt32 mCheckSum;
        [Description("The subsystem required to run this image.")]
        public UInt16 mSubsystem;
        [Description("The DLL characteristics of the image.")]
        public UInt16 mDllCharacteristics;
        [Description("The number of bytes to reserve for the stack. Only the memory specified by the SizeOfStackCommit member is committed at load time; the rest is made available one page at a time until this reserve size is reached.")]
        public UInt32 mSizeOfStackReserve;
        [Description("The number of bytes to commit for the stack.")]
        public UInt32 mSizeOfStackCommit;
        [Description("The number of bytes to reserve for the local heap. Only the memory specified by the SizeOfHeapCommit member is committed at load time; the rest is made available one page at a time until this reserve size is reached.")]
        public UInt32 mSizeOfHeapReserve;
        [Description("The number of bytes to commit for the local heap.")]
        public UInt32 mSizeOfHeapCommit;
        [Description("This member is obsolete.")]
        public UInt32 mLoaderFlags;
        [Description("The number of directory entries in the remainder of the optional header.")]
        public UInt32 mNumberOfRvaAndSizes;

        public static bool TryRead(Stream stream, out PEHeaderOption32 header)
        {
            try
            {
                header = new PEHeaderOption32();
                header.Read(stream);
                return true;
            }
            catch (Exception ex)
            {
                header = default(PEHeaderOption32);
                Console.Error.WriteLine(ex);
                return false;
            }
        }

        public void Read(Stream stream)
        {
            var magic = new byte[MAGIC.Length];
            stream.Read(magic);
            if (!MAGIC.SequenceEqual(magic))
                throw new BadImageFormatException($"Magic value ({magic.ToByteString()}) is not present for a PE32 header");

            mMajorLinkerVersion = (byte)stream.ReadByte();
            mMinorLinkerVersion = (byte)stream.ReadByte();
            mSizeOfCode = stream.ReadUInt32();
            mSizeOfInitializedData = stream.ReadUInt32();
            mSizeOfUninitializedData = stream.ReadUInt32();
            mAddressOfEntryPoint = stream.ReadUInt32();
            mBaseOfCode = stream.ReadUInt32();
            mBaseOfData = stream.ReadUInt32();
            mImageBase = stream.ReadUInt32();
            mSectionAlignment = stream.ReadUInt32();
            mFileAlignment = stream.ReadUInt32();
            mMajorOperatingSystemVersion = stream.ReadUInt16();
            mMinorOperatingSystemVersion = stream.ReadUInt16();
            mMajorImageVersion = stream.ReadUInt16();
            mMinorImageVersion = stream.ReadUInt16();
            mMajorSubsystemVersion = stream.ReadUInt16();
            mMinorSubsystemVersion = stream.ReadUInt16();
            mWin32VersionValue = stream.ReadUInt32();
            mSizeOfImage = stream.ReadUInt32();
            mSizeOfHeaders = stream.ReadUInt32();
            mCheckSum = stream.ReadUInt32();
            mSubsystem = stream.ReadUInt16();
            mDllCharacteristics = stream.ReadUInt16();
            mSizeOfStackReserve = stream.ReadUInt32();
            mSizeOfStackCommit = stream.ReadUInt32();
            mSizeOfHeapReserve = stream.ReadUInt32();
            mSizeOfHeapCommit = stream.ReadUInt32();
            mLoaderFlags = stream.ReadUInt32();
            mNumberOfRvaAndSizes = stream.ReadUInt32();
        }
    }
}