using System.ComponentModel;

namespace agent_playground
{
    public enum Register : byte
    {
        Unknown = 0,

        [Description("EAX")]
        EAX = 1,
        [Description("AX")]
        AX = 2,
        [Description("AH")]
        AH = 3,
        [Description("AL")]
        AL = 4,

        [Description("EBX")]
        EBX = 10,
        [Description("ESP")]
        ESP = 20
    }
}