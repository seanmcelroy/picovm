namespace agent_playground
{
    public struct BytecodeTextSymbol
    {
        public string name;
        // The offset for the beginning of the instruction.  Useful if we need to change the opcode, like MOV_REG_MEM to MOV_REG_CON for a constant
        public ushort textSegmentInstructionOffset;
        // The offset for the actual symbol.  At this address, should be 0xFF as a placeholder until the symbol is resolved through substitution
        public ushort textSegmentReferenceOffset;
        public byte referenceLength;
    }
}