using picovm.Tests.Support;
using picovm.VM;
using Xunit;

namespace picovm.Tests
{
    /// <summary>
    /// Coverage for <c>ADD_INDIRECT_REGISTER</c> (<c>ADD reg, [reg]</c>) across every data
    /// width on both agents. Mirrors <see cref="AgentAddImmediateTest"/>'s sentinel pattern:
    /// after the <c>ADD</c> each case runs a <c>JC</c> and a <c>JZ</c>, each writing a
    /// different value into a spare register, so a row only passes if the sum, both flags,
    /// and post-ADD decoding are all correct.
    ///
    /// Unlike the memory-destination-immediate form, data width here is dictated by the
    /// <em>destination register</em> (operand1); the pointer register (operand2) is
    /// independent.  That is Bug D: earlier the runtime dispatched the pointer accessor by
    /// operand1's data width, so <c>ADD AL, [EBX]</c> crashed because the byte arm called
    /// <c>ReadHalfRegister(EBX)</c>.  The mixed-width theories below (byte data with a wider
    /// pointer, etc.) exist specifically to lock that fix in.
    /// </summary>
    public class AgentAddIndirectRegisterTest
    {
        // Byte-pointer tests need a cell address that fits in a byte and is above the small
        // text segment these programs compile to (~80 bytes with the sentinel tail).
        private const int ByteCellAddr = 200;
        private const int WideCellAddr = 0x1000;

        // v1 initialises the destination register; v2 is stored at [ptr] and added in by the ADD.
        // seedReg must match the data width so v2 is stored without truncation.
        private static string[] Program(int cellAddr, string destReg, string ptrReg, string seedReg, string v1, string v2) => Asm.Text(
            $"MOV {ptrReg}, {cellAddr}",
            $"MOV {seedReg}, {v2}",
            $"MOV [{ptrReg}], {seedReg}",
            $"MOV {destReg}, {v1}",
            $"ADD {destReg}, [{ptrReg}]",
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

        // ---- Same-size baselines ------------------------------------------------------------

        [Theory]
        [InlineData("5", "3", (byte)8, false, false)]
        [InlineData("0xFF", "1", (byte)0, true, true)]
        public void Agent32_Byte_Baseline(string v1, string v2, byte expectedSum, bool expectedCarry, bool expectedZero)
        {
            var agent = MovTestHarness.Run32(Program(ByteCellAddr, "AL", "BL", "CL", v1, v2));

            Assert.Equal(expectedSum, agent.ReadHalfRegister(Register.AL));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }

        [Theory]
        [InlineData("5", "3", (ushort)8, false, false)]
        [InlineData("0xFFFF", "1", (ushort)0, true, true)]
        public void Agent32_Word_Baseline(string v1, string v2, ushort expectedSum, bool expectedCarry, bool expectedZero)
        {
            var agent = MovTestHarness.Run32(Program(WideCellAddr, "AX", "DI", "SI", v1, v2));

            Assert.Equal(expectedSum, agent.ReadRegister(Register.AX));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }

        [Theory]
        [InlineData("5", "3", 8u, false, false)]
        [InlineData("0xFFFFFFFF", "1", 0u, true, true)]
        public void Agent32_DWord_Baseline(string v1, string v2, uint expectedSum, bool expectedCarry, bool expectedZero)
        {
            var agent = MovTestHarness.Run32(Program(WideCellAddr, "EAX", "EDI", "ESI", v1, v2));

            Assert.Equal(expectedSum, agent.ReadExtendedRegister(Register.EAX));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }

        // ---- Mixed-size (Bug-D coverage) ---------------------------------------------------

        [Theory]
        [InlineData("5", "3", (byte)8, false, false)]
        [InlineData("0xFF", "1", (byte)0, true, true)]
        public void Agent32_Byte_DWordPointer(string v1, string v2, byte expectedSum, bool expectedCarry, bool expectedZero)
        {
            // Byte data through a 4-byte pointer. Byte arm must dispatch operand2 accessor by
            // operand2's own size (helper), not by operand1's byte width.
            var agent = MovTestHarness.Run32(Program(WideCellAddr, "AL", "EDI", "CL", v1, v2));

            Assert.Equal(expectedSum, agent.ReadHalfRegister(Register.AL));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }

        [Theory]
        [InlineData("5", "3", (ushort)8, false, false)]
        [InlineData("0xFFFF", "1", (ushort)0, true, true)]
        public void Agent32_Word_DWordPointer(string v1, string v2, ushort expectedSum, bool expectedCarry, bool expectedZero)
        {
            var agent = MovTestHarness.Run32(Program(WideCellAddr, "AX", "EDI", "SI", v1, v2));

            Assert.Equal(expectedSum, agent.ReadRegister(Register.AX));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }
    }

    /// <summary>64-bit mirror of <see cref="AgentAddIndirectRegisterTest"/>, plus mixed-size rows through a QWord pointer.</summary>
    public class Agent64AddIndirectRegisterTest
    {
        private const int ByteCellAddr = 200;
        private const int WideCellAddr = 0x1000;

        private static string[] Program(int cellAddr, string destReg, string ptrReg, string seedReg, string v1, string v2) => Asm.Text(
            $"MOV {ptrReg}, {cellAddr}",
            $"MOV {seedReg}, {v2}",
            $"MOV [{ptrReg}], {seedReg}",
            $"MOV {destReg}, {v1}",
            $"ADD {destReg}, [{ptrReg}]",
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

        // ---- Same-size baselines ------------------------------------------------------------

        [Theory]
        [InlineData("5", "3", (byte)8, false, false)]
        [InlineData("0xFF", "1", (byte)0, true, true)]
        public void Byte_Baseline(string v1, string v2, byte expectedSum, bool expectedCarry, bool expectedZero)
        {
            var agent = MovTestHarness.Run64(Program(ByteCellAddr, "AL", "BL", "CL", v1, v2));

            Assert.Equal(expectedSum, agent.ReadHalfRegister(Register.AL));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }

        [Theory]
        [InlineData("5", "3", (ushort)8, false, false)]
        [InlineData("0xFFFF", "1", (ushort)0, true, true)]
        public void Word_Baseline(string v1, string v2, ushort expectedSum, bool expectedCarry, bool expectedZero)
        {
            var agent = MovTestHarness.Run64(Program(WideCellAddr, "AX", "DI", "SI", v1, v2));

            Assert.Equal(expectedSum, agent.ReadRegister(Register.AX));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }

        [Theory]
        [InlineData("5", "3", 8u, false, false)]
        [InlineData("0xFFFFFFFF", "1", 0u, true, true)]
        public void DWord_Baseline(string v1, string v2, uint expectedSum, bool expectedCarry, bool expectedZero)
        {
            var agent = MovTestHarness.Run64(Program(WideCellAddr, "EAX", "EDI", "ESI", v1, v2));

            Assert.Equal(expectedSum, agent.ReadExtendedRegister(Register.EAX));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }

        [Theory]
        // Baseline: sum fits, no carry, non-zero.
        [InlineData("5", "3", 8ul, false, false)]
        // Full 64-bit wrap: exercises the BigInteger/ulong.MaxValue carry check in the case-8 arm.
        [InlineData("0xFFFFFFFFFFFFFFFF", "1", 0ul, true, true)]
        public void QWord_Baseline(string v1, string v2, ulong expectedSum, bool expectedCarry, bool expectedZero)
        {
            var agent = MovTestHarness.Run64(Program(WideCellAddr, "RAX", "RDI", "RSI", v1, v2));

            Assert.Equal(expectedSum, agent.ReadR64Register(Register.RAX));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }

        // ---- Mixed-size (Bug-D coverage) ---------------------------------------------------

        [Theory]
        [InlineData("5", "3", (byte)8, false, false)]
        [InlineData("0xFF", "1", (byte)0, true, true)]
        public void Byte_QWordPointer(string v1, string v2, byte expectedSum, bool expectedCarry, bool expectedZero)
        {
            var agent = MovTestHarness.Run64(Program(WideCellAddr, "AL", "RDI", "CL", v1, v2));

            Assert.Equal(expectedSum, agent.ReadHalfRegister(Register.AL));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }

        [Theory]
        [InlineData("5", "3", 8u, false, false)]
        [InlineData("0xFFFFFFFF", "1", 0u, true, true)]
        public void DWord_QWordPointer(string v1, string v2, uint expectedSum, bool expectedCarry, bool expectedZero)
        {
            var agent = MovTestHarness.Run64(Program(WideCellAddr, "EAX", "RDI", "ESI", v1, v2));

            Assert.Equal(expectedSum, agent.ReadExtendedRegister(Register.EAX));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }
    }
}
