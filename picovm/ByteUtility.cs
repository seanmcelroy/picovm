namespace picovm
{
    public static class ByteUtility
    {
        public static int CountBits(long value) => System.Numerics.BitOperations.PopCount((ulong)value);

        public static int CountBits(ulong value) => System.Numerics.BitOperations.PopCount(value);

        public static int CountBits(int value) => System.Numerics.BitOperations.PopCount((uint)value);

        public static int CountBits(uint value) => System.Numerics.BitOperations.PopCount(value);

        public static int CountBits(ushort value) => System.Numerics.BitOperations.PopCount(value);

        public static int CountBits(byte value)=> System.Numerics.BitOperations.PopCount(value);
    }
}