using System;
using System.ComponentModel;

namespace picovm.Packager.Elf
{
    public enum HeaderVersion : UInt32
    {
        // Invalid version
        [Description("0x0 NONE")]
        EV_NONE = 0,
        // Current version
        [Description("0x1 (current)")]
        EV_CURRENT = 1
    }
}