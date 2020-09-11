using System;

namespace picovm.Compiler
{
    public struct BytecodeBssSymbol
    {
        public enum BssType : byte
        {
            Unknown = 0,
            Byte = 1,
            Word = 2,
            DoubleWord = 3,
            QuadWord = 4
        }

        public string? name;
        public BssType type;
        public ushort length;

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