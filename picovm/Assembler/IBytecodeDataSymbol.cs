using System;

namespace picovm.Assembler
{
    public interface IBytecodeDataSymbol : IComparable<IBytecodeDataSymbol>, IEquatable<IBytecodeDataSymbol>
    {
        ValueType DataSegmentOffset { get; set; }
        ushort Length { get; }
        bool Constant { get; }
    }
}