using System;
using System.ComponentModel;

namespace picovm.Packager.Elf
{
    [Flags]
    public enum SegmentPermissionFlags : UInt32
    {
        // Executable
        [ShortName("X")]
        [Description("Execute")]
        PF_X = 0x1,
        // Write
        [ShortName("W")]
        [Description("Write")]
        PF_W = 0x2,
        // Read
        [ShortName("R")]
        [Description("Read")]
        PF_R = 0x4,
        [Description("Unspecified")]
        PF_MASKPROC = 0xf000000
    }
}