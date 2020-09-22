using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

namespace picovm.Packager.PE
{
    public struct PEHeaderOption64
    {
        // Magic number for optional 64-bit PE sub-header
        public static readonly ImmutableArray<byte> MAGIC = ImmutableArray<byte>.Empty.AddRange(new byte[] { 0x0b, 0x02 });

        public byte mMajorLinkerVersion;
        public byte mMinorLinkerVersion;
        public UInt32 mSizeOfCode;
        public UInt32 mSizeOfInitializedData;
        public UInt32 mSizeOfUninitializedData;
        public UInt32 mAddressOfEntryPoint;
        public UInt32 mBaseOfCode;
        public UInt64 mImageBase;
        public UInt32 mSectionAlignment;
        public UInt32 mFileAlignment;
        public UInt16 mMajorOperatingSystemVersion;
        public UInt16 mMinorOperatingSystemVersion;
        public UInt16 mMajorImageVersion;
        public UInt16 mMinorImageVersion;
        public UInt16 mMajorSubsystemVersion;
        public UInt16 mMinorSubsystemVersion;
        public UInt32 mWin32VersionValue;
        public UInt32 mSizeOfImage;
        public UInt32 mSizeOfHeaders;
        public UInt32 mCheckSum;
        public UInt16 mSubsystem;
        public UInt16 mDllCharacteristics;
        public UInt32 mSizeOfStackReserve;
        public UInt32 mSizeOfStackCommit;
        public UInt32 mSizeOfHeapReserve;
        public UInt32 mSizeOfHeapCommit;
        public UInt32 mLoaderFlags;
        public UInt32 mNumberOfRvaAndSizes;

        public static bool TryRead(Stream stream, out PEHeaderOption64 header)
        {
            try
            {
                header = new PEHeaderOption64();
                header.Read(stream);
                return true;
            }
            catch (Exception ex)
            {
                header = default(PEHeaderOption64);
                Console.Error.WriteLine(ex);
                return false;
            }
        }

        public void Read(Stream stream)
        {
            var magic = new byte[MAGIC.Length];
            stream.Read(magic);
            if (!MAGIC.SequenceEqual(magic))
                throw new BadImageFormatException($"Magic value ({magic.ToByteString()}) is not present for a PE64 header");

            mMajorLinkerVersion = (byte)stream.ReadByte();
            mMinorLinkerVersion = (byte)stream.ReadByte();
            mSizeOfCode = stream.ReadUInt32();
            mSizeOfInitializedData = stream.ReadUInt32();
            mSizeOfUninitializedData = stream.ReadUInt32();
            mAddressOfEntryPoint = stream.ReadUInt32();
            mBaseOfCode = stream.ReadUInt32();
            mImageBase = stream.ReadUInt64();
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