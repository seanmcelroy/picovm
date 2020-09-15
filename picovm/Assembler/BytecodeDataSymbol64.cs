using System;

namespace picovm.Assembler
{
    public struct BytecodeDataSymbol64 : IBytecodeDataSymbol
    {
        public UInt64 DataSegmentOffset { readonly get; internal set; }
        public ushort Length { get; }
        public bool Constant { get; }

        ValueType IBytecodeDataSymbol.DataSegmentOffset
        {
            get => this.DataSegmentOffset;
            set => this.DataSegmentOffset = (UInt64)value;
        }

        public BytecodeDataSymbol64(UInt64 dataSegmentOffset, ushort length, bool constant)
        {
            this.DataSegmentOffset = dataSegmentOffset;
            this.Length = length;
            this.Constant = constant;
        }

        public override string ToString() => $"Offset:{DataSegmentOffset}, len={Length}";

        int IComparable<IBytecodeDataSymbol>.CompareTo(IBytecodeDataSymbol? other)
        {
            if (other == null) return 1;
            return this.DataSegmentOffset.CompareTo(other.DataSegmentOffset);
        }

        bool IEquatable<IBytecodeDataSymbol>.Equals(IBytecodeDataSymbol? other)
        {
            if (other == null) return false;
            return this.DataSegmentOffset.Equals(other.DataSegmentOffset)
                && this.Length.Equals(other.Length)
                && this.Constant.Equals(other.Constant);
        }
    }
}