using System;
using System.Numerics;

namespace picovm.Assembler
{
    public static class ValueTypeUtility
    {
        public static TAddrSize Add<TAddrSize>(this TAddrSize value, TAddrSize increment) where TAddrSize : struct, INumber<TAddrSize> => typeof(TAddrSize) == typeof(UInt32)
            ? (TAddrSize)(ValueType)(Convert.ToUInt32(value) + Convert.ToUInt32(increment))
            : (TAddrSize)(ValueType)(Convert.ToUInt64(value) + Convert.ToUInt64(increment));

        public static TAddrSize Add<TAddrSize>(this TAddrSize value, int increment) where TAddrSize : struct, INumber<TAddrSize> => typeof(TAddrSize) == typeof(UInt32)
            ? (TAddrSize)(ValueType)(Convert.ToUInt32(value) + Convert.ToUInt32(increment))
            : (TAddrSize)(ValueType)(Convert.ToUInt64(value) + Convert.ToUInt64(increment));


        public static TAddrSize Add<TAddrSize>(this TAddrSize value, UInt32 increment) where TAddrSize : struct, INumber<TAddrSize> => typeof(TAddrSize) == typeof(UInt32)
            ? (TAddrSize)(ValueType)(Convert.ToUInt32(value) + increment)
            : (TAddrSize)(ValueType)(Convert.ToUInt64(value) + Convert.ToUInt64(increment));

        public static TAddrSize Add<TAddrSize>(this TAddrSize value, UInt64 increment) where TAddrSize : struct, IComparable, INumber<TAddrSize> => typeof(TAddrSize) == typeof(UInt32)
            ? (TAddrSize)(ValueType)(Convert.ToUInt32(value) + Convert.ToUInt32(increment))
            : (TAddrSize)(ValueType)(Convert.ToUInt64(value) + increment);

        public static TAddrSize Add<TAddrSize>(this ValueType value, ValueType increment) where TAddrSize : struct, INumber<TAddrSize> => typeof(TAddrSize) == typeof(UInt32)
            ? (TAddrSize)(ValueType)(Convert.ToUInt32(value) + Convert.ToUInt32(increment))
            : (TAddrSize)(ValueType)(Convert.ToUInt64(value) + Convert.ToUInt64(increment));

        public static TAddrSize Sub<TAddrSize>(this TAddrSize value, TAddrSize increment) where TAddrSize : struct, INumber<TAddrSize> => typeof(TAddrSize) == typeof(UInt32)
            ? (TAddrSize)(ValueType)(Convert.ToUInt32(value) - Convert.ToUInt32(increment))
            : (TAddrSize)(ValueType)(Convert.ToUInt64(value) - Convert.ToUInt64(increment));

        public static TAddrSize Sub<TAddrSize>(this TAddrSize value, int increment) where TAddrSize : struct, IComparable, INumber<TAddrSize> => typeof(TAddrSize) == typeof(UInt32)
            ? (TAddrSize)(ValueType)(Convert.ToUInt32(value) - Convert.ToUInt32(increment))
            : (TAddrSize)(ValueType)(Convert.ToUInt64(value) - Convert.ToUInt64(increment));

        public static TAddrSize Sub<TAddrSize>(this TAddrSize value, UInt32 increment) where TAddrSize : struct, IComparable, INumber<TAddrSize> => typeof(TAddrSize) == typeof(UInt32)
            ? (TAddrSize)(ValueType)(Convert.ToUInt32(value) - increment)
            : (TAddrSize)(ValueType)(Convert.ToUInt64(value) - Convert.ToUInt64(increment));

        public static TAddrSize Sub<TAddrSize>(this TAddrSize value, UInt64 increment) where TAddrSize : struct, INumber<TAddrSize> => typeof(TAddrSize) == typeof(UInt32)
            ? (TAddrSize)(ValueType)(Convert.ToUInt32(value) - Convert.ToUInt32(increment))
            : (TAddrSize)(ValueType)(Convert.ToUInt64(value) - increment);

        public static TAddrSize Sub<TAddrSize>(this ValueType value, ValueType increment) where TAddrSize : struct, INumber<TAddrSize> => typeof(TAddrSize) == typeof(UInt32)
            ? (TAddrSize)(ValueType)(Convert.ToUInt32(value) - Convert.ToUInt32(increment))
            : (TAddrSize)(ValueType)(Convert.ToUInt64(value) - Convert.ToUInt64(increment));
    }
}