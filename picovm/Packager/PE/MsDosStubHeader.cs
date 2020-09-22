using System;
using System.IO;
using System.Linq;

namespace picovm.Packager.PE
{
    public struct MsDosStubHeader
    {
        // Magic number, always 0x5A4D (MZ in LE)
        private static readonly byte[] MAGIC = new byte[] { 0x4d, 0x5a };

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
                var magicBuffer = new byte[MAGIC.Length];
                var bytesRead = stream.Read(magicBuffer, 0, MAGIC.Length);
                if (bytesRead != MAGIC.Length)
                    return false;
                var magicMatch = bytesRead == MAGIC.Length && Enumerable.SequenceEqual(MAGIC, magicBuffer);
                if (!magicMatch)
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
                PEHeader potentialHeader;
                if (!PEHeader.TryRead(stream, out potentialHeader))
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
                header = default(MsDosStubHeader);
                Console.Error.WriteLine(ex);
                return false;
            }
        }

        public void Read(Stream stream)
        {
            var magic = new byte[MAGIC.Length];
            stream.Read(magic);
            if (!MAGIC.SequenceEqual(magic))
                throw new BadImageFormatException("Magic value is not present for an ELF file");

            stream.Seek(0x3C, SeekOrigin.Begin);

            e_lfanew = stream.ReadUInt32();
        }
    }
}