using System;
using System.ComponentModel;

namespace picovm.Packager.PE
{
    public enum MachineType : UInt16
    {
        [Description("Applicable to any machine type")]
        IMAGE_FILE_MACHINE_UNKNOWN = 0,

        [Description("Matsushita AM33")]
        IMAGE_FILE_MACHINE_AM33 = 0x1d3,

        [Description("x64")]
        IMAGE_FILE_MACHINE_AMD64 = 0x8664,

        [Description("ARM little endian")]
        IMAGE_FILE_MACHINE_ARM = 0x1c0,

        [Description("ARM64 little endian")]
        IMAGE_FILE_MACHINE_ARM64 = 0xaa64,

        [Description("ARM Thumb-2 little endian")]
        IMAGE_FILE_MACHINE_ARMNT = 0x1c4,

        [Description("EFI byte code")]
        IMAGE_FILE_MACHINE_EBC = 0xebc,

        [Description("Intel 386 or later processors and compatible processors")]
        IMAGE_FILE_MACHINE_I386 = 0x14c,

        [Description("Intel Itanium processor family")]
        IMAGE_FILE_MACHINE_IA64 = 0x200,

        [Description("Mitsubishi M32R little endian")]
        IMAGE_FILE_MACHINE_M32R = 0x9041,

        [Description("MIPS16")]
        IMAGE_FILE_MACHINE_MIPS16 = 0x266,

        [Description("MIPS with FPU")]
        IMAGE_FILE_MACHINE_MIPSFPU = 0x366,

        [Description("MIPS16 with FPU")]
        IMAGE_FILE_MACHINE_MIPSFPU16 = 0x466,

        [Description("Power PC little endian")]
        IMAGE_FILE_MACHINE_POWERPC = 0x1f0,

        [Description("Power PC with floating point support")]
        IMAGE_FILE_MACHINE_POWERPCFP = 0x1f1,

        [Description("MIPS little endian")]
        IMAGE_FILE_MACHINE_R4000 = 0x166,

        [Description("RISC-V 32-bit address space")]
        IMAGE_FILE_MACHINE_RISCV32 = 0x5032,

        [Description("RISC-V 64-bit address space")]
        IMAGE_FILE_MACHINE_RISCV64 = 0x5064,

        [Description("RISC-V 128-bit address space")]
        IMAGE_FILE_MACHINE_RISCV128 = 0x5128,

        [Description("Hitachi SH3")]
        IMAGE_FILE_MACHINE_SH3 = 0x1a2,

        [Description("Hitachi SH3 DSP")]
        IMAGE_FILE_MACHINE_SH3DSP = 0x1a3,

        [Description("Hitachi SH4")]
        IMAGE_FILE_MACHINE_SH4 = 0x1a6,

        [Description("Hitachi SH5")]
        IMAGE_FILE_MACHINE_SH5 = 0x1a8,

        [Description("Thumb")]
        IMAGE_FILE_MACHINE_THUMB = 0x1c2,

        [Description("MIPS little-endian WCE v2")]
        IMAGE_FILE_MACHINE_WCEMIPSV2 = 0x169,
    }
}