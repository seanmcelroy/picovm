using System;

namespace picovm.Packager.PE
{
    public readonly struct PEDataDictionaryEntry(UInt32 rva, UInt32 size)
    {
        public readonly UInt32 RelativeVirtualAddress = rva;
        public readonly UInt32 Size = size;

        public override bool Equals(object? obj)
        {
            if (obj is not PEDataDictionaryEntry mys)
                return false;

            return
                mys.RelativeVirtualAddress == RelativeVirtualAddress &&
                mys.Size == Size;
        }

        public override int GetHashCode() => HashCode.Combine(RelativeVirtualAddress, Size);
        public override string ToString() => $"RVA={RelativeVirtualAddress}, RVAx=0x{RelativeVirtualAddress:x}, SZ={Size}";
    }
}
