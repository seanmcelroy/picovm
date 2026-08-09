using System.ComponentModel;

namespace picovm.VM
{
    public enum Flag : byte
    {
        [Description("CF")]
        CARRY_FLAG = 0,

        [Description("BRKI")]
        NEC_IO_TRAP = 1,

        [Description("PF")]
        PARITY_FLAG = 2,

        [Description("AF")]
        AUX_CARRY_FLAG = 4,

        [Description("ZF")]
        ZERO_FLAG = 6,

        [Description("SF")]
        SIGN_FLAG = 7,

        [Description("TF")]
        TRAP_FLAG = 8,

        [Description("IF")]
        INTERRUPT_ENABLE_FLAG = 9,

        [Description("DF")]
        DIRECTION_FLAG = 10,

        [Description("OF")]
        OVERFLOW_FLAG = 11
    }
}