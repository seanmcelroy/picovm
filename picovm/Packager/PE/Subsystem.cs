using System;
using System.ComponentModel;

namespace picovm.Packager.PE
{
    public enum Subsystem : UInt16
    {
        [ShortName("Unknown")]
        [Description("Unknown subsystem.")]
        IMAGE_SUBSYSTEM_UNKNOWN = 0,

        [ShortName("None/Native")]
        [Description("No subsystem required (device drivers and native system processes).")]
        IMAGE_SUBSYSTEM_NATIVE = 1,

        [ShortName("Windows GUI")]
        [Description("Windows graphical user interface (GUI) subsystem.")]
        IMAGE_SUBSYSTEM_WINDOWS_GUI = 2,

        [ShortName("Windows Character-mode UI")]
        [Description("Windows character-mode user interface (CUI) subsystem.")]
        IMAGE_SUBSYSTEM_WINDOWS_CUI = 3,

        [ShortName("OS/2 CUI")]
        [Description("OS/2 CUI subsystem.")]
        IMAGE_SUBSYSTEM_OS2_CUI = 5,

        [ShortName("POSIX CUI")]
        [Description("POSIX CUI subsystem.")]
        IMAGE_SUBSYSTEM_POSIX_CUI = 7,

        [ShortName("WINDOWS CE")]
        [Description("Windows CE system.")]
        IMAGE_SUBSYSTEM_WINDOWS_CE_GUI = 9,

        [ShortName("EFI App")]
        [Description("Extensible Firmware Interface (EFI) application.")]
        IMAGE_SUBSYSTEM_EFI_APPLICATION = 10,

        [ShortName("EFI boot driver")]
        [Description("EFI driver with boot services.")]
        IMAGE_SUBSYSTEM_EFI_BOOT_SERVICE_DRIVER = 11,

        [ShortName("EFI run-time driver")]
        [Description("EFI driver with run-time services.")]
        IMAGE_SUBSYSTEM_EFI_RUNTIME_DRIVER = 12,


        [ShortName("EFI ROM")]
        [Description("EFI ROM image.")]
        IMAGE_SUBSYSTEM_EFI_ROM = 13,

        [ShortName("Xbox")]
        [Description("Xbox system.")]
        IMAGE_SUBSYSTEM_XBOX = 14,

        [ShortName("Boot application")]
        [Description("Boot application")]
        IMAGE_SUBSYSTEM_WINDOWS_BOOT_APPLICATION = 16
    }
}