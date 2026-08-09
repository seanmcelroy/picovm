namespace picovm
{
    public static class ByteUtility
    {
        public static int CountBits(long value)
        {
            int count = 0;
            while (value != 0)
            {
                count++;
                value &= value - 1;
            }
            return count;
        }
        public static int CountBits(ulong value)
        {
            int count = 0;
            while (value != 0)
            {
                count++;
                value &= value - 1;
            }
            return count;
        }
        public static int CountBits(int value)
        {
            int count = 0;
            while (value != 0)
            {
                count++;
                value &= value - 1;
            }
            return count;
        }
        public static int CountBits(uint value)
        {
            int count = 0;
            while (value != 0)
            {
                count++;
                value &= value - 1;
            }
            return count;
        }
        public static int CountBits(ushort value)
        {
            int count = 0;
            while (value != 0)
            {
                count++;
                value &= (ushort)(value - 1);
            }
            return count;
        }
        public static int CountBits(byte value)
        {
            int count = 0;
            while (value != 0)
            {
                count++;
                value &= (byte)(value - 1);
            }
            return count;
        }
    }
}