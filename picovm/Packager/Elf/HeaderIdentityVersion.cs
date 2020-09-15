using System.ComponentModel;

namespace picovm.Packager.Elf
{
    public enum HeaderIdentityVersion : byte
    {
        [Description("1 (current)")]
        EI_CURRENT = 1
    }
}