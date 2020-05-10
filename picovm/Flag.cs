using System.ComponentModel;

namespace agent_playground
{
    public enum Flag : byte
    {
        Unknown = 0,

        [Description("ZF")]
        ZERO_FLAG = 1
    }
}