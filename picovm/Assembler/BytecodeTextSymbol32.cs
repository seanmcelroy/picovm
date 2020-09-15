using System;

namespace picovm.Assembler
{
    public readonly struct BytecodeTextSymbol32 : IBytecodeTextSymbol
    {
        public string Name { get; }
        // The offset for the beginning of the instruction.  Useful if we need to change the opcode, like MOV_REG_MEM to MOV_REG_CON for a constant
        public UInt32 TextSegmentInstructionOffset { get; }
        // The offset for the actual symbol.  At this address, should be 0xFF as a placeholder until the symbol is resolved through substitution
        public UInt32 TextSegmentReferenceOffset { get; }
        public byte ReferenceLength { get; }

        ValueType IBytecodeTextSymbol.TextSegmentInstructionOffset => this.TextSegmentInstructionOffset;

        ValueType IBytecodeTextSymbol.TextSegmentReferenceOffset => this.TextSegmentReferenceOffset;

        public BytecodeTextSymbol32(string name, UInt32 textSegmentInstructionOffset, UInt32 textSegmentReferenceOffset, byte referenceLength)
        {
            this.Name = name;
            this.TextSegmentInstructionOffset = textSegmentInstructionOffset;
            this.TextSegmentReferenceOffset = textSegmentReferenceOffset;
            this.ReferenceLength = referenceLength;
        }


        public override string ToString() => $"Symbol:{Name} refOffset:{TextSegmentReferenceOffset}, len={ReferenceLength}";

        int IComparable<IBytecodeTextSymbol>.CompareTo(IBytecodeTextSymbol? other)
        {
            if (other == null) return 1;
            return this.Name?.CompareTo(other.Name) ?? 1;
        }

        bool IEquatable<IBytecodeTextSymbol>.Equals(IBytecodeTextSymbol? other)
        {
            if (other == null) return false;
            return this.Name.Equals(other.Name)
                && this.TextSegmentInstructionOffset.Equals(other.TextSegmentInstructionOffset)
                && this.TextSegmentReferenceOffset.Equals(other.TextSegmentReferenceOffset)
                && this.ReferenceLength.Equals(other.ReferenceLength);
        }
    }
}