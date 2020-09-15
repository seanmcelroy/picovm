using System;

namespace picovm.Assembler
{
    public static class ValueTypeUtility
    {
        public static ValueType Add<TAddrSize>(this ValueType value, ValueType increment) => typeof(TAddrSize) == typeof(UInt32)
                                ? (ValueType)(UInt32)((UInt32)value + Convert.ToUInt32(increment))
                                : (ValueType)(UInt64)((UInt64)value + Convert.ToUInt64(increment));
    }
}