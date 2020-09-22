using System;
using System.ComponentModel;
using System.IO;

namespace picovm.Packager.Elf.Elf32
{
    public struct SectionHeader32
    {
        public UInt32 SH_NAME;

        [Description("This member categorizes the section's contents and semantics. Section types and their descriptions appear below")]
        public SectionHeaderType SH_TYPE;

        [Description("Sections support 1-bit flags that describe miscellaneous attributes.")]
        public UInt32 SH_FLAGS;

        [Description("If the section will appear in the memory image of a process, this member gives the address at which the section's first byte should reside.  Otherwise, the member contains 0.")]
        public UInt32 SH_ADDR;

        [Description("This member's value gives the byte offset from the beginning of the file to the first byte in the section.  One section type, SHT_NOBITS described below, occupies no space in the file, and its sh_offset member locates the conceptual placement in the file.")]
        public UInt32 SH_OFFSET;

        [Description("This member gives the section's size in bytes.  Unless the section type is SHT_NOBITS, the section occupiess h_size bytes in the file.  A section of type SHT_NOBITS may have a non-zero size, but it occupies no space in the file")]
        public UInt32 SH_SIZE;

        [Description("This member holds a section header table index link, whose interpretation depends on the section type.  A table below describes the values")]
        public UInt32 SH_LINK;

        [Description("This member holds extra information, whose interpretation depends on the section type.")]
        public UInt32 SH_INFO;

        public UInt32 SH_ADDRALIGN;
        public UInt32 SH_ENTSIZE;

        public UInt16 Write(Stream stream, HeaderIdentityClass EI_CLASS)
        {
            UInt16 headerLength = 0;

            headerLength += stream.WriteWord((UInt32)SH_NAME);
            headerLength += stream.WriteWord((UInt32)SH_TYPE);
            headerLength += stream.WriteWord((UInt32)SH_FLAGS);
            headerLength += stream.WriteAddress32(SH_ADDR);
            headerLength += stream.WriteOffset32(SH_OFFSET);
            headerLength += stream.WriteWord((UInt32)SH_SIZE);
            headerLength += stream.WriteWord((UInt32)SH_LINK);
            headerLength += stream.WriteWord((UInt32)SH_INFO);
            headerLength += stream.WriteWord((UInt32)SH_ADDRALIGN);
            headerLength += stream.WriteWord((UInt32)SH_ENTSIZE);

            return headerLength;
        }
    }
}
