using System;

namespace picovm.Assembler
{
    public readonly struct BytecodeBssSymbol(string? name, BytecodeBssSymbol.BssType type, ushort length)
    {
        public enum BssType : byte
        {
            Unknown = 0,
            Byte = 1,
            Word = 2,
            DoubleWord = 3,
            QuadWord = 4
        }

        public readonly string? name = name;
        public readonly BssType type = type;
        public readonly ushort length = length;

        public int Size()
        {
            return type switch
            {
                BssType.Byte => length,
                BssType.Word => length * 2,
                BssType.DoubleWord => length * 4,
                BssType.QuadWord => length * 8,
                _ => throw new InvalidOperationException($"Unsupported BSS type: {type}"),
            };
        }
    }
}