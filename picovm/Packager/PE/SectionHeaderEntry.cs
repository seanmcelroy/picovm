using System;
using System.IO;
using System.Linq;

namespace picovm.Packager.PE
{
    public readonly struct SectionHeaderEntry(Stream stream)
    {
        public readonly UInt64 Name = stream.ReadUInt64();
        public readonly UInt32 VirtualSize = stream.ReadUInt32();
        public readonly UInt32 VirtualAddress = stream.ReadUInt32();
        public readonly UInt32 SizeOfRawData = stream.ReadUInt32();
        public readonly UInt32 PointerToRawData = stream.ReadUInt32();
        public readonly UInt32 PointerToRelocations = stream.ReadUInt32();
        public readonly UInt32 PointerToLineNumbers = stream.ReadUInt32();
        public readonly UInt16 NumberOfRelocations = stream.ReadUInt16();
        public readonly UInt16 NumberOfLineNumbers = stream.ReadUInt16();
        public readonly UInt32 Characteristics = stream.ReadUInt32();

        public string NameAsString() => System.Text.Encoding.ASCII.GetString(BitConverter.GetBytes(Name).TakeWhile(b => b != 0x00).ToArray());

        public override bool Equals(object? obj)
        {
            if (obj is not SectionHeaderEntry mys)
                return false;

            return
                mys.Name == Name &&
                mys.VirtualSize == VirtualSize &&
                mys.VirtualAddress == VirtualAddress &&
                mys.SizeOfRawData == SizeOfRawData &&
                mys.PointerToRawData == PointerToRawData;
        }

        public override int GetHashCode() => HashCode.Combine(Name, VirtualSize, VirtualAddress, SizeOfRawData, PointerToRawData);
        public override string ToString() => $"Name={NameAsString()}, Addr=0x{VirtualAddress:x}";
    }
}