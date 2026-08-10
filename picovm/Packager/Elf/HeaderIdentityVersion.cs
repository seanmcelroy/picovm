using System.ComponentModel;

namespace picovm.Packager.Elf
{
    public enum HeaderIdentityVersion : byte
    {
        /// <summary>
        /// Invalid version identity
        /// </summary>
        [Description("0 (unknown)")]
        EI_NONE = 0,

        [Description("1 (current)")]
        EI_CURRENT = 1
    }
}