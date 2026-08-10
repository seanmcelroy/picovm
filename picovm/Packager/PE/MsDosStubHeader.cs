using System;
using System.IO;
using System.Linq;

namespace picovm.Packager.PE
{
    public struct MsDosStubHeader
    {
        // Magic number, always 0x5A4D (MZ in LE)
        private static readonly byte[] MAGIC = [0x4d, 0x5a];

        public UInt32 e_lfanew;

        public static bool IsFileType(Stream stream)
        {
            if (!stream.CanRead)
                throw new ArgumentException("Stream is not available for reading", nameof(stream));
            if (!stream.CanSeek)
                throw new ArgumentException("Stream is not available for seeking", nameof(stream));

            if (stream.Position != 0)
                stream.Seek(0, SeekOrigin.Begin);

            {
                Span<byte> magic = stackalloc byte[MAGIC.Length];
                try
                {
                    stream.ReadExactly(magic);
                }
                catch (EndOfStreamException)
                {
                    return false;
                }
                if (!MAGIC.AsSpan().SequenceEqual(magic))
                    return false;
            }

            // Read e_lfanew
            stream.Seek(0x3C, SeekOrigin.Begin);
            {
                var lfaNewBuffer = new byte[4];
                var bytesRead = stream.Read(lfaNewBuffer, 0, lfaNewBuffer.Length);
                if (bytesRead != lfaNewBuffer.Length)
                    return false;

                var peHeaderLocation = BitConverter.ToUInt32(lfaNewBuffer);
                stream.Seek(peHeaderLocation, SeekOrigin.Begin);
                if (!PEHeader.TryRead(stream, out PEHeader potentialHeader))
                    return false;
            }

            return true;
        }

        public static bool TryRead(Stream stream, out MsDosStubHeader header)
        {
            try
            {
                header = new MsDosStubHeader();
                header.Read(stream);
                return true;
            }
            catch (Exception ex)
            {
                header = default;
                Console.Error.WriteLine(ex);
                return false;
            }
        }

        public void Read(Stream stream)
        {
            Span<byte> magic = stackalloc byte[MAGIC.Length];
            stream.ReadExactly(magic);
            if (!MAGIC.SequenceEqual(magic))
                throw new BadImageFormatException("Magic value (MZ) is not present for a PE file.");

            stream.Seek(0x3C, SeekOrigin.Begin);

            e_lfanew = stream.ReadUInt32();
        }
    }
}