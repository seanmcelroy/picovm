using System;
using System.ComponentModel;

namespace picovm.Packager.PE
{
    [Flags]
    public enum DllCharacteristics : UInt16
    {
        [ShortName("Reserved_0x0001")]
        [Description("Reserved")]
        Reserved1 = 0x0001,

        [ShortName("Reserved_0x0002")]
        [Description("Reserved")]
        Reserved2 = 0x0002,

        [ShortName("Reserved_0x0004")]
        [Description("Reserved")]
        Reserved4 = 0x0004,

        [ShortName("Reserved_0x0008")]
        [Description("Reserved")]
        Reserved8 = 0x0008,

        [ShortName("HIGH_ENTROPY_VA")]
        [Description("Image can handle a high entropy 64-bit virtual address space.")]
        IMAGE_DLLCHARACTERISTICS_HIGH_ENTROPY_VA = 0x0020,

        [ShortName("DYNAMIC_BASE")]
        [Description("The DLL can be relocated at load time.")]
        IMAGE_DLLCHARACTERISTICS_DYNAMIC_BASE = 0x0040,

        [ShortName("FORCE_INTEGRITY")]
        [Description("Code integrity checks are forced. If you set this flag and a section contains only uninitialized data, set the PointerToRawData member of IMAGE_SECTION_HEADER for that section to zero; otherwise, the image will fail to load because the digital signature cannot be verified.")]
        IMAGE_DLLCHARACTERISTICS_FORCE_INTEGRITY = 0x0080,

        [ShortName("NX_COMPAT")]
        [Description("The image is compatible with data execution prevention (DEP).")]
        IMAGE_DLLCHARACTERISTICS_NX_COMPAT = 0x0100,

        [ShortName("NO_ISOLATION")]
        [Description("The image is isolation aware, but should not be isolated.")]
        IMAGE_DLLCHARACTERISTICS_NO_ISOLATION = 0x0200,

        [ShortName("NO_SEH")]
        [Description("The image does not use structured exception handling (SEH). No handlers can be called in this image.")]
        IMAGE_DLLCHARACTERISTICS_NO_SEH = 0x0400,

        [ShortName("NO_BIND")]
        [Description("Do not bind the image.")]
        IMAGE_DLLCHARACTERISTICS_NO_BIND = 0x0800,

        [ShortName("APPCONTAINER")]
        [Description("Image must execute in an AppContainer.")]
        IMAGE_DLLCHARACTERISTICS_APPCONTAINER = 0x1000,

        [ShortName("WDM_DRIVER")]
        [Description("A WDM driver.")]
        IMAGE_DLLCHARACTERISTICS_WDM_DRIVER = 0x2000,

        [ShortName("GUARD_CF")]
        [Description("Image supports Control Flow Guard.")]
        IMAGE_DLLCHARACTERISTICS_GUARD_CF = 0x4000,

        [ShortName("TERMINAL_SERVER_AWARE")]
        [Description("The image is terminal server aware.")]
        IMAGE_DLLCHARACTERISTICS_TERMINAL_SERVER_AWARE = 0x8000
    }
}