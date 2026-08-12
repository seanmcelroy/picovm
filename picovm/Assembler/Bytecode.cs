using System.ComponentModel;

namespace picovm.Assembler
{
    public enum Bytecode : byte
    {
        Unknown = 0,

        [Description("END")]
        END,

        [Description("INT")] // Interrupt, can by syscall on x86
        INT,
        [Description("SYSCALL")] // syscall on x64
        SYSCALL,

        /// <summary>
        /// Register-to-register.
        /// dst ← src
        /// </summary>
        /// <example>
        /// Source: MOV reg, reg
        /// </example>
        [Description("MOV_REGISTER")]
        MOV_REGISTER,

        /// <summary>
        /// Immediate load of a symbol or literal.
        /// This is similar to NASM "address-of" if the source is a symbol address (instead of a literal)
        /// dst ← imm
        /// </summary>
        /// <example>
        /// Source: MOV reg, symbol (or) MOV reg const
        /// Opcode: dstReg, addr (1 + 4/8 bytes, based on machine address width)
        /// </example>
        [Description("MOV_IMMEDIATE")]
        MOV_IMMEDIATE,

        /// <summary>
        /// Direct load of a symbol resolved at compile time into a register.
        /// </summary>
        /// <example>
        /// Source: MOV reg, [symbol]
        /// </example>
        [Description("MOV_DIRECT_LOAD")]
        MOV_DIRECT_LOAD,

        /// <summary>
        /// Direct load of register value into a symbol resolved at compile time.
        /// </summary>
        /// <example>
        /// Source: MOV [symbol], reg
        /// </example>
        [Description("MOV_DIRECT_STORE")]
        MOV_DIRECT_STORE,

        /// <summary>
        /// Direct load of constant into a symbol resolved at compile time.
        /// </summary>
        /// <example>
        /// Source: MOV [symbol], const
        /// </example>
        [Description("MOV_DIRECT_IMMEDIATE")]
        MOV_DIRECT_IMMEDIATE,

        /// <summary>
        /// Register-indirect load.
        /// This opcode loads from an address that is in a register
        /// </summary>
        /// <example>
        /// Source: MOV reg, [reg]
        /// Opcode: dstReg, addr (1 + 4/8 bytes, based on machine address width)
        /// </example>
        [Description("MOV_INDIRECT_LOAD")]
        MOV_INDIRECT_LOAD,

        /// <summary>
        /// Register-indirect store from register.
        /// This opcode stores to an address in a register
        /// </summary>
        /// <example>
        /// Source: MOV [reg], reg
        /// Opcode: dstAddr (1 + 4/8 bytes, based on machine address width), srcReg
        /// </example>
        [Description("MOV_INDIRECT_STORE_REGISTER")]
        MOV_INDIRECT_STORE_REGISTER,

        /// <summary>
        /// Register-indirect store from immediate.
        /// This opcode stores to an address in a register
        /// </summary>
        /// <example>
        /// Source: MOV [reg], const
        /// </example>
        [Description("MOV_INDIRECT_STORE_IMMEDIATE")]
        MOV_INDIRECT_STORE_IMMEDIATE,


        [Description("PUSH_REG")]
        PUSH_REG,
        [Description("PUSH_MEM")]
        PUSH_MEM,
        [Description("PUSH_CON")]
        PUSH_CON,

        [Description("POP_REG")]
        POP_REG,
        [Description("POP_MEM")]
        POP_MEM,

        /// <summary>
        /// This adds the value in the second register to the value in
        /// the first, and stores the result back in the first. Both
        /// operands are registers, only operating directly on the register contents.
        /// </summary>
        /// <example>
        /// Source: ADD reg, reg
        /// </example>
        [Description("ADD_REGISTER")]
        ADD_REGISTER,

        /// <summary>
        /// This adds the value in the second operand (a memory location)
        /// to the register noted in the first operand.
        /// It adds them together, then writes the result
        /// back to that register.
        /// </summary>
        /// <example>
        /// Source: ADD reg, [reg]
        /// </example>
        [Description("ADD_INDIRECT_REGISTER")]
        ADD_INDIRECT_REGISTER,

        /// <summary>
        /// This adds the value in the second operand (a register)
        /// to the memory location who address is stored in the first
        /// operand.  It adds them together, then writes the result
        /// back to that memory address.
        /// </summary>
        /// <example>
        /// Source: ADD [reg], reg
        /// </example>
        [Description("ADD_INDIRECT_MEMORY_REGISTER")]
        ADD_INDIRECT_MEMORY_REGISTER,

        /// <summary>
        /// This adds the value in the second operand (a literal)
        /// to the memory location who address is stored in the first
        /// operand.  It adds them together, then writes the result
        /// back to that memory address.
        /// </summary>
        /// <example>
        /// Source: ADD [reg], const
        /// </example>
        [Description("ADD_INDIRECT_MEMORY_IMMEDIATE")]
        ADD_INDIRECT_MEMORY_IMMEDIATE,

        /// <summary>
        /// This adds the literal value in the second operand to the value in
        /// the first (a register), and stores the result back in the first.
        /// </summary>
        /// <example>
        /// Source: ADD reg, const
        /// </example>
        [Description("ADD_IMMEDIATE")]
        ADD_IMMEDIATE,

        /// <summary>
        /// Direct load of a symbol resolved at compile time
        /// which is added to the value in a register and stored back into it.
        /// </summary>
        /// <example>
        /// Source: ADD reg, [symbol]
        /// </example>
        [Description("ADD_DIRECT_LOAD")]
        ADD_DIRECT_LOAD,

        /// <summary>
        /// Direct load of register value which is added to the value
        /// stored in a symbol resolved at compile time, then
        /// written back into that address.
        /// </summary>
        /// <example>
        /// Source: ADD [symbol], reg
        /// </example>
        [Description("ADD_DIRECT_STORE")]
        ADD_DIRECT_STORE,

        /// <summary>
        /// Direct load of constant which is added to the value
        /// stored in a symbol resolved at compile time, then
        /// written back into that address.
        /// </summary>
        /// <example>
        /// Source: ADD [symbol], const
        /// </example>
        [Description("ADD_DIRECT_IMMEDIATE")]
        ADD_DIRECT_IMMEDIATE,

        /// <summary>
        /// Bitwise AND of two registers
        /// </summary>
        /// <example>
        /// Source: AND reg, reg
        /// </example>
        /// <seealso cref="TEST_REGISTER"/>
        [Description("AND_REGISTER")]
        AND_REGISTER,

        /// <summary>
        /// Bitwise AND of a register and an immediate
        /// </summary>
        /// <example>
        /// Source: AND reg, const
        /// </example>
        /// <seealso cref="TEST_IMMEDIATE"/>
        [Description("AND_IMMEDIATE")]
        AND_IMMEDIATE,

        /// <summary>
        /// Bitwise AND of two registers,
        /// but the result is not written back, only
        /// logic status flags are updated
        /// </summary>
        /// <example>
        /// Source: TEST reg, reg
        /// </example>
        /// <seealso cref="AND_REGISTER"/>
        [Description("TEST_REGISTER")]
        TEST_REGISTER,

        /// <summary>
        /// Bitwise AND of a register and an immediate,
        /// but the result is not written back, only
        /// logic status flags are updated
        /// </summary>
        /// <example>
        /// Source: TEST reg, const
        /// </example>
        /// <seealso cref="AND_IMMEDIATE"/>
        [Description("TEST_IMMEDIATE")]
        TEST_IMMEDIATE,

        /// <summary>
        /// Jump if zero (used after a CMP, ZF=1)
        /// </summary>
        [Description("JZ")]
        JZ,

        /// <summary>
        /// Jump if equal (used after a CMP, ZF=1)
        /// </summary>
        [Description("JE")]
        JE,

        /// <summary>
        /// Jump if not zero (used after a CMP, ZF=0)
        /// </summary>
        [Description("JNZ")]
        JNZ,

        /// <summary>
        /// Jump if not equal (used after a CMP, ZF=0)
        /// </summary>
        [Description("JNE")]
        JNE,

        /// <summary>
        /// Jump if overflow (OF=1)
        /// </summary>
        [Description("JO")]
        JO,

        /// <summary>
        /// Jump if not overflow (OF=0)
        /// </summary>
        [Description("JNO")]
        JNO,

        /// <summary>
        /// Jump if sign (SF=1)
        /// </summary>
        [Description("JS")]
        JS,

        /// <summary>
        /// Jump if not sign (SF=0)
        /// </summary>
        [Description("JNS")]
        JNS,

        /// <summary>
        /// Jump if below (CF=1)
        /// </summary>
        [Description("JB")]
        JB,

        /// <summary>
        /// Jump if not above or equal (CF=1)
        /// </summary>
        [Description("JNAE")]
        JNAE,

        /// <summary>
        /// Jump if carry (CF=1)
        /// </summary>
        [Description("JC")]
        JC,

        /// <summary>
        /// Jump if not below (CF=0)
        /// </summary>
        [Description("JNB")]
        JNB,

        /// <summary>
        /// Jump if above or equal (CF=0)
        /// </summary>
        [Description("JAE")]
        JAE,

        /// <summary>
        /// Jump if not carry (CF=0)
        /// </summary>
        [Description("JNC")]
        JNC,

        /// <summary>
        /// Jump if below or equal (CF=1 or ZF=1)
        /// </summary>
        [Description("JBE")]
        JBE,

        /// <summary>
        /// Jump if not above (CF=1 or ZF=1)
        /// </summary>
        [Description("JNA")]
        JNA,

        /// <summary>
        /// Jump if above (CF=0 and ZF=0)
        /// </summary>
        [Description("JA")]
        JA,

        /// <summary>
        /// Jump if not below or equal (CF=0 and ZF=0)
        /// </summary>
        [Description("JNBE")]
        JNBE,

        /// <summary>
        /// Jump if less (SF <> OF)
        /// </summary>
        [Description("JL")]
        JL,

        /// <summary>
        /// Jump if not greater or equal (SF <> OF)
        /// </summary>
        [Description("JNGE")]
        JNGE,

        /// <summary>
        /// Jump if greater or equal (SF = OF)
        /// </summary>
        [Description("JGE")]
        JGE,

        /// <summary>
        /// Jump if not less (SF = OF)
        /// </summary>
        [Description("JNL")]
        JNL,

        /// <summary>
        /// Jump if less or equal (ZF=1 or SF<>OF)
        /// </summary>
        [Description("JLE")]
        JLE ,

        /// <summary>
        /// Jump if not greater (ZF=1 or SF<>OF)
        /// </summary>
        [Description("JNG")]
        JNG,

        /// <summary>
        /// Jump if greater (ZF=0 and SF=OF)
        /// </summary>
        [Description("JG")]
        JG,

        /// <summary>
        /// Jump if not less or equal (ZF=0 and SF=OF)
        /// </summary>
        [Description("JNLE")]
        JNLE,

        /// <summary>
        /// Jump if parity (PF=1)
        /// </summary>
        [Description("JP")]
        JP,

        /// <summary>
        /// Jump if parity even (PF=1)
        /// </summary>
        [Description("JPE")]
        JPE,

        /// <summary>
        /// Jump if not parity (PF=0)
        /// </summary>
        [Description("JNP")]
        JNP,

        /// <summary>
        /// Jump if parity odd (PF=0)
        /// </summary>
        [Description("JPO")]
        JPO,

        /// <summary>
        /// Jump if %CX register is 0
        /// </summary>
        [Description("JCXZ")]
        JCXZ,

        /// <summary>
        /// Jump if %ECX register is 0
        /// </summary>
        [Description("JECXZ")]
        JECXZ,

        [Description("JMP")]
        JMP,

        [Description("XOR_REG_REG")]
        XOR_REG_REG,

        /// <summary>
        /// Compares two register values
        /// </summary>
        /// <example>
        /// Source: CMP reg, reg
        /// </example>
        /// <remarks>
        /// This is essential for any look comparing against a variable
        /// </remarks>
        [Description("CMP_REGISTER")]
        CMP_REGISTER,

        /// <summary>
        /// Compares a register value with an immediate
        /// </summary>
        /// <example>
        /// Source: CMP reg, const
        /// </example>
        [Description("CMP_IMMEDIATE")]
        CMP_IMMEDIATE,

        /// <summary>
        /// Call to an address provided by a register value.
        /// </summary>
        /// <example>
        /// Source: CALL reg
        /// </example>
        [Description("CALL_REGISTER")]
        CALL_REGISTER,

        /// <summary>
        /// Call to an address provided by a symbol or literal.
        /// </summary>
        /// <example>
        /// Source: CALL const
        /// Opcode: addr (1 + 4/8 bytes, based on machine address width)
        /// </example>
        [Description("CALL_IMMEDIATE")]
        CALL_IMMEDIATE,

        /// <summary>
        /// Returns by popping a return address into the instruction pointer.
        /// </summary>
        /// <example>
        /// Source: RET
        /// </example>
        [Description("RET")]
        RET,
    }
}
