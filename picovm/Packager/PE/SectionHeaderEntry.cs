using System;
using System.IO;
using System.Linq;

namespace picovm.Packager.PE
{
    public readonly struct SectionHeaderEntry
    {
        public readonly UInt64 Name;
        public readonly UInt32 VirtualSize;
        public readonly UInt32 VirtualAddress;
        public readonly UInt32 SizeOfRawData;
        public readonly UInt32 PointerToRawData;
        public readonly UInt32 PointerToRelocations;
        public readonly UInt32 PointerToLineNumbers;
        public readonly UInt16 NumberOfRelocations;
        public readonly UInt16 NumberOfLineNumbers;
        public readonly UInt32 Characteristics;

        public SectionHeaderEntry(Stream stream)
        {
            this.Name = stream.ReadUInt64();
            this.VirtualSize = stream.ReadUInt32();
            this.VirtualAddress = stream.ReadUInt32();
            this.SizeOfRawData = stream.ReadUInt32();
            this.PointerToRawData = stream.ReadUInt32();
            this.PointerToRelocations = stream.ReadUInt32();
            this.PointerToLineNumbers = stream.ReadUInt32();
            this.NumberOfRelocations = stream.ReadUInt16();
            this.NumberOfLineNumbers = stream.ReadUInt16();
            this.Characteristics = stream.ReadUInt32();
        }

        public string NameAsString() => System.Text.Encoding.ASCII.GetString(BitConverter.GetBytes(this.Name).TakeWhile(b => b != 0x00).ToArray());

        public override bool Equals(object? obj)
        {
            if (!(obj is SectionHeaderEntry))
                return false;

            var mys = (SectionHeaderEntry)obj;
            return
                mys.Name == this.Name &&
                mys.VirtualSize == this.VirtualSize &&
                mys.VirtualAddress == this.VirtualAddress &&
                mys.SizeOfRawData == this.SizeOfRawData &&
                mys.PointerToRawData == this.PointerToRawData;
        }

        public override int GetHashCode() => HashCode.Combine(Name, VirtualSize, VirtualAddress, SizeOfRawData, PointerToRawData);
        public override string ToString() => $"Name={NameAsString()}, Addr=0x{this.VirtualAddress:x}";
    }
}