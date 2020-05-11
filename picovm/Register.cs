using System.ComponentModel;

namespace picovm
{
    public enum Register : byte
    {
        Unknown = 0,

        [Description("EAX")]
        EAX = 101,
        [Description("AX")]
        AX = 102,
        [Description("AH")]
        AH = 103,
        [Description("AL")]
        AL = 104,

        [Description("EBX")]
        EBX = 105,
        [Description("BX")]
        BX = 106,
        [Description("BH")]
        BH = 107,
        [Description("BL")]
        BL = 108,

        [Description("ECX")]
        ECX = 109,
        [Description("CX")]
        CX = 110,
        [Description("CH")]
        CH = 111,
        [Description("CL")]
        CL = 112,

        [Description("EDX")]
        EDX = 113,
        [Description("DX")]
        DX = 114,
        [Description("DH")]
        DH = 115,
        [Description("DL")]
        DL = 116,

        [Description("ESP")]
        ESP = 120
    }
}