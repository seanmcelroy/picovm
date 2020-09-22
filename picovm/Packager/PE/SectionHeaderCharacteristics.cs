using System;
using System.ComponentModel;

namespace picovm.Packager.PE
{
    [Flags]
    public enum SectionHeaderCharacteristics : UInt32
    {
        /*[ShortName("Reserved_0x00000000")]
        [Description("Reserved for future use.")]
        Reserved_0x00000000 = 0x00000000,*/

        [ShortName("Reserved_0x00000001")]
        [Description("Reserved for future use.")]
        Reserved_0x00000001 = 0x00000001,

        [ShortName("Reserved_0x00000002")]
        [Description("Reserved for future use.")]
        Reserved_0x00000002 = 0x00000002,

        [ShortName("Reserved_0x00000004")]
        [Description("Reserved for future use.")]
        Reserved_0x00000004 = 0x00000004,

        [ShortName("TYPE_NO_PAD")]
        [Description("The section should not be padded to the next boundary.")]
        IMAGE_SCN_TYPE_NO_PAD = 0x00000008,

        [ShortName("Reserved_0x00000010")]
        [Description("Reserved for future use.")]
        Reserved_0x00000010 = 0x00000010,

        [ShortName("CNT_CODE")]
        [Description("The section contains executable code.")]
        IMAGE_SCN_CNT_CODE = 0x00000020,

        [ShortName("CNT_INITIALIZED_DATA")]
        [Description("The section contains initialized data.")]
        IMAGE_SCN_CNT_INITIALIZED_DATA = 0x00000040,

        [ShortName("CNT_UNINITIALIZED_DATA")]
        [Description("The section contains uninitialized data.")]
        IMAGE_SCN_CNT_UNINITIALIZED_DATA = 0x00000080,

        [ShortName("LNK_OTHER")]
        [Description("Reserved for future use.")]
        IMAGE_SCN_LNK_OTHER = 0x00000100,

        [ShortName("LNK_INFO")]
        [Description("The section contains comments or other information. The .drectve section has this type. This is valid for object files only.")]
        IMAGE_SCN_LNK_INFO = 0x00000200,

        [ShortName("Reserved_0x00000400")]
        [Description("Reserved for future use.")]
        Reserved_0x00000400 = 0x00000400,

        [ShortName("LNK_REMOVE")]
        [Description("The section will not become part of the image. This is valid only for object files.")]
        IMAGE_SCN_LNK_REMOVE = 0x00000800,

        [ShortName("LNK_COMDAT")]
        [Description("The section contains COMDAT data.")]
        IMAGE_SCN_LNK_COMDAT = 0x00001000,

        [ShortName("GPREL")]
        [Description("The section contains data referenced through the global pointer (GP).")]
        IMAGE_SCN_GPREL = 0x00008000,

        [ShortName("MEM_PURGEABLE")]
        [Description("Reserved for future use.")]
        IMAGE_SCN_MEM_PURGEABLE = 0x00020000,

        [ShortName("MEM_16BIT")]
        [Description("Reserved for future use.")]
        IMAGE_SCN_MEM_16BIT = 0x00020000,


        [ShortName("MEM_LOCKED")]
        [Description("Reserved for future use.")]
        IMAGE_SCN_MEM_LOCKED = 0x00040000,

        [ShortName("MEM_PRELOAD")]
        [Description("Reserved for future use.")]
        IMAGE_SCN_MEM_PRELOAD = 0x00080000,

        [ShortName("ALIGN_1BYTES")]
        [Description("Align data on a 1-byte boundary. Valid only for object files.")]
        IMAGE_SCN_ALIGN_1BYTES = 0x00100000,

        [ShortName("ALIGN_2BYTES")]
        [Description("Align data on a 2-byte boundary. Valid only for object files.")]
        IMAGE_SCN_ALIGN_2BYTES = 0x00200000,

        [ShortName("ALIGN_4BYTES")]
        [Description("Align data on a 4-byte boundary. Valid only for object files.")]
        IMAGE_SCN_ALIGN_4BYTES = 0x00300000,

        [ShortName("ALIGN_8BYTES")]
        [Description("Align data on an 8-byte boundary. Valid only for object files.")]
        IMAGE_SCN_ALIGN_8BYTES = 0x00400000,

        [ShortName("ALIGN_16BYTES")]
        [Description("Align data on a 16-byte boundary. Valid only for object files.")]
        IMAGE_SCN_ALIGN_16BYTES = 0x00500000,

        [ShortName("ALIGN_32BYTES")]
        [Description("Align data on a 32-byte boundary. Valid only for object files.")]
        IMAGE_SCN_ALIGN_32BYTES = 0x00600000,

        [ShortName("ALIGN_64BYTES")]
        [Description("Align data on a 64-byte boundary. Valid only for object files.")]
        IMAGE_SCN_ALIGN_64BYTES = 0x00700000,

        [ShortName("ALIGN_128BYTES")]
        [Description("Align data on a 128-byte boundary. Valid only for object files.")]
        IMAGE_SCN_ALIGN_128BYTES = 0x00800000,

        [ShortName("ALIGN_256BYTES")]
        [Description("Align data on a 256-byte boundary. Valid only for object files.")]
        IMAGE_SCN_ALIGN_256BYTES = 0x00900000,

        [ShortName("ALIGN_512BYTES")]
        [Description("Align data on a 512-byte boundary. Valid only for object files.")]
        IMAGE_SCN_ALIGN_512BYTES = 0x00A00000,

        [ShortName("ALIGN_1024BYTES")]
        [Description("Align data on a 1024-byte boundary. Valid only for object files.")]
        IMAGE_SCN_ALIGN_1024BYTES = 0x00B00000,

        [ShortName("ALIGN_2048BYTES")]
        [Description("Align data on a 2048-byte boundary. Valid only for object files.")]
        IMAGE_SCN_ALIGN_2048BYTES = 0x00C00000,

        [ShortName("ALIGN_4096BYTES")]
        [Description("Align data on a 4096-byte boundary. Valid only for object files.")]
        IMAGE_SCN_ALIGN_4096BYTES = 0x00D00000,

        [ShortName("ALIGN_8192BYTES")]
        [Description("Align data on an 8192-byte boundary. Valid only for object files.")]
        IMAGE_SCN_ALIGN_8192BYTES = 0x00E00000,

        [ShortName("LNK_NRELOC_OVFL")]
        [Description("The section contains extended relocations.")]
        IMAGE_SCN_LNK_NRELOC_OVFL = 0x01000000,

        [ShortName("MEM_DISCARDABLE")]
        [Description("The section can be discarded as needed.")]
        IMAGE_SCN_MEM_DISCARDABLE = 0x02000000,

        [ShortName("MEM_NOT_CACHED")]
        [Description("The section cannot be cached.")]
        IMAGE_SCN_MEM_NOT_CACHED = 0x04000000,

        [ShortName("MEM_NOT_PAGED")]
        [Description("The section is not pageable.")]
        IMAGE_SCN_MEM_NOT_PAGED = 0x08000000,

        [ShortName("MEM_SHARED")]
        [Description("The section can be shared in memory.")]
        IMAGE_SCN_MEM_SHARED = 0x10000000,

        [ShortName("MEM_EXECUTE")]
        [Description("The section can be executed as code.")]
        IMAGE_SCN_MEM_EXECUTE = 0x20000000,

        [ShortName("MEM_READ")]
        [Description("The section can be read.")]
        IMAGE_SCN_MEM_READ = 0x40000000,

        [ShortName("MEM_WRITE")]
        [Description("The section can be written to.")]
        IMAGE_SCN_MEM_WRITE = 0x80000000,
    }
}