using System;
using System.ComponentModel;

namespace picovm.Packager.Elf
{
    public enum SectionHeaderType : UInt32
    {
        [ShortName("NULL")]
        [Description("This value marks the section header as inactive; it does not have an associated section.  Other members of the section header have undefined values.")]
        SHT_NULL = 0,

        [ShortName("PROGBITS")]
        [Description("The section holds information defined by the program, whose format and meaning are determined solely by the program.")]
        SHT_PROGBITS = 1,
        [ShortName("SYMTAB")]
        SHT_SYMTAB = 2,

        [ShortName("STRTAB")]
        [Description("The section holds a string table.  An object file may have multiple string table sections.")]
        SHT_STRTAB = 3,
        [ShortName("RELA")]
        SHT_RELA = 4,
        [ShortName("HASH")]
        SHT_HASH = 5,
        [ShortName("DYNAMIC")]
        SHT_DYNAMIC = 6,
        [ShortName("NOTE")]
        SHT_NOTE = 7,
        [ShortName("NOBITS")]
        SHT_NOBITS = 8,
        [ShortName("REL")]
        SHT_REL = 9,
        [ShortName("SHLIB")]
        SHT_SHLIB = 10,
        [ShortName("DYNSYM")]
        SHT_DYNSYM = 11,
        SHT_LOPROC = 0x70000000,
        SHT_HIPROC = 0x7fffffff,
        SHT_LOUSER = 0x80000000,
        SHT_HIUSER = 0xffffffff,
    }
}