using System;

namespace picovm.Packager.PE
{
    public readonly struct PEDataDictionaryEntry
    {
        public readonly UInt32 RelativeVirtualAddress;
        public readonly UInt32 Size;

        public PEDataDictionaryEntry(UInt32 rva, UInt32 size)
        {
            this.RelativeVirtualAddress = rva;
            this.Size = size;
        }

        public override bool Equals(object? obj)
        {
            if (!(obj is PEDataDictionaryEntry))
                return false;

            var mys = (PEDataDictionaryEntry)obj;
            return
                mys.RelativeVirtualAddress == this.RelativeVirtualAddress &&
                mys.Size == this.Size;
        }

        public override int GetHashCode() => HashCode.Combine(RelativeVirtualAddress, Size);
        public override string ToString() => $"RVA={this.RelativeVirtualAddress}, RVAx=0x{this.RelativeVirtualAddress:x}, SZ={this.Size}";
    }
}
