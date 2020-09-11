using System;
using System.ComponentModel;

namespace picovm.Packager.Elf64
{
    [Flags]
    public enum SectionHeaderFlags : UInt32
    {
        [Description("The section contains data that should be writable during process execution.")]
        SHF_WRITE = 0x1,

        [Description("The section occupies memory during process execution. Some control sections do not reside in the memory image ofan object file; this attribute is off for those sections.")]
        SHF_ALLOC = 0x2,

        [Description("The section contains executable machine instructions.")]
        SHF_EXECINSTR = 0x4,

        [Description("Reserved for processor-specific semantics")]
        SHF_MASKPROC = 0xf000000
    }
}