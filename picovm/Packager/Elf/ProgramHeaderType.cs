using System;
using System.ComponentModel;

namespace picovm.Packager.Elf
{
    public enum ProgramHeaderType : UInt32
    {
        [ShortName("NULL")]
        [Description("The array element is unused; other members' values are undefined.  This type lets the program header table have ignored entries")]
        PT_NULL = 0,
        [ShortName("LOAD")]
        [Description("Loadable segment. The array element specifies a loadable segment, described by p_filesz and p_memsz.  The bytes from the file are mapped to the beginning of the memory segment.  If the segment’s memory size (p_memsz) is larger than the file size (p_filesz), the \"extra\" bytes are defined to hold the value 0 and to follow the segment's initialized area.  The file size may not be larger than the memorysize.  Loadable segment entries in the program header table appear in ascending order, sorted on the p_vaddr member.")]
        PT_LOAD = 1,
        [ShortName("DYNAMIC")]
        PT_DYNAMIC = 2,
        [ShortName("INTERP")]
        PT_INTERP = 3,
        [ShortName("NOTE")]
        PT_NOTE = 4,
        // This segment type is reserved but has unspecified semantics.  Pro-grams that contain an array element of this type do not conformto the ABI.
        [ShortName("SHLIB")]
        PT_SHLIB = 5,
        [ShortName("PHDR")]
        PT_PHDR = 6,
        [ShortName("GNU_EH_FRAME")]
        [Description("The array element specifies the location and size of the exception handling information as defined by the .eh_frame_hdr section.")]
        PT_GNU_EH_FRAME = 0x6474e550,
        [ShortName("GNU_STACK")]
        [Description("The p_flags member specifies the permissions on the segment containing the stack and is used to indicate wether the stack should be executable. The absense of this header indicates that the stack will be executable.")]
        PT_GNU_STACK = 0x6474e551,
        [ShortName("GNU_RELRO")]
        [Description("The array element specifies the location and size of a segment which may be made read-only after relocation shave been processed.")]
        PT_GNU_RELRO = 0x6474e552,
        [ShortName("LOSUNW")]
        PT_LOSUNW = 0x6ffffffa,
        [ShortName("SUNWBSS")]
        PT_SUNWBSS = 0x6ffffffb,
        [ShortName("SUNWSTACK")]
        PT_SUNWSTACK = 0x6ffffffa,
        [ShortName("HISUNW")]
        PT_HISUNW = 0x6fffffff,
        [ShortName("LOPROC")]
        PT_LOPROC = 0x70000000,
        [ShortName("HIPROC")]
        PT_HIPROC = 0x7fffffff
    }
}