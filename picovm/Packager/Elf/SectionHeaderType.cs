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

        [ShortName("INIT_ARRAY")]
        [Description("Identifies a section containing an array of pointers to initialization functions. Each pointer in the array is taken as a parameterless procedure with a void return.")]
        SHT_INIT_ARRAY = 14,

        [ShortName("FINI_ARRAY")]
        [Description("Identifies a section containing an array of pointers to termination functions. Each pointer in the array is taken as a parameterless procedure with a void return.")]
        SHT_FINI_ARRAY = 15,

        [ShortName("PREINIT_ARRAY")]
        [Description("Identifies a section containing an array of pointers to functions that are invoked before all other initialization functions. Each pointer in the array is taken as a parameterless procedure with a void return.")]
        SHT_PREINIT_ARRAY = 16,

        [ShortName("GROUP")]
        [Description("Identifies a section group. A section group identifies a set of related sections that must be treated as a unit by the link-editor. Sections of type SHT_GROUP can appear only in relocatable objects.")]
        SHT_GROUP = 17,

        [ShortName("SYMTAB_SHNDX")]
        SHT_SYMTAB_SHNDX = 18,

        [ShortName("LOOS")]
        SHT_LOOS = 0x60000000,

        [ShortName("GNU_ATTRIBUTES")]
        SHT_GNU_ATTRIBUTES = 0x6ffffff5,

        [ShortName("GNU_HASH")]
        SHT_GNU_HASH = 0x6ffffff6,

        [ShortName("GNU_LIBLIST")]
        SHT_GNU_LIBLIST = 0x6ffffff7,

        [ShortName("CHECKSUM")]
        SHT_CHECKSUM = 0x6ffffff8,

        [ShortName("LOSUNW")]
        SHT_LOSUNW = 0x6ffffffa,

        [ShortName("SUNW_move")]
        SHT_SUNW_move = 0x6ffffffa,

        [ShortName("SUNW_COMDAT")]
        SHT_SUNW_COMDAT = 0x6ffffffb,

        [ShortName("SUNW_syminfo")]
        SHT_SUNW_syminfo = 0x6ffffffc,

        [ShortName("GNU_verdef")]
        SHT_GNU_verdef = 0x6ffffffd,

        [ShortName("GNU_verneed")]
        SHT_GNU_verneed = 0x6ffffffe,

        [ShortName("GNU_versym")]
        SHT_GNU_versym = 0x6fffffff,

        [ShortName("HISUNW")]
        SHT_HISUNW = 0x6fffffff,

        [ShortName("LOPROC")]
        SHT_LOPROC = 0x70000000,
        [ShortName("HIPROC")]
        SHT_HIPROC = 0x7fffffff,
        [ShortName("LOUSER")]
        SHT_LOUSER = 0x80000000,
        [ShortName("HIUSER")]
        SHT_HIUSER = 0xffffffff,
    }
}