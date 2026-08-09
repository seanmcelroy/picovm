using System;
using System.Collections.Generic;

namespace picovm.Assembler
{
    public struct BytecodeDataSymbol<TAddrSize>(TAddrSize dataSegmentOffset, ushort length, bool constant)
        : IComparable<BytecodeDataSymbol<TAddrSize>>
        where TAddrSize : struct, IComparable, IComparable<TAddrSize>, IConvertible, IEquatable<TAddrSize>, IFormattable 
    {
        public TAddrSize DataSegmentOffset { readonly get; internal set; } = dataSegmentOffset;
        public ushort Length { get; } = length;
        public bool Constant { get; } = constant;

        public override readonly string ToString() => $"Offset:{DataSegmentOffset}, len={Length}";

        public readonly int CompareTo(BytecodeDataSymbol<TAddrSize> other) => DataSegmentOffset.CompareTo(other.DataSegmentOffset);

        public readonly bool Equals(BytecodeDataSymbol<TAddrSize> other) => DataSegmentOffset.Equals(other.DataSegmentOffset)
            && Length.Equals(other.Length)
            && Constant.Equals(other.Constant);
    }
}