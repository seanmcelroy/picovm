using System;

namespace picovm.Packager.Elf
{
    public enum HeaderVersion : UInt32
    {
        // Invalid version
        EV_NONE = 0,
        // Current version
        EV_CURRENT = 1
    }
}