using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace picovm.Packager
{
    public static class StreamUtility
    {
        public static T ReadByteAndParse<T>(this Stream stream, T defaultNoMatch) where T : struct, IConvertible
        {
            if (!typeof(T).IsEnum)
                throw new ArgumentException("T must be an enumerated type");

            var value = (byte)stream.ReadByte();
            if (Enum.GetName(typeof(T), value) == null)
                return defaultNoMatch;
            return (T)(object)value;
        }

        public static UInt32 ReadAddress32(this Stream stream)
        {
            var fourBytes = new byte[4];
            stream.Read(fourBytes);
            return BitConverter.ToUInt32(fourBytes);
        }

        public static UInt64 ReadAddress64(this Stream stream)
        {
            var eightBytes = new byte[8];
            stream.Read(eightBytes);
            return BitConverter.ToUInt64(eightBytes);
        }

        public static UInt32 ReadOffset32(this Stream stream) => ReadAddress32(stream);

        public static UInt64 ReadOffset64(this Stream stream) => ReadAddress64(stream);

        public static UInt16 ReadUInt16(this Stream stream)
        {
            var twoBytes = new byte[2];
            stream.Read(twoBytes);
            return BitConverter.ToUInt16(twoBytes);
        }

        public static UInt32 ReadUInt32(this Stream stream)
        {
            var fourBytes = new byte[4];
            stream.Read(fourBytes);
            return BitConverter.ToUInt32(fourBytes);
        }

        public static UInt64 ReadUInt64(this Stream stream)
        {
            var eightBytes = new byte[8];
            stream.Read(eightBytes);
            return BitConverter.ToUInt64(eightBytes);
        }

        public static string ReadNulTerminatedString(this Stream stream)
        {
            var sb = new StringBuilder();
            while (true)
            {
                var i = stream.ReadByte();
                if (i == -1 || i == 0)
                    return sb.ToString();
                sb.Append((char)(byte)i);
            }
        }

        public static UInt16 WriteOneByte(this Stream stream, byte value)
        {
            stream.WriteByte(value);
            return 1;
        }
        public static UInt16 WriteAddress32(this Stream stream, UInt32 value)
        {
            stream.Write(BitConverter.GetBytes((UInt32)value));
            return sizeof(UInt32);
        }

        public static UInt16 WriteAddress64(this Stream stream, UInt64 value)
        {
            stream.Write(BitConverter.GetBytes(value));
            return sizeof(UInt64);
        }

        public static UInt16 WriteHalfWord(this Stream stream, UInt16 value)
        {
            stream.Write(BitConverter.GetBytes((UInt16)value));
            return sizeof(UInt16);
        }
        public static UInt16 WriteOffset32(this Stream stream, UInt32 value) => stream.WriteAddress32(value);
        public static UInt16 WriteOffset64(this Stream stream, UInt64 value) => stream.WriteAddress64(value);

        public static void SeekToRVA(this Stream stream, IEnumerable<PE.SectionHeaderEntry> sectionHeaders, UInt32 rva)
        {
            var sectionForRva = sectionHeaders.Single(sh => rva >= sh.VirtualAddress && rva < sh.VirtualAddress + sh.VirtualSize);
            var seekAddr = sectionForRva.PointerToRawData + (rva - sectionForRva.VirtualAddress);
            stream.Seek(seekAddr, SeekOrigin.Begin);
        }
    }
}