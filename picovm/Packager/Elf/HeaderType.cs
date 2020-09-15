using System;

namespace picovm.Packager.Elf
{
    public enum HeaderType : UInt16
    {
        // No file type
        ET_NONE = 0,
        // Relocatable
        ET_REL = 1,
        // Executable
        ET_EXEC = 2,
        // Shared object file
        ET_DYN = 3,
        // Core file
        ET_CORE = 4,
        // Processor specific
        ET_LOPROC = 0xff00,
        // Processor specific
        ET_HIPROC = 0xffff
    }
}