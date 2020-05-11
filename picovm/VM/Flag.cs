using System.ComponentModel;

namespace picovm.VM
{
    public enum Flag : byte
    {
        Unknown = 0,

        [Description("ZF")]
        ZERO_FLAG = 1
    }
}