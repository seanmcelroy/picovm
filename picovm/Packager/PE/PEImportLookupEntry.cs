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
            this.OrdinalNameFlag = (value & (UInt32)0x80000000) == 0x80000000;
            this.OrdinalNumber = this.OrdinalNameFlag ? (UInt16)(value & (UInt32)0xFFFF) : (UInt16)0;
            this.HintTableNameRva = !this.OrdinalNameFlag ? (value & (UInt32)0x7FFFFFFF) : 0;

            //this.OrdinalNameFlag = (value & (UInt32)0x1) == 0x1;
            //this.OrdinalNumber = this.OrdinalNameFlag ? (UInt16)(value & (UInt32)0xFFFF0000) : (UInt16)0;
            //this.HintTableNameRva = !this.OrdinalNameFlag ? (value & (UInt32)0x80000000) : 0;
        }

        public PEImportLookupEntry(UInt64 value)
        {
            this.OrdinalNameFlag = (value & (UInt64)0x8000000000000000) == 0x8000000000000000;
            this.OrdinalNumber = this.OrdinalNameFlag ? (UInt16)(value & (UInt64)0xFFFF) : (UInt16)0;
            this.HintTableNameRva = !this.OrdinalNameFlag ? (UInt32)(value & (UInt64)0x7FFFFFFF) : 0;
        }

        public override bool Equals(object? obj)
        {
            if (!(obj is PEImportLookupEntry))
                return false;

            var mys = (PEImportLookupEntry)obj;
            return
                mys.OrdinalNameFlag == this.OrdinalNameFlag &&
                mys.OrdinalNumber == this.OrdinalNumber &&
                mys.HintTableNameRva == this.HintTableNameRva;
        }

        public override int GetHashCode() => HashCode.Combine(OrdinalNameFlag, OrdinalNumber, HintTableNameRva);
        public override string ToString() => this.OrdinalNameFlag ? $"Ordinal={this.OrdinalNumber}" : $"NameRVA={this.HintTableNameRva}";
    }
}