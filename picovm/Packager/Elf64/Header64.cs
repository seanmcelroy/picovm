using System;
using System.IO;
using System.Linq;

namespace picovm.Packager.Elf64
{
    public struct Header64
    {
        // Magic number, always 0x7f E L F
        private static readonly byte[] MAGIC = new byte[] { 0x7f, 0x45, 0x4c, 0x46 };

        public HeaderIdentityClass EI_CLASS;
        public HeaderIdentityData EI_DATA;
        public HeaderIdentityVersion EI_VERSION;
        public byte EI_OSABI;
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
        public UInt16 E_SHSTRIDX;

        public void Read(Stream stream)
        {
            var magic = new byte[4];
            stream.Read(magic);
            if (!MAGIC.SequenceEqual(magic))
                throw new BadImageFormatException("Magic value is not present for an ELF file");

            EI_CLASS = stream.ReadByteAndParse<HeaderIdentityClass>(HeaderIdentityClass.ELFCLASSNONE);
            EI_DATA = stream.ReadByteAndParse<HeaderIdentityData>(HeaderIdentityData.ELFDATANONE);
            EI_VERSION = stream.ReadByteAndParse<HeaderIdentityVersion>(HeaderIdentityVersion.EI_CURRENT);

            stream.Seek(9, SeekOrigin.Current);
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
            E_SHSTRIDX = stream.ReadUInt16();

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
            headerLength += stream.WriteAndCount(MAGIC);
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
            headerLength += stream.WriteHalfWord(E_SHSTRIDX);

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