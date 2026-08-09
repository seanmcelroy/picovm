using System;
using System.Collections.Generic;

namespace picovm.Assembler
{
    public readonly struct BytecodeTextSymbol<TAddrSize>(string name, TAddrSize textSegmentInstructionOffset, TAddrSize textSegmentReferenceOffset, byte referenceLength)
        : IComparable<BytecodeTextSymbol<TAddrSize>>
        where TAddrSize : struct, IComparable, IComparable<TAddrSize>, IConvertible, IEquatable<TAddrSize>, IFormattable 
    {
        public string Name { get; } = name;
        // The offset for the beginning of the instruction.  Useful if we need to change the opcode
        public TAddrSize TextSegmentInstructionOffset { get; } = textSegmentInstructionOffset;
        // The offset for the actual symbol.  At this address, should be 0xFF as a placeholder until the symbol is resolved through substitution
        public TAddrSize TextSegmentReferenceOffset { get; } = textSegmentReferenceOffset;
        public byte ReferenceLength { get; } = referenceLength;

        public override string ToString() => $"Symbol:{Name} refOffset:{TextSegmentReferenceOffset}, len={ReferenceLength}";

        public int CompareTo(BytecodeTextSymbol<TAddrSize> other) => Name?.CompareTo(other.Name) ?? 1;

        public bool Equals(BytecodeTextSymbol<TAddrSize> other) => Name.Equals(other.Name)
                && TextSegmentInstructionOffset.Equals(other.TextSegmentInstructionOffset)
                && TextSegmentReferenceOffset.Equals(other.TextSegmentReferenceOffset)
                && ReferenceLength.Equals(other.ReferenceLength);
    }
}