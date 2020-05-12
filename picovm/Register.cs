using System.ComponentModel;

namespace picovm
{
    public enum Register : byte
    {
        Unknown = 0,

        [Description("RAX")]
        RAX = 100,
        [Description("EAX")]
        EAX = 101,
        [Description("AX")]
        AX = 102,
        [Description("AH")]
        AH = 103,
        [Description("AL")]
        AL = 104,

        [Description("RBX")]
        RBX = 105,

        [Description("EBX")]
        EBX = 106,
        [Description("BX")]
        BX = 107,
        [Description("BH")]
        BH = 108,
        [Description("BL")]
        BL = 109,

        [Description("RCX")]
        RCX = 110,
        [Description("ECX")]
        ECX = 111,
        [Description("CX")]
        CX = 112,
        [Description("CH")]
        CH = 113,
        [Description("CL")]
        CL = 114,

        [Description("RDX")]
        RDX = 115,
        [Description("EDX")]
        EDX = 116,
        [Description("DX")]
        DX = 117,
        [Description("DH")]
        DH = 118,
        [Description("DL")]
        DL = 119,

        [Description("ESP")]
        SP = 120,

        R8 = 208,
        R9 = 209,
        R10 = 210,
        R11 = 211,
        R12 = 212,
        R13 = 213,
        R14 = 214,
        R15 = 215
    }
}