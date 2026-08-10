using System;

namespace picovm.Packager.PE
{
    public readonly struct PEImportLookupEntry
    {
        public readonly bool OrdinalNameFlag;
        public readonly UInt16 OrdinalNumber;
        public readonly UInt32 HintTableNameRva;

        public PEImportLookupEntry(UInt32 value)
        {
            OrdinalNameFlag = (value & 0x80000000u) == 0x80000000;
            OrdinalNumber = OrdinalNameFlag ? (UInt16)(value & 0xFFFFu) : (UInt16)0;
            HintTableNameRva = !OrdinalNameFlag ? (value & 0x7FFFFFFFu) : 0;
        }

        public PEImportLookupEntry(UInt64 value)
        {
            OrdinalNameFlag = (value & 0x8000000000000000ul) == 0x8000000000000000;
            OrdinalNumber = OrdinalNameFlag ? (UInt16)(value & 0xFFFFul) : (UInt16)0;
            HintTableNameRva = !OrdinalNameFlag ? (UInt32)(value & 0x7FFFFFFFul) : 0;
        }

        public override bool Equals(object? obj)
        {
            if (!(obj is PEImportLookupEntry))
                return false;

            var mys = (PEImportLookupEntry)obj;
            return
                mys.OrdinalNameFlag == OrdinalNameFlag &&
                mys.OrdinalNumber == OrdinalNumber &&
                mys.HintTableNameRva == HintTableNameRva;
        }

        public override int GetHashCode() => HashCode.Combine(OrdinalNameFlag, OrdinalNumber, HintTableNameRva);
        public override string ToString() => OrdinalNameFlag ? $"Ordinal={OrdinalNumber}" : $"NameRVA={HintTableNameRva}";
    }
}