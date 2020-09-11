using System;
using System.ComponentModel;

namespace picovm.Packager.Elf64
{
    public static class SpecialSectionIndexes
    {
        [Description("This value marks an undefined, missing, irrelevant, or otherwise meaningless section reference.  For example, a symbol \"defined\" relative to section number SHN_UNDEF is anundefined symbol.")]
        public const UInt16 SHN_UNDEF = 0;
        public const UInt16 SHN_LORESERVE = 0xff00;
        public const UInt16 SHN_LOPROC = 0xff00;
        public const UInt16 SHN_HIPROC = 0xff1f;
        public const UInt16 SHN_ABS = 0xfff1;
        public const UInt16 SHN_COMMON = 0xfff2;
        public const UInt16 SHN_HIRESERVE = 0xffff;
    }
}
