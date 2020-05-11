namespace picovm.Compiler
{
    public sealed class BytecodeDataSymbol
    {
        public uint dataSegmentOffset;
        public ushort length;
        public bool constant;

        public override string ToString() => $"Offset:{dataSegmentOffset}, len={length}";
    }
}