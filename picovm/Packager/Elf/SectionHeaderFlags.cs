using System;
using System.ComponentModel;

namespace picovm.Packager.Elf
{
    [Flags]
    public enum SectionHeaderFlags : UInt32
    {
        [ShortName("W")]
        [Description("The section contains data that should be writable during process execution.")]
        SHF_WRITE = 0x1,

        [ShortName("A")]
        [Description("The section occupies memory during process execution. Some control sections do not reside in the memory image ofan object file; this attribute is off for those sections.")]
        SHF_ALLOC = 0x2,

        [ShortName("X")]
        [Description("The section contains executable machine instructions.")]
        SHF_EXECINSTR = 0x4,
        [Description("Identifies a section containing data that may be merged to eliminate duplication")]

        [ShortName("M")]
        SHF_MERGE = 0x10,

        [ShortName("S")]
        [Description("Identifies a section that consists of null-terminated character strings.")]
        SHF_STRINGS = 0x20,

        [ShortName("I")]
        [Description("This section headers sh_info field holds a section header table index.")]
        SHF_INFO_LINK = 0x40,

        [ShortName("L")]
        SHF_LINK_ORDER = 0x80,

        [ShortName("O")]
        SHF_OS_NONCONFORMING = 0x100,

        [ShortName("G")]
        SHF_GROUP = 0x200,

        [ShortName("T")]
        SHF_TLS = 0x400,

        [ShortName("C")]
        SHF_COMPRESSED = 0x800,

        [ShortName("o")]
        [Description("OS-specific")]
        SHF_MASKOS = 0x0ff00000,

        [ShortName("y")]
        SHF_ARM_PURECODE = 0x20000000,

        [Description("Special ordering requirement (Solaris).")]
        SHF_ORDERED = 0x40000000,

        [ShortName("E")]
        [Description("Section is excluded unless referenced or allocated (Solaris).")]
        SHF_EXCLUDE = 0x80000000,

        [ShortName("p")]
        [Description("Reserved for processor-specific semantics")]
        SHF_MASKPROC = 0xf0000000
    }
}