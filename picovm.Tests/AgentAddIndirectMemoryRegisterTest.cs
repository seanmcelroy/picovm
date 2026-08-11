using picovm.Tests.Support;
using picovm.VM;
using Xunit;

namespace picovm.Tests
{
    /// <summary>
    /// Coverage for <c>ADD_INDIRECT_MEMORY_REGISTER</c> (<c>ADD [reg], reg</c>) across every
    /// data width on both agents.  Mirrors <see cref="AgentAddImmediateTest"/>'s sentinel
    /// pattern: after the <c>ADD</c> each case runs a <c>JC</c> and a <c>JZ</c>, each writing
    /// a different value into a spare register, so a row only passes if the memory result,
    /// both flags, and post-ADD decoding are all correct.
    ///
    /// Data width here is dictated by the <em>source register</em> (operand2); the pointer
    /// register (operand1) is independent.  That is Bug D: the runtime's <c>_MEMORY_REGISTER</c>
    /// handler used to dispatch data width by the pointer's size and read the source with an
    /// accessor matching the pointer.  The mixed-width theories below (byte data through a
    /// wider pointer, etc.) lock in the fix, which pairs a per-size pointer-read helper with
    /// an outer switch on the source-register width.
    /// </summary>
    public class AgentAddIndirectMemoryRegisterTest
    {
        // Byte-pointer tests need a cell address that fits in a byte and is above the small
        // text segment these programs compile to (~80 bytes with the sentinel tail).
        private const int ByteCellAddr = 200;
        private const int WideCellAddr = 0x1000;

        // v1 is stored at [ptr]; v2 goes into srcReg and is added into memory by the ADD.
        // srcReg and witnessReg must both match the data width; the same srcReg is reused for
        // the initial seed store so no extra register is needed.
        private static string[] Program(int cellAddr, string ptrReg, string srcReg, string witnessReg, string v1, string v2) => Asm.Text(
            $"MOV {ptrReg}, {cellAddr}",
            $"MOV {srcReg}, {v1}",
            $"MOV [{ptrReg}], {srcReg}",
            $"MOV {srcReg}, {v2}",
            $"ADD [{ptrReg}], {srcReg}",
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

        // ---- Same-size baselines ------------------------------------------------------------

        [Theory]
        [InlineData("5", "3", (byte)8, false, false)]
        [InlineData("0xFF", "1", (byte)0, true, true)]
        public void Agent32_Byte_Baseline(string v1, string v2, byte expectedSum, bool expectedCarry, bool expectedZero)
        {
            var agent = MovTestHarness.Run32(Program(ByteCellAddr, "BL", "AL", "DL", v1, v2));

            Assert.Equal(expectedSum, agent.ReadHalfRegister(Register.DL));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }

        [Theory]
        [InlineData("5", "3", (ushort)8, false, false)]
        [InlineData("0xFFFF", "1", (ushort)0, true, true)]
        public void Agent32_Word_Baseline(string v1, string v2, ushort expectedSum, bool expectedCarry, bool expectedZero)
        {
            var agent = MovTestHarness.Run32(Program(WideCellAddr, "DI", "AX", "DX", v1, v2));

            Assert.Equal(expectedSum, agent.ReadRegister(Register.DX));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }

        [Theory]
        [InlineData("5", "3", 8u, false, false)]
        [InlineData("0xFFFFFFFF", "1", 0u, true, true)]
        public void Agent32_DWord_Baseline(string v1, string v2, uint expectedSum, bool expectedCarry, bool expectedZero)
        {
            var agent = MovTestHarness.Run32(Program(WideCellAddr, "EDI", "EAX", "EDX", v1, v2));

            Assert.Equal(expectedSum, agent.ReadExtendedRegister(Register.EDX));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }

        // ---- Mixed-size (Bug-D coverage) ---------------------------------------------------

        [Theory]
        [InlineData("5", "3", (byte)8, false, false)]
        [InlineData("0xFF", "1", (byte)0, true, true)]
        public void Agent32_Byte_DWordPointer(string v1, string v2, byte expectedSum, bool expectedCarry, bool expectedZero)
        {
            // Byte data written through a 4-byte pointer. Handler must dispatch data-op width
            // on the source's byte size, not on the pointer's 4-byte size.
            var agent = MovTestHarness.Run32(Program(WideCellAddr, "EDI", "AL", "DL", v1, v2));

            Assert.Equal(expectedSum, agent.ReadHalfRegister(Register.DL));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }

        [Theory]
        [InlineData("5", "3", (ushort)8, false, false)]
        [InlineData("0xFFFF", "1", (ushort)0, true, true)]
        public void Agent32_Word_DWordPointer(string v1, string v2, ushort expectedSum, bool expectedCarry, bool expectedZero)
        {
            var agent = MovTestHarness.Run32(Program(WideCellAddr, "EDI", "AX", "DX", v1, v2));

            Assert.Equal(expectedSum, agent.ReadRegister(Register.DX));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }
    }

    /// <summary>64-bit mirror of <see cref="AgentAddIndirectMemoryRegisterTest"/>, plus mixed-size rows through a QWord pointer.</summary>
    public class Agent64AddIndirectMemoryRegisterTest
    {
        private const int ByteCellAddr = 200;
        private const int WideCellAddr = 0x1000;

        private static string[] Program(int cellAddr, string ptrReg, string srcReg, string witnessReg, string v1, string v2) => Asm.Text(
            $"MOV {ptrReg}, {cellAddr}",
            $"MOV {srcReg}, {v1}",
            $"MOV [{ptrReg}], {srcReg}",
            $"MOV {srcReg}, {v2}",
            $"ADD [{ptrReg}], {srcReg}",
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

        // ---- Same-size baselines ------------------------------------------------------------

        [Theory]
        [InlineData("5", "3", (byte)8, false, false)]
        [InlineData("0xFF", "1", (byte)0, true, true)]
        public void Byte_Baseline(string v1, string v2, byte expectedSum, bool expectedCarry, bool expectedZero)
        {
            var agent = MovTestHarness.Run64(Program(ByteCellAddr, "BL", "AL", "DL", v1, v2));

            Assert.Equal(expectedSum, agent.ReadHalfRegister(Register.DL));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }

        [Theory]
        [InlineData("5", "3", (ushort)8, false, false)]
        [InlineData("0xFFFF", "1", (ushort)0, true, true)]
        public void Word_Baseline(string v1, string v2, ushort expectedSum, bool expectedCarry, bool expectedZero)
        {
            var agent = MovTestHarness.Run64(Program(WideCellAddr, "DI", "AX", "DX", v1, v2));

            Assert.Equal(expectedSum, agent.ReadRegister(Register.DX));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }

        [Theory]
        [InlineData("5", "3", 8u, false, false)]
        [InlineData("0xFFFFFFFF", "1", 0u, true, true)]
        public void DWord_Baseline(string v1, string v2, uint expectedSum, bool expectedCarry, bool expectedZero)
        {
            var agent = MovTestHarness.Run64(Program(WideCellAddr, "EDI", "EAX", "EDX", v1, v2));

            Assert.Equal(expectedSum, agent.ReadExtendedRegister(Register.EDX));
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
            var agent = MovTestHarness.Run64(Program(WideCellAddr, "RDI", "RAX", "RDX", v1, v2));

            Assert.Equal(expectedSum, agent.ReadR64Register(Register.RDX));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }

        // ---- Mixed-size (Bug-D coverage) ---------------------------------------------------

        [Theory]
        [InlineData("5", "3", (byte)8, false, false)]
        [InlineData("0xFF", "1", (byte)0, true, true)]
        public void Byte_QWordPointer(string v1, string v2, byte expectedSum, bool expectedCarry, bool expectedZero)
        {
            var agent = MovTestHarness.Run64(Program(WideCellAddr, "RDI", "AL", "DL", v1, v2));

            Assert.Equal(expectedSum, agent.ReadHalfRegister(Register.DL));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }

        [Theory]
        [InlineData("5", "3", 8u, false, false)]
        [InlineData("0xFFFFFFFF", "1", 0u, true, true)]
        public void DWord_QWordPointer(string v1, string v2, uint expectedSum, bool expectedCarry, bool expectedZero)
        {
            var agent = MovTestHarness.Run64(Program(WideCellAddr, "RDI", "EAX", "EDX", v1, v2));

            Assert.Equal(expectedSum, agent.ReadExtendedRegister(Register.EDX));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }
    }
}
