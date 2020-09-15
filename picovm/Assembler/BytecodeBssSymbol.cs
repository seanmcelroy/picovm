using System;

namespace picovm.Assembler
{
    public readonly struct BytecodeBssSymbol
    {
        public enum BssType : byte
        {
            Unknown = 0,
            Byte = 1,
            Word = 2,
            DoubleWord = 3,
            QuadWord = 4
        }

        public readonly string? name;
        public readonly BssType type;
        public readonly ushort length;

        public BytecodeBssSymbol(string? name, BssType type, ushort length)
        {
            this.name = name;
            this.type = type;
            this.length = length;
        }

        public int Size()
        {
            switch (type)
            {
                case BssType.Byte:
                    return length;
                case BssType.Word:
                    return length * 2;
                case BssType.DoubleWord:
                    return length * 4;
                case BssType.QuadWord:
                    return length * 8;
            }

            throw new InvalidOperationException($"Unsupported BSS type: {type}");
        }
    }
}