using System.ComponentModel;

namespace picovm.Assembler
{
    public enum Bytecode : byte
    {
        Unknown = 0,

        [Description("END")]
        END = 1,

        [Description("INT")] // Interrupt, can by syscall on x86
        INT = 2,
        [Description("SYSCALL")] // syscall on x64
        SYSCALL = 3,

        /// <summary>
        /// Register-to-register.
        /// dst ← src
        /// </summary>
        /// <example>
        /// Source: MOV EAX, EBX ; eax = ebx
        /// </example>
        [Description("MOV_REGISTER")]
        MOV_REGISTER = 5,

        /// <summary>
        /// Immediate load of a symbol or literal.
        /// This is similar to NASM "address-of" if the source is a symbol address (instead of a literal)
        /// dst ← imm
        /// </summary>
        /// <example>
        /// Source: MOV ECX, msg (or) MOV AX 3
        /// Opcode: dstReg, addr (1 + 4/8 bytes, based on machine address width)
        /// </example>
        [Description("MOV_IMMEDIATE")]
        MOV_IMMEDIATE = 6,

        /// <summary>
        /// Direct load of constant into a symbol resolved at compile time.
        /// </summary>
        /// <example>
        /// Source: MOV [symbol], const
        /// </example>
        [Description("MOV_DIRECT")]
        MOV_DIRECT = 7,

        /// <summary>
        /// Register-indirect load.
        /// This opcode loads from an address that is in a register
        /// dst ← memory[address register].
        /// </summary>
        /// <example>
        /// Source: MOV EAX, [EBX]
        /// Opcode: dstReg, addr (1 + 4/8 bytes, based on machine address width)
        /// </example>
        [Description("MOV_INDIRECT")]
        MOV_INDIRECT = 8,

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

        /// <summary>
        /// Jump if zero (used after a CMP, ZF=1)
        /// </summary>
        [Description("JZ")]
        JZ = 31,

        /// <summary>
        /// Jump if equal (used after a CMP, ZF=1)
        /// </summary>
        [Description("JE")]
        JE = 32,

        /// <summary>
        /// Jump if not zero (used after a CMP, ZF=0)
        /// </summary>
        [Description("JNZ")]
        JNZ = 33,

        /// <summary>
        /// Jump if not equal (used after a CMP, ZF=0)
        /// </summary>
        [Description("JNE")]
        JNE = 34,

        /// <summary>
        /// Jump if overflow (OF=1)
        /// </summary>
        [Description("JO")]
        JO = 35,

        /// <summary>
        /// Jump if not overflow (OF=0)
        /// </summary>
        [Description("JNO")]
        JNO = 36,

        /// <summary>
        /// Jump if sign (SF=1)
        /// </summary>
        [Description("JS")]
        JS = 37,

        /// <summary>
        /// Jump if not sign (SF=0)
        /// </summary>
        [Description("JNS")]
        JNS = 38,

        /// <summary>
        /// Jump if below (CF=1)
        /// </summary>
        [Description("JB")]
        JB = 39,

        /// <summary>
        /// Jump if not above or equal (CF=1)
        /// </summary>
        [Description("JNAE")]
        JNAE = 40,

        /// <summary>
        /// Jump if carry (CF=1)
        /// </summary>
        [Description("JC")]
        JC = 41,

        /// <summary>
        /// Jump if not below (CF=0)
        /// </summary>
        [Description("JNB")]
        JNB = 42,

        /// <summary>
        /// Jump if above or equal (CF=0)
        /// </summary>
        [Description("JAE")]
        JAE = 43,

        /// <summary>
        /// Jump if not carry (CF=0)
        /// </summary>
        [Description("JNC")]
        JNC = 44,

        /// <summary>
        /// Jump if below or equal (CF = 1 or ZF = 1)
        /// </summary>
        [Description("JBE")]
        JBE = 45,

        /// <summary>
        /// Jump if not above (CF = 1 or ZF = 1)
        /// </summary>
        [Description("JNA")]
        JNA = 46,

        /// <summary>
        /// Jump if above (CF=0 and ZF=0)
        /// </summary>
        [Description("JA")]
        JA = 47,

        /// <summary>
        /// Jump if not below or equal (CF=0 and ZF=0)
        /// </summary>
        [Description("JNBE")]
        JNBE = 48,

        /// <summary>
        /// Jump if less (SF <> OF)
        /// </summary>
        [Description("JL")]
        JL = 49,

        /// <summary>
        /// Jump if not greater or equal (SF <> OF)
        /// </summary>
        [Description("JNGE")]
        JNGE = 50,

        /// <summary>
        /// Jump if greater or equal (SF = OF)
        /// </summary>
        [Description("JGE")]
        JGE = 51,

        /// <summary>
        /// Jump if not less (SF = OF)
        /// </summary>
        [Description("JNL")]
        JNL = 52,

        /// <summary>
        /// Jump if less or equal (ZF=1 or SF<>OF)
        /// </summary>
        [Description("JLE")]
        JLE  = 53,

        /// <summary>
        /// Jump if not greater (ZF=1 or SF<>OF)
        /// </summary>
        [Description("JNG")]
        JNG = 54,

        /// <summary>
        /// Jump if greater (ZF=0 and SF=OF)
        /// </summary>
        [Description("JG")]
        JG = 55,

        /// <summary>
        /// Jump if not less or equal (ZF=0 and SF=OF)
        /// </summary>
        [Description("JNLE")]
        JNLE = 56,

        /// <summary>
        /// Jump if parity (PF=1)
        /// </summary>
        [Description("JP")]
        JP = 57,

        /// <summary>
        /// Jump if parity even (PF=1)
        /// </summary>
        [Description("JPE")]
        JPE = 58,

        /// <summary>
        /// Jump if not parity (PF=0)
        /// </summary>
        [Description("JNP")]
        JNP = 59,

        /// <summary>
        /// Jump if parity odd (PF=0)
        /// </summary>
        [Description("JPO")]
        JPO = 60,

        /// <summary>
        /// Jump if %CX register is 0
        /// </summary>
        [Description("JCXZ")]
        JCXZ = 61,

        /// <summary>
        /// Jump if %ECX register is 0
        /// </summary>
        [Description("JECXZ")]
        JECXZ = 62,

        [Description("JMP")]
        JMP = 63,

        [Description("XOR_REG_REG")]
        XOR_REG_REG = 64,

        [Description("CMP_REG_CON")]
        CMP_REG_CON = 65,

        /// <summary>
        /// Call to an address provided by a register value.
        /// </summary>
        /// <example>
        /// Source: CALL EAX
        /// </example>
        [Description("CALL_REGISTER")]
        CALL_REGISTER = 66,

        /// <summary>
        /// Call to an address provided by a symbol or literal.
        /// </summary>
        /// <example>
        /// Source: CALL 23434
        /// Opcode: addr (1 + 4/8 bytes, based on machine address width)
        /// </example>
        [Description("CALL_IMMEDIATE")]
        CALL_IMMEDIATE = 67,

        /// <summary>
        /// Returns by popping a return address into the instruction pointer.
        /// </summary>
        /// <example>
        /// Source: RET
        /// </example>
        [Description("RET")]
        RET = 68,
    }
}
