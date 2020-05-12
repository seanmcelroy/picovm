using System.ComponentModel;

namespace picovm.Compiler
{
    public enum Bytecode : byte
    {
        Unknown = 0,

        [Description("END")]
        END = 1,

        [Description("INT")]
        INT = 2,

        [Description("MOV_REG_REG")]
        MOV_REG_REG = 3,
        [Description("MOV_REG_MEM")]
        MOV_REG_MEM = 4,
        [Description("MOV_REG_CON")]
        MOV_REG_CON = 5,

        [Description("MOV_MEM_CON")]
        MOV_MEM_CON = 6,

        [Description("PUSH_REG")]
        PUSH_REG = 10,
        [Description("PUSH_MEM")]
        PUSH_MEM = 11,
        [Description("PUSH_CON")]
        PUSH_CON = 12,

        [Description("POP_REG")]
        POP_REG = 15,
        [Description("POP_MEM")]
        POP_MEM = 16,

        [Description("ADD_MEM_CON")]
        ADD_MEM_CON = 21,

        [Description("ADD_REG_CON")]
        ADD_REG_CON = 22,

        [Description("AND_REG_CON")]
        AND_REG_CON = 23,

        [Description("JZ")]
        JZ = 31,
        [Description("JMP")]
        JMP = 32,

        [Description("XOR_REG_REG")]
        XOR_REG_REG = 40
    }
}
