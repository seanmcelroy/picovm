using picovm.Tests.Support;
using picovm.VM;
using Xunit;

namespace picovm.Tests
{
    /// <summary>
    /// Coverage for <c>ADD_IMMEDIATE</c> (<c>ADD reg, imm</c>) across every register width, on
    /// both agents. Every case appends a branch on <c>CF</c> and a branch on <c>ZF</c> after
    /// the <c>ADD</c>, each writing a different sentinel into a spare register. That means a
    /// case only passes if the sum is right, both flags are right, <em>and</em> decoding
    /// continued correctly afterwards -- exactly the combination that hid the previous bugs
    /// here (a wrong flag, and a stray instruction-pointer increment that corrupted decoding
    /// of everything after the ADD).
    /// </summary>
    public class AgentAddImmediateTest
    {
        private static string[] Program(string reg, string v1, string v2) => Asm.Text(
            $"MOV {reg}, {v1}",
            $"ADD {reg}, {v2}",
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
        public void AL(string v1, string v2, byte expectedSum, bool expectedCarry, bool expectedZero)
        {
            var agent = MovTestHarness.Run32(Program("AL", v1, v2));

            Assert.Equal(expectedSum, agent.ReadHalfRegister(Register.AL));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }

        [Theory]
        [InlineData("5", "3", (ushort)8, false, false)]
        [InlineData("0xFFFF", "1", (ushort)0, true, true)]
        public void AX(string v1, string v2, ushort expectedSum, bool expectedCarry, bool expectedZero)
        {
            var agent = MovTestHarness.Run32(Program("AX", v1, v2));

            Assert.Equal(expectedSum, agent.ReadRegister(Register.AX));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }

        [Theory]
        [InlineData("5", "3", 8u, false, false)]
        [InlineData("0xFFFFFFFF", "1", 0u, true, true)]
        public void EAX_32Bit(string v1, string v2, uint expectedSum, bool expectedCarry, bool expectedZero)
        {
            var agent = MovTestHarness.Run32(Program("EAX", v1, v2));

            Assert.Equal(expectedSum, agent.ReadExtendedRegister(Register.EAX));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }
    }

    /// <summary>64-bit mirror of <see cref="AgentAddImmediateTest"/>, plus the RAX-width case.</summary>
    public class Agent64AddImmediateTest
    {
        private static string[] Program(string reg, string v1, string v2) => Asm.Text(
            $"MOV {reg}, {v1}",
            $"ADD {reg}, {v2}",
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
        public void AL(string v1, string v2, byte expectedSum, bool expectedCarry, bool expectedZero)
        {
            var agent = MovTestHarness.Run64(Program("AL", v1, v2));

            Assert.Equal(expectedSum, agent.ReadHalfRegister(Register.AL));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }

        [Theory]
        [InlineData("5", "3", (ushort)8, false, false)]
        [InlineData("0xFFFF", "1", (ushort)0, true, true)]
        public void AX(string v1, string v2, ushort expectedSum, bool expectedCarry, bool expectedZero)
        {
            var agent = MovTestHarness.Run64(Program("AX", v1, v2));

            Assert.Equal(expectedSum, agent.ReadRegister(Register.AX));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }

        [Theory]
        [InlineData("5", "3", 8u, false, false)]
        [InlineData("0xFFFFFFFF", "1", 0u, true, true)]
        public void EAX(string v1, string v2, uint expectedSum, bool expectedCarry, bool expectedZero)
        {
            var agent = MovTestHarness.Run64(Program("EAX", v1, v2));

            Assert.Equal(expectedSum, agent.ReadExtendedRegister(Register.EAX));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }

        [Theory]
        [InlineData("5", "3", 8ul, false, false)]
        [InlineData("0xFFFFFFFFFFFFFFFF", "1", 0ul, true, true)]
        public void RAX(string v1, string v2, ulong expectedSum, bool expectedCarry, bool expectedZero)
        {
            var agent = MovTestHarness.Run64(Program("RAX", v1, v2));

            Assert.Equal(expectedSum, agent.ReadR64Register(Register.RAX));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }
    }
}
