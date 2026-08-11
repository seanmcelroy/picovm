using picovm.Tests.Support;
using picovm.VM;
using Xunit;

namespace picovm.Tests
{
    /// <summary>
    /// Coverage for <c>ADD_INDIRECT_MEMORY_IMMEDIATE</c> (<c>ADD [reg], imm</c>) across every
    /// data width on both agents. Mirrors <see cref="AgentAddImmediateTest"/>'s sentinel
    /// pattern: after the <c>ADD</c> each case runs a <c>JC</c> and a <c>JZ</c>, each writing
    /// a different value into a spare register, so a row only passes if the sum, both flags,
    /// and post-ADD decoding are all correct.
    ///
    /// The assembler infers the immediate/memory width from the pointer register's size (the
    /// immediate has no other signal), so each width test uses a matched-width pointer
    /// register: byte tests use a byte pointer, word tests a word pointer, and so on.  Memory
    /// is seeded at runtime via <c>MOV [ptr], reg</c> rather than a data-section symbol so the
    /// same test shape works on both agents.  The memory result is captured into a witness
    /// register before the flag branches; <c>MOV</c> does not touch flags, so the subsequent
    /// <c>JC</c>/<c>JZ</c> still see the flags from the <c>ADD</c>.
    /// </summary>
    public class AgentAddIndirectMemoryImmediateTest
    {
        // Byte-pointer tests need a cell address that fits in a byte and is above the small
        // text segment these programs compile to (~70 bytes with the sentinel tail).
        private const int ByteCellAddr = 200;
        private const int WordCellAddr = 0x1000;

        private static string[] Program(int cellAddr, string ptrReg, string seedReg, string witnessReg, string v1, string v2) => Asm.Text(
            $"MOV {ptrReg}, {cellAddr}",
            $"MOV {seedReg}, {v1}",
            $"MOV [{ptrReg}], {seedReg}",
            $"ADD [{ptrReg}], {v2}",
            $"MOV {witnessReg}, [{ptrReg}]",
            "JC report_carry",
            "MOV EBX, 100",
            "JMP after_carry",
            "report_carry:",
            "MOV EBX, 200",
            "after_carry:",
            "JZ report_zero",
            "MOV ECX, 100",
            "JMP done",
            "report_zero:",
            "MOV ECX, 200",
            "done:",
            "END");

        [Theory]
        [InlineData("5", "3", (byte)8, false, false)]
        [InlineData("0xFF", "1", (byte)0, true, true)]
        public void Agent32_Byte(string v1, string v2, byte expectedSum, bool expectedCarry, bool expectedZero)
        {
            var agent = MovTestHarness.Run32(Program(ByteCellAddr, "BL", "CL", "DL", v1, v2));

            Assert.Equal(expectedSum, agent.ReadHalfRegister(Register.DL));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }

        [Theory]
        [InlineData("5", "3", (ushort)8, false, false)]
        [InlineData("0xFFFF", "1", (ushort)0, true, true)]
        public void Agent32_Word(string v1, string v2, ushort expectedSum, bool expectedCarry, bool expectedZero)
        {
            var agent = MovTestHarness.Run32(Program(WordCellAddr, "DI", "AX", "DX", v1, v2));

            Assert.Equal(expectedSum, agent.ReadRegister(Register.DX));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }

        [Theory]
        [InlineData("5", "3", 8u, false, false)]
        [InlineData("0xFFFFFFFF", "1", 0u, true, true)]
        public void Agent32_DWord(string v1, string v2, uint expectedSum, bool expectedCarry, bool expectedZero)
        {
            var agent = MovTestHarness.Run32(Program(WordCellAddr, "EDI", "EAX", "EDX", v1, v2));

            Assert.Equal(expectedSum, agent.ReadExtendedRegister(Register.EDX));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }
    }

    /// <summary>64-bit mirror of <see cref="AgentAddIndirectMemoryImmediateTest"/>.</summary>
    public class Agent64AddIndirectMemoryImmediateTest
    {
        private const int ByteCellAddr = 200;
        private const int WordCellAddr = 0x1000;

        private static string[] Program(int cellAddr, string ptrReg, string seedReg, string witnessReg, string v1, string v2) => Asm.Text(
            $"MOV {ptrReg}, {cellAddr}",
            $"MOV {seedReg}, {v1}",
            $"MOV [{ptrReg}], {seedReg}",
            $"ADD [{ptrReg}], {v2}",
            $"MOV {witnessReg}, [{ptrReg}]",
            "JC report_carry",
            "MOV EBX, 100",
            "JMP after_carry",
            "report_carry:",
            "MOV EBX, 200",
            "after_carry:",
            "JZ report_zero",
            "MOV ECX, 100",
            "JMP done",
            "report_zero:",
            "MOV ECX, 200",
            "done:",
            "END");

        [Theory]
        [InlineData("5", "3", (byte)8, false, false)]
        [InlineData("0xFF", "1", (byte)0, true, true)]
        public void Byte(string v1, string v2, byte expectedSum, bool expectedCarry, bool expectedZero)
        {
            var agent = MovTestHarness.Run64(Program(ByteCellAddr, "BL", "CL", "DL", v1, v2));

            Assert.Equal(expectedSum, agent.ReadHalfRegister(Register.DL));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }

        [Theory]
        [InlineData("5", "3", (ushort)8, false, false)]
        [InlineData("0xFFFF", "1", (ushort)0, true, true)]
        public void Word(string v1, string v2, ushort expectedSum, bool expectedCarry, bool expectedZero)
        {
            var agent = MovTestHarness.Run64(Program(WordCellAddr, "DI", "AX", "DX", v1, v2));

            Assert.Equal(expectedSum, agent.ReadRegister(Register.DX));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }

        [Theory]
        [InlineData("5", "3", 8u, false, false)]
        [InlineData("0xFFFFFFFF", "1", 0u, true, true)]
        public void DWord(string v1, string v2, uint expectedSum, bool expectedCarry, bool expectedZero)
        {
            var agent = MovTestHarness.Run64(Program(WordCellAddr, "EDI", "EAX", "EDX", v1, v2));

            Assert.Equal(expectedSum, agent.ReadExtendedRegister(Register.EDX));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }

        [Theory]
        // Baseline: sum fits, no carry, non-zero.
        [InlineData("5", "3", 8ul, false, false)]
        // Full 64-bit wrap: exercises the BigInteger/ulong.MaxValue carry check in the case-8 arm.
        [InlineData("0xFFFFFFFFFFFFFFFF", "1", 0ul, true, true)]
        public void QWord(string v1, string v2, ulong expectedSum, bool expectedCarry, bool expectedZero)
        {
            var agent = MovTestHarness.Run64(Program(WordCellAddr, "RDI", "RAX", "RDX", v1, v2));

            Assert.Equal(expectedSum, agent.ReadR64Register(Register.RDX));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }
    }
}
