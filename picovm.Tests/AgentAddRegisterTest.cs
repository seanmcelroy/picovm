using picovm.Tests.Support;
using picovm.VM;
using Xunit;

namespace picovm.Tests
{
    /// <summary>
    /// Coverage for <c>ADD_REGISTER</c> (<c>ADD reg, reg</c>) across every register width on
    /// both agents. Mirrors <see cref="AgentAddRegConTest"/>: sets both operands via <c>MOV</c>,
    /// runs the <c>ADD</c>, then branches on <c>CF</c> and <c>ZF</c> to sentinel values so a
    /// case only passes if the sum is right, both flags are right, <em>and</em> decoding
    /// continued correctly afterwards. Destination is always an <c>A</c>-family register so
    /// the <c>EBX</c>/<c>ECX</c> sentinel writes cannot clobber the result being asserted.
    /// </summary>
    public class AgentAddRegisterTest
    {
        private static string[] Program(string dest, string src, string v1, string v2) => Asm.Text(
            $"MOV {dest}, {v1}",
            $"MOV {src}, {v2}",
            $"ADD {dest}, {src}",
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
        public void AL_BL(string v1, string v2, byte expectedSum, bool expectedCarry, bool expectedZero)
        {
            var agent = MovTestHarness.Run32(Program("AL", "BL", v1, v2));

            Assert.Equal(expectedSum, agent.ReadHalfRegister(Register.AL));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }

        [Theory]
        [InlineData("5", "3", (ushort)8, false, false)]
        [InlineData("0xFFFF", "1", (ushort)0, true, true)]
        public void AX_BX(string v1, string v2, ushort expectedSum, bool expectedCarry, bool expectedZero)
        {
            var agent = MovTestHarness.Run32(Program("AX", "BX", v1, v2));

            Assert.Equal(expectedSum, agent.ReadRegister(Register.AX));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }

        [Theory]
        [InlineData("5", "3", 8u, false, false)]
        [InlineData("0xFFFFFFFF", "1", 0u, true, true)]
        public void EAX_EBX_32Bit(string v1, string v2, uint expectedSum, bool expectedCarry, bool expectedZero)
        {
            var agent = MovTestHarness.Run32(Program("EAX", "EBX", v1, v2));

            Assert.Equal(expectedSum, agent.ReadExtendedRegister(Register.EAX));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }
    }

    /// <summary>
    /// 64-bit mirror of <see cref="AgentAddRegisterTest"/>, plus the <c>RAX+RBX</c> case that
    /// specifically exercises the fixes to the <c>case 8:</c> arm of
    /// <see cref="picovm.VM.Agent64.Tick"/>: the inner size label, the 64-bit signed
    /// reinterpret, and the <see cref="System.Numerics.BigInteger"/>-based carry check.
    /// </summary>
    public class Agent64AddRegisterTest
    {
        private static string[] Program(string dest, string src, string v1, string v2) => Asm.Text(
            $"MOV {dest}, {v1}",
            $"MOV {src}, {v2}",
            $"ADD {dest}, {src}",
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
        public void AL_BL(string v1, string v2, byte expectedSum, bool expectedCarry, bool expectedZero)
        {
            var agent = MovTestHarness.Run64(Program("AL", "BL", v1, v2));

            Assert.Equal(expectedSum, agent.ReadHalfRegister(Register.AL));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }

        [Theory]
        [InlineData("5", "3", (ushort)8, false, false)]
        [InlineData("0xFFFF", "1", (ushort)0, true, true)]
        public void AX_BX(string v1, string v2, ushort expectedSum, bool expectedCarry, bool expectedZero)
        {
            var agent = MovTestHarness.Run64(Program("AX", "BX", v1, v2));

            Assert.Equal(expectedSum, agent.ReadRegister(Register.AX));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }

        [Theory]
        [InlineData("5", "3", 8u, false, false)]
        [InlineData("0xFFFFFFFF", "1", 0u, true, true)]
        public void EAX_EBX(string v1, string v2, uint expectedSum, bool expectedCarry, bool expectedZero)
        {
            var agent = MovTestHarness.Run64(Program("EAX", "EBX", v1, v2));

            Assert.Equal(expectedSum, agent.ReadExtendedRegister(Register.EAX));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }

        [Theory]
        // Baseline: sum fits, no carry, non-zero.
        [InlineData("5", "3", 8ul, false, false)]
        // Full 64-bit wrap: catches the ulong.MaxValue-vs-uint.MaxValue carry bug and the
        // case-4-vs-case-8 label bug (both operands are 64-bit; a broken label throws).
        [InlineData("0xFFFFFFFFFFFFFFFF", "1", 0ul, true, true)]
        // Sum straddles the 32-bit boundary: catches the (int) truncation in the signed
        // reinterpret. With (int) the low 32 bits are zero, so ZF would incorrectly be set.
        [InlineData("0x100000000", "0x100000000", 0x200000000ul, false, false)]
        public void RAX_RBX(string v1, string v2, ulong expectedSum, bool expectedCarry, bool expectedZero)
        {
            var agent = MovTestHarness.Run64(Program("RAX", "RBX", v1, v2));

            Assert.Equal(expectedSum, agent.ReadR64Register(Register.RAX));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }
    }
}
