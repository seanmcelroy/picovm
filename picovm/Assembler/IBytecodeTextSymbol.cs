using System;

namespace picovm.Assembler
{
    public interface IBytecodeTextSymbol : IComparable<IBytecodeTextSymbol>, IEquatable<IBytecodeTextSymbol>
    {
        string Name { get; }
        // The offset for the beginning of the instruction.  Useful if we need to change the opcode, like MOV_REG_MEM to MOV_REG_CON for a constant
        ValueType TextSegmentInstructionOffset { get; }
        // The offset for the actual symbol.  At this address, should be 0xFF as a placeholder until the symbol is resolved through substitution
        ValueType TextSegmentReferenceOffset { get; }
        byte ReferenceLength { get; }
    }
}