using System;
using System.ComponentModel;

namespace picovm.Packager.Elf.Elf
{
    public enum ProgramHeaderType : UInt32
    {
        [Description("The array element is unused; other members' values are undefined.  This type lets the program header table have ignored entries")]
        PT_NULL = 0,
        [Description("Loadable segment. The array element specifies a loadable segment, described by p_filesz and p_memsz.  The bytes from the file are mapped to the beginning of the memory segment.  If the segment’s memory size (p_memsz) is larger than the file size (p_filesz), the \"extra\" bytes are defined to hold the value 0 and to follow the segment's initialized area.  The file size may not be larger than the memorysize.  Loadable segment entries in the program header table appear in ascending order, sorted on the p_vaddr member.")]
        PT_LOAD = 1,
        PT_DYNAMIC = 2,
        PT_INTERP = 3,
        PT_NOTE = 4,
        // This segment type is reserved but has unspecified semantics.  Pro-grams that contain an array element of this type do not conformto the ABI.
        PT_SHLIB = 5,
        PT_PHDR = 6,
        PT_LOPROC = 0x70000000,
        PT_HIPROC = 0x7fffffff
    }
}