using System;
using System.ComponentModel;

namespace picovm.Packager.Elf
{
    public static class SpecialSectionIndexes
    {
        [Description("This value marks an undefined, missing, irrelevant, or otherwise meaningless section reference.  For example, a symbol \"defined\" relative to section number SHN_UNDEF is anundefined symbol.")]
        public const UInt16 SHN_UNDEF = 0;
        [Description("This value specifies the lower bound of the range of reserved indexes.")]
        public const UInt16 SHN_LORESERVE = 0xff00;
        public const UInt16 SHN_LOPROC = 0xff00;
        public const UInt16 SHN_HIPROC = 0xff1f;
        public const UInt16 SHN_LOOS = 0xff20;
        public const UInt16 SHN_HIOS = 0xff3f;
        [Description("This value specifies absolute values for the corresponding reference. For example, symbols defined relative to section number SHN_ABS have absolute values and are not affected by relocation.")]
        public const UInt16 SHN_ABS = 0xfff1;
        [Description("Symbols defined relative to this section are common symbols, such as FORTRAN COMMON or unallocated C external variables.")]
        public const UInt16 SHN_COMMON = 0xfff2;
        public const UInt16 SHN_XINDEX = 0xffff;
        public const UInt16 SHN_HIRESERVE = 0xffff;
    }
}
