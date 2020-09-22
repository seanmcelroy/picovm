using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

namespace picovm.Packager.Elf.Elf64
{
    public struct Header64
    {
        // Magic number, always 0x7f E L F
        public static readonly ImmutableArray<byte> MAGIC = ImmutableArray<byte>.Empty.AddRange(new byte[] { 0x7f, 0x45, 0x4c, 0x46 });

        public HeaderIdentityClass EI_CLASS;
        public HeaderIdentityData EI_DATA;
        public HeaderIdentityVersion EI_VERSION;
        public HeaderOsAbiVersion EI_OSABI;
        public byte EI_ABIVERSION;
        public byte EI_PAD9;
        public byte EI_PAD10;
        public byte EI_PAD11;
        public byte EI_PAD12;
        public byte EI_PAD13;
        public byte EI_PAD14;
        public byte EI_PAD15;
        public HeaderType E_TYPE;
        public HeaderMachine E_MACHINE;
        public HeaderVersion E_VERSION;
        // This member gives the virtual address to which the system firsttransfers control, thus starting the process.  If the file has no associated entry point, this member holds zero.
        public UInt64 E_ENTRY;
        // This member holds the program header table's file offset in bytes.  If the file has no program header table, this member holds zero.
        public UInt64 E_PHOFF;
        // This member holds the section header table's file offset in bytes.  If the file has no section header table, this member holds zero.
        public UInt64 E_SHOFF;
        public UInt32 E_FLAGS;
        public UInt16 E_EHSIZE;
        public UInt16 E_PHENTSIZE;
        public UInt16 E_PHNUM;
        public UInt16 E_SHENTSIZE;
        public UInt16 E_SHNUM;
        public UInt16 E_SHSTRNDX;

        public static bool IsFileType(Stream stream)
        {
            if (!stream.CanRead)
                throw new ArgumentException("Stream is not available for reading", nameof(stream));
            if (!stream.CanSeek)
                throw new ArgumentException("Stream is not available for seeking", nameof(stream));

            if (stream.Position != 0)
                stream.Seek(0, SeekOrigin.Begin);

            var magicBuffer = new byte[MAGIC.Length];
            var bytesRead = stream.Read(magicBuffer, 0, MAGIC.Length);
            if (bytesRead != MAGIC.Length)
                return false;
            var magicMatch = bytesRead == MAGIC.Length && Enumerable.SequenceEqual(MAGIC, magicBuffer);
            if (!magicMatch)
                return false;

            stream.Seek(0, SeekOrigin.Begin);
            Header64 potentialHeader;
            if (!Header64.TryRead(stream, out potentialHeader))
                return false;
            return potentialHeader.EI_CLASS == HeaderIdentityClass.ELFCLASS64;
        }

        public static bool TryRead(Stream stream, out Header64 header)
        {
            try
            {
                header = new Header64();
                header.Read(stream);
                return true;
            }
            catch (Exception ex)
            {
                header = default(Header64);
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

            EI_CLASS = stream.ReadByteAndParse<HeaderIdentityClass>(HeaderIdentityClass.ELFCLASSNONE);
            EI_DATA = stream.ReadByteAndParse<HeaderIdentityData>(HeaderIdentityData.ELFDATANONE);
            EI_VERSION = stream.ReadByteAndParse<HeaderIdentityVersion>(HeaderIdentityVersion.EI_CURRENT);
            EI_OSABI = stream.ReadByteAndParse<HeaderOsAbiVersion>(HeaderOsAbiVersion.ELFOSABI_NONE);
            EI_ABIVERSION = (byte)stream.ReadByte();

            stream.Seek(16, SeekOrigin.Begin);
            E_TYPE = stream.ReadHalfWord<HeaderType>(HeaderType.ET_NONE);
            E_MACHINE = stream.ReadHalfWord<HeaderMachine>(HeaderMachine.EM_NONE);
            E_VERSION = stream.ReadWord<HeaderVersion>(HeaderVersion.EV_NONE);
            E_ENTRY = stream.ReadAddress64();
            E_PHOFF = stream.ReadOffset64();
            E_SHOFF = stream.ReadOffset64();
            E_FLAGS = stream.ReadUInt32();
            E_EHSIZE = stream.ReadUInt16();
            E_PHENTSIZE = stream.ReadUInt16();
            E_PHNUM = stream.ReadUInt16();
            E_SHENTSIZE = stream.ReadUInt16();
            E_SHNUM = stream.ReadUInt16();
            E_SHSTRNDX = stream.ReadUInt16();

            if (E_EHSIZE != stream.Position)
            {
                throw new InvalidOperationException("E_EHSIZE does not equal the current reader position");
            }
        }

        public void Write(Stream stream, UInt16 programHeaderCount, UInt16 sectionHeaderCount)
        {
            E_PHNUM = programHeaderCount;
            E_SHNUM = sectionHeaderCount;

            // E_IDENT

            UInt16 headerLength = 0;
            // Index 0-3
            headerLength += stream.WriteAndCount(MAGIC.AsSpan());
            headerLength += stream.WriteOneByte((byte)EI_CLASS);
            headerLength += stream.WriteOneByte((byte)EI_DATA);
            headerLength += stream.WriteOneByte((byte)EI_VERSION);
            // Index 7-15 are padding
            headerLength += stream.WriteAndCount(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 });
            headerLength += stream.WriteOneByte((byte)16); // Size of this header, always 16 bytes

            headerLength += stream.WriteHalfWord((UInt16)E_TYPE);
            headerLength += stream.WriteHalfWord((UInt16)E_MACHINE);
            headerLength += stream.WriteWord((UInt32)E_VERSION);
            headerLength += stream.WriteAddress64(E_ENTRY);
            headerLength += stream.WriteOffset64(E_PHOFF);
            headerLength += stream.WriteOffset64(E_SHOFF);
            headerLength += stream.WriteAndCount(BitConverter.GetBytes(E_FLAGS));
            headerLength += stream.WriteHalfWord(E_EHSIZE); // Is this 0x34?
            headerLength += stream.WriteHalfWord(E_PHENTSIZE);
            headerLength += stream.WriteHalfWord(E_PHNUM);
            headerLength += stream.WriteHalfWord(E_SHENTSIZE);
            headerLength += stream.WriteHalfWord(E_SHNUM);
            headerLength += stream.WriteHalfWord(E_SHSTRNDX);

            if (E_EHSIZE != headerLength)
                throw new InvalidOperationException("Miscalculation of E_EHSIZE");

            // Pad out to program header start.
            while (E_PHOFF - headerLength > 0)
            {
                headerLength += stream.WriteOneByte(0);
            }
        }
    }
}