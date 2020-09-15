using System.ComponentModel;

namespace picovm.Packager.Elf
{
    public enum HeaderIdentityData : byte
    {
        ELFDATANONE = 0,
        [Description("2's complement, little endian")]
        ELFDATA2LSB = 1,
        [Description("2's complement, big endian")]
        ELFDATA2MSB = 2
    }
}