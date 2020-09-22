using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

namespace picovm.Packager.PE
{
    public struct PEHeader // aka COFF header
    {
        // Magic number, always PE\0\0
        public static readonly ImmutableArray<byte> MAGIC = ImmutableArray<byte>.Empty.AddRange(new byte[] { 0x50, 0x45, 0x00, 0x00 });

        public UInt16 mMachine;
        public UInt16 mNumberOfSections;
        public UInt32 mTimeDateStamp;
        public UInt32 mPointerToSymbolTable;
        public UInt32 mNumberOfSymbols;
        public UInt16 mSizeOfOptionalHeader;
        public UInt16 mCharacteristics;

        public static bool TryRead(Stream stream, out PEHeader header)
        {
            try
            {
                header = new PEHeader();
                header.Read(stream);
                return true;
            }
            catch (Exception ex)
            {
                header = default(PEHeader);
                Console.Error.WriteLine(ex);
                return false;
            }
        }

        public void Read(Stream stream)
        {
            var magic = new byte[MAGIC.Length];
            stream.Read(magic);
            if (!MAGIC.SequenceEqual(magic))
                throw new BadImageFormatException("Magic value is not present for a PE file");

            mMachine = stream.ReadUInt16();
            mNumberOfSections = stream.ReadUInt16();
            mTimeDateStamp = stream.ReadUInt32();
            mPointerToSymbolTable = stream.ReadUInt32();
            mNumberOfSymbols = stream.ReadUInt32();
            mSizeOfOptionalHeader = stream.ReadUInt16();
            mCharacteristics = stream.ReadUInt16();
        }
    }
}