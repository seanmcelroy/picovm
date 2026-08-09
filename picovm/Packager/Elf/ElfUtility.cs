using System;
using System.IO;

namespace picovm.Packager.Elf
{
    public static class ElfUtility
    {
        public static T ReadHalfWord<T>(this Stream stream, T defaultNoMatch) where T : struct, IConvertible
        {
            if (!typeof(T).IsEnum)
                throw new ArgumentException("T must be an enumerated type");

            var twoBytes = new byte[2];
            stream.ReadExactly(twoBytes);
            var value = BitConverter.ToUInt16(twoBytes);

            if (Enum.GetName(typeof(T), value) == null)
                return defaultNoMatch;
            return (T)(object)value;
        }

        public static UInt64 ReadXWord(this Stream stream) => StreamUtility.ReadUInt64(stream);

        public static T ReadWord<T>(this Stream stream, T defaultNoMatch) where T : struct, IConvertible
        {
            if (!typeof(T).IsEnum)
                throw new ArgumentException("T must be an enumerated type");

            var fourBytes = new byte[4];
            stream.ReadExactly(fourBytes);
            var value = BitConverter.ToUInt32(fourBytes);

            if (Enum.GetName(typeof(T), value) == null)
                return defaultNoMatch;
            return (T)(object)value;
        }

        public static UInt16 WriteHalfWord(this Stream stream, UInt16 value)
        {
            stream.Write(BitConverter.GetBytes(value));
            return sizeof(UInt16);
        }
        public static UInt16 WriteWord(this Stream stream, UInt32 value)
        {
            stream.Write(BitConverter.GetBytes(value));
            return sizeof(UInt32);
        }
        public static UInt16 WriteXWord(this Stream stream, UInt64 value)
        {
            stream.Write(BitConverter.GetBytes(value));
            return sizeof(UInt64);
        }

        public static UInt16 WriteAndCount(this Stream stream, ReadOnlySpan<byte> buffer)
        {
            stream.Write(buffer);
            return (UInt16)buffer.Length;
        }

        public static int CalculateRoundUpTo16Pad(this uint realSize, uint roundUp = 16) => realSize % roundUp == 0 ? 0 : (int)(roundUp - (realSize % roundUp));
        public static int CalculateRoundUpTo16Pad(this ushort realSize, uint roundUp = 16) => realSize % roundUp == 0 ? 0 : (int)(roundUp - (realSize % roundUp));

        public static UInt32 GnuHash(Span<byte> bytes)
        {
            UInt32 hash = 5381;
            foreach (var b in bytes)
            {
                hash = (hash << 5) + hash + b;
            }
            return hash;
        }
    }
}