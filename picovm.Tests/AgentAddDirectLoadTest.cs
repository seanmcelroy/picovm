using System;
using picovm.Tests.Support;
using picovm.VM;
using Xunit;

namespace picovm.Tests
{
    /// <summary>
    /// Coverage for <c>ADD_DIRECT_LOAD</c> (<c>ADD reg, [symbol]</c>): load from an absolute
    /// address resolved at compile time, add into the destination register, set arithmetic
    /// flags.  Mirrors <see cref="AgentAddIndirectRegisterTest"/>'s sentinel pattern: each
    /// case runs a <c>JC</c> and a <c>JZ</c> after the <c>ADD</c>, each writing a different
    /// value into a spare register, so a row only passes if the sum, both flags, and post-ADD
    /// decoding are all correct.  The JC/JZ witness also indirectly guards the IP advance
    /// past the baked-in address: a wrong advance corrupts every following instruction, and
    /// the witness branches decode as garbage.
    /// </summary>
    public class AgentAddDirectLoadTest
    {
        // The counter is seeded at runtime via MOV_DIRECT_IMMEDIATE so the same 8-byte
        // scratch works for byte/word/dword tests without a per-width declaration.  ADD
        // reads only as many bytes as the destination register's width; extra bytes are
        // ignored by the load.
        private static string[] Program(string destReg, string hint, string v1, string v2) => Asm.WithData(
            ["counter db 0, 0, 0, 0, 0, 0, 0, 0"],
            $"MOV {hint} [counter], {v2}",
            $"MOV {destReg}, {v1}",
            $"ADD {destReg}, [counter]",
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

        // ---- Baselines at each width ------------------------------------------------------

        [Theory]
        [InlineData("5", "3", (byte)8, false, false)]
        [InlineData("0xFF", "1", (byte)0, true, true)]
        public void Byte(string v1, string v2, byte expectedSum, bool expectedCarry, bool expectedZero)
        {
            var agent = MovTestHarness.Run32(Program("AL", "BYTE", v1, v2));

            Assert.Equal(expectedSum, agent.ReadHalfRegister(Register.AL));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }

        [Theory]
        [InlineData("5", "3", (ushort)8, false, false)]
        [InlineData("0xFFFF", "1", (ushort)0, true, true)]
        public void Word(string v1, string v2, ushort expectedSum, bool expectedCarry, bool expectedZero)
        {
            var agent = MovTestHarness.Run32(Program("AX", "WORD", v1, v2));

            Assert.Equal(expectedSum, agent.ReadRegister(Register.AX));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }

        [Theory]
        [InlineData("5", "3", 8u, false, false)]
        [InlineData("0xFFFFFFFF", "1", 0u, true, true)]
        public void DWord(string v1, string v2, uint expectedSum, bool expectedCarry, bool expectedZero)
        {
            var agent = MovTestHarness.Run32(Program("EAX", "DWORD", v1, v2));

            Assert.Equal(expectedSum, agent.ReadExtendedRegister(Register.EAX));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }

        // ---- Instruction contract ---------------------------------------------------------

        /// <summary>
        /// ADD_DIRECT_LOAD is a load, not a read-modify-write: the memory at the symbol must
        /// be unchanged after the instruction.  If it were misimplemented as ADD_DIRECT_STORE,
        /// this test catches it -- the sum would land at [counter] instead of (or as well as)
        /// in the register.
        /// </summary>
        [Fact]
        public void SourceSymbol_IsNotClobbered()
        {
            var compilation = MovTestHarness.Compile32(Asm.WithData(
                ["counter db 0xEF, 0xBE, 0xAD, 0xDE"],
                "MOV EAX, 0x11111111",
                "ADD EAX, [counter]"));
            var agent = MovTestHarness.Load32(compilation);
            MovTestHarness.RunToEnd(agent);

            var addr = MovTestHarness.DataSymbolAddress(compilation, "counter");
            Assert.Equal(new byte[] { 0xEF, 0xBE, 0xAD, 0xDE }, agent.PeekMemory(addr, 4));
            Assert.Equal(0x11111111U + 0xDEADBEEFU, agent.ReadExtendedRegister(Register.EAX));
        }

        /// <summary>
        /// Two adjacent symbols: a load from one must not pick up bytes from the other.  If
        /// the compiler baked in the wrong address or the VM read the wrong width, at least
        /// one of these two adds would land in the neighbour's bytes.
        /// </summary>
        [Fact]
        public void LoadsFromSymbolAddress_NotNeighbour()
        {
            var agent = MovTestHarness.Run32(Asm.WithData(
                ["first  db 0x01, 0x00, 0x00, 0x00",
                 "second db 0x02, 0x00, 0x00, 0x00"],
                "MOV EAX, 10",
                "ADD EAX, [first]",
                "MOV EBX, 20",
                "ADD EBX, [second]"));

            Assert.Equal(11U, agent.ReadExtendedRegister(Register.EAX));
            Assert.Equal(22U, agent.ReadExtendedRegister(Register.EBX));
        }

        // ---- Size hints -------------------------------------------------------------------

        /// <summary>
        /// Contrast with the direct-immediate form: LOAD infers its width from the
        /// destination register, so no hint is required.
        /// </summary>
        [Fact]
        public void UnhintedLoad_IsAccepted()
        {
            var agent = MovTestHarness.Run32(Asm.WithData(
                ["counter db 0x03, 0x00, 0x00, 0x00"],
                "MOV EAX, 5",
                "ADD EAX, [counter]"));

            Assert.Equal(8U, agent.ReadExtendedRegister(Register.EAX));
        }

        /// <summary>
        /// A hint disagreeing with the destination register is rejected at compile time -- the
        /// VM would silently use the register's width, so the hint would be a lie.
        /// </summary>
        [Theory]
        [InlineData("ADD BYTE EAX, [counter]")]
        [InlineData("ADD WORD EAX, [counter]")]
        [InlineData("ADD DWORD AL, [counter]")]
        public void DisagreeingSizeHint_Throws(string instruction)
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                MovTestHarness.TryCompile32(Asm.WithData(
                    ["counter db 0, 0, 0, 0, 0, 0, 0, 0"], instruction)));

            Assert.Contains("disagrees with", ex.Message);
        }
    }

    /// <summary>64-bit mirror of <see cref="AgentAddDirectLoadTest"/>, plus a QWord row.</summary>
    public class Agent64AddDirectLoadTest
    {
        private static string[] Program(string destReg, string hint, string v1, string v2) => Asm.WithData(
            ["counter db 0, 0, 0, 0, 0, 0, 0, 0"],
            $"MOV {hint} [counter], {v2}",
            $"MOV {destReg}, {v1}",
            $"ADD {destReg}, [counter]",
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
            var agent = MovTestHarness.Run64(Program("AL", "BYTE", v1, v2));

            Assert.Equal(expectedSum, agent.ReadHalfRegister(Register.AL));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }

        [Theory]
        [InlineData("5", "3", (ushort)8, false, false)]
        [InlineData("0xFFFF", "1", (ushort)0, true, true)]
        public void Word(string v1, string v2, ushort expectedSum, bool expectedCarry, bool expectedZero)
        {
            var agent = MovTestHarness.Run64(Program("AX", "WORD", v1, v2));

            Assert.Equal(expectedSum, agent.ReadRegister(Register.AX));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }

        [Theory]
        [InlineData("5", "3", 8u, false, false)]
        [InlineData("0xFFFFFFFF", "1", 0u, true, true)]
        public void DWord(string v1, string v2, uint expectedSum, bool expectedCarry, bool expectedZero)
        {
            var agent = MovTestHarness.Run64(Program("EAX", "DWORD", v1, v2));

            Assert.Equal(expectedSum, agent.ReadExtendedRegister(Register.EAX));
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
            var agent = MovTestHarness.Run64(Program("RAX", "QWORD", v1, v2));

            Assert.Equal(expectedSum, agent.ReadR64Register(Register.RAX));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }

        [Fact]
        public void SourceSymbol_IsNotClobbered()
        {
            var compilation = MovTestHarness.Compile64(Asm.WithData(
                ["counter db 0x0B, 0xB0, 0xFE, 0xCA, 0xEF, 0xBE, 0xAD, 0xDE"],
                "MOV RAX, 0x1111111111111111",
                "ADD RAX, [counter]"));
            var agent = MovTestHarness.Load64(compilation);
            MovTestHarness.RunToEnd(agent);

            var addr = MovTestHarness.DataSymbolAddress(compilation, "counter");
            Assert.Equal(
                new byte[] { 0x0B, 0xB0, 0xFE, 0xCA, 0xEF, 0xBE, 0xAD, 0xDE },
                agent.PeekMemory(addr, 8));
            Assert.Equal(0x1111111111111111UL + 0xDEADBEEFCAFEB00BUL, agent.ReadR64Register(Register.RAX));
        }

        [Fact]
        public void UnhintedLoad_IsAccepted()
        {
            var agent = MovTestHarness.Run64(Asm.WithData(
                ["counter db 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00"],
                "MOV RAX, 5",
                "ADD RAX, [counter]"));

            Assert.Equal(8UL, agent.ReadR64Register(Register.RAX));
        }

        [Theory]
        [InlineData("ADD DWORD RAX, [counter]")]
        [InlineData("ADD BYTE RAX, [counter]")]
        [InlineData("ADD QWORD EAX, [counter]")]
        public void DisagreeingSizeHint_Throws(string instruction)
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                MovTestHarness.TryCompile64(Asm.WithData(
                    ["counter db 0, 0, 0, 0, 0, 0, 0, 0"], instruction)));

            Assert.Contains("disagrees with", ex.Message);
        }
    }
}
