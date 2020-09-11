using System;
using System.ComponentModel;

namespace picovm.Packager.Elf64
{
    public enum SegmentPermissionFlags : UInt32
    {
        // Executable
        [Description("Execute")]
        PF_X = 0x1,
        // Write
        [Description("Write")]
        PF_W = 0x2,
        // Read
        [Description("Read")]
        PF_R = 0x4,
        [Description("Unspecified")]
        PF_MASKPROC = 0xf000000
    }
}