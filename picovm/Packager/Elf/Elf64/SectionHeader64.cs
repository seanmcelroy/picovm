using System;
using System.ComponentModel;
using System.IO;

namespace picovm.Packager.Elf.Elf64
{
    public struct SectionHeader64
    {
        public UInt32 SH_NAME;

        [Description("This member categorizes the section's contents and semantics. Section types and their descriptions appear below")]
        public SectionHeaderType SH_TYPE;

        [Description("Sections support 1-bit flags that describe miscellaneous attributes.")]
        public UInt64 SH_FLAGS;

        [Description("If the section will appear in the memory image of a process, this member gives the address at which the section's first byte should reside.  Otherwise, the member contains 0.")]
        public UInt64 SH_ADDR;

        [Description("This member's value gives the byte offset from the beginning of the file to the first byte in the section.  One section type, SHT_NOBIT Sdescribed below, occupies no space in the file, and its sh_offset member locates the conceptual placement in the file.")]
        public UInt64 SH_OFFSET;

        [Description("This member gives the section's size in bytes.  Unless the section type is SHT_NOBITS, the section occupiess h_size bytes in the file.  A section of type SHT_NOBITS may have a non-zero size, but it occupies no space in the file")]
        public UInt64 SH_SIZE;

        [Description("This member holds a section header table index link, whose interpretation depends on the section type.  A table below describes the values")]
        public UInt32 SH_LINK;

        [Description("This member holds extra information, whose interpretation depends on the section type.")]
        public UInt32 SH_INFO;

        public UInt64 SH_ADDRALIGN;
        public UInt64 SH_ENTSIZE;

        public void Read(Stream stream)
        {
            SH_NAME = stream.ReadUInt32();
            SH_TYPE = stream.ReadWord<SectionHeaderType>(SectionHeaderType.SHT_NULL);
            SH_FLAGS = stream.ReadXWord();
            SH_ADDR = stream.ReadAddress64();
            SH_OFFSET = stream.ReadOffset64();
            SH_SIZE = stream.ReadXWord();
            SH_LINK = stream.ReadUInt32();
            SH_INFO = stream.ReadUInt32();
            SH_ADDRALIGN = stream.ReadXWord();
            SH_ENTSIZE = stream.ReadXWord();
        }

        public UInt16 Write(Stream stream, HeaderIdentityClass EI_CLASS)
        {
            UInt16 headerLength = 0;

            headerLength += stream.WriteWord((UInt32)SH_NAME);
            headerLength += stream.WriteWord((UInt32)SH_TYPE);
            headerLength += stream.WriteXWord(SH_FLAGS);
            headerLength += stream.WriteAddress64(SH_ADDR);
            headerLength += stream.WriteOffset64(SH_OFFSET);
            headerLength += stream.WriteXWord(SH_SIZE);
            headerLength += stream.WriteWord((UInt32)SH_LINK);
            headerLength += stream.WriteWord((UInt32)SH_INFO);
            headerLength += stream.WriteXWord(SH_ADDRALIGN);
            headerLength += stream.WriteXWord(SH_ENTSIZE);

            return headerLength;
        }
    }
}
