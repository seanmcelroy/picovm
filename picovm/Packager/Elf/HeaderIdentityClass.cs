using System.ComponentModel;

namespace picovm.Packager.Elf
{
    public enum HeaderIdentityClass : byte
    {
        ELFCLASSNONE = 0,
        [Description("ELF32")]
        ELFCLASS32 = 1,
        [Description("ELF64")]
        ELFCLASS64 = 2
    }
}