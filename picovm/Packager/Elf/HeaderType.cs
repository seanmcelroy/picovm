using System;
using System.ComponentModel;

namespace picovm.Packager.Elf
{
    public enum HeaderType : UInt16
    {
        [Description("NONE (No file type)")]
        ET_NONE = 0,
        [Description("REL (Relocatable file)")]
        ET_REL = 1,
        [Description("EXEC (Executable file)")]
        ET_EXEC = 2,
        [Description("DYN (Shared object file)")]
        ET_DYN = 3,
        [Description("CORE (Core file)")]
        ET_CORE = 4,
        // Processor specific
        ET_LOPROC = 0xff00,
        // Processor specific
        ET_HIPROC = 0xffff
    }
}