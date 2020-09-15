using System.ComponentModel;

namespace picovm.Packager.Elf
{
    public enum HeaderOsAbiVersion : byte
    {
        [Description("UNIX - System V")]
        ELFOSABI_NONE = 0,
        [Description("Hewlett-Packard HP-UX")]
        ELFOSABI_HPUX = 1,
        [Description("NetBSD")]
        ELFOSABI_NETBSD = 2,
        [Description("GNU")]
        ELFOSABI_GNU = 3,
        [Description("Historical, alias for ELFOSABI_GNU")]
        ELFOSABI_LINUX = 3,
        [Description("Sun Solaris")]
        ELFOSABI_SOLARIS = 6,
        [Description("AIX")]
        ELFOSABI_AIX = 7,
        [Description("IRIX")]
        ELFOSABI_IRIX = 8,
        [Description("FreeBSD")]
        ELFOSABI_FREEBSD = 9,
        [Description("Compaq TRU64 UNIX")]
        ELFOSABI_TRU64 = 10,
        [Description("Novell Modesto")]
        ELFOSABI_MODESTO = 11,
        [Description("Open BSD")]
        ELFOSABI_OPENBSD = 12,
        [Description("Open VMS")]
        ELFOSABI_OPENVMS = 13,
        [Description("Hewlett-Packard Non-Stop Kernel")]
        ELFOSABI_NSK = 14,
        [Description("Amiga Research OS")]
        ELFOSABI_AROS = 15,
        [Description("The FenixOS highly scalable multi-core OS")]
        ELFOSABI_FENIXOS = 16,
        [Description("Nuxi CloudABI")]
        ELFOSABI_CLOUDABI = 17,
        [Description("Stratus Technologies OpenVOS")]
        ELFOSABI_OPENVOS = 18,
    }
}