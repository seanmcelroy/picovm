using System;
using picovm.Tests.Support;
using picovm.VM;
using Xunit;

namespace picovm.Tests
{
    /// <summary>
    /// Coverage for <c>ADD_DIRECT_IMMEDIATE</c> (<c>ADD DWORD [symbol], const</c>):
    /// read-modify-write of memory at an absolute address resolved at compile time, using a
    /// literal as the addend.  Like <c>MOV_DIRECT_IMMEDIATE</c>, this opcode carries an
    /// explicit size byte in the instruction stream because neither operand is a register
    /// that could imply a width.
    /// </summary>
    public class AgentAddDirectImmediateTest
    {
        // Memory seeded with v1 via MOV_DIRECT_IMMEDIATE, then ADD_DIRECT_IMMEDIATE adds v2.
        // Both instructions use the same width hint so v1 and v2 exercise the same case arm.
        private static string[] Program(string hint, string v1, string v2) => Asm.WithData(
            ["counter db 0, 0, 0, 0, 0, 0, 0, 0"],
            $"MOV {hint} [counter], {v1}",
            $"ADD {hint} [counter], {v2}",
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
            var compilation = MovTestHarness.Compile32(Program("BYTE", v1, v2));
            var agent = MovTestHarness.Load32(compilation);
            MovTestHarness.RunToEnd(agent);

            var addr = MovTestHarness.DataSymbolAddress(compilation, "counter");
            Assert.Equal(expectedSum, agent.PeekMemory(addr, 1)[0]);
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }

        [Theory]
        [InlineData("5", "3", (ushort)8, false, false)]
        [InlineData("0xFFFF", "1", (ushort)0, true, true)]
        public void Word(string v1, string v2, ushort expectedSum, bool expectedCarry, bool expectedZero)
        {
            var compilation = MovTestHarness.Compile32(Program("WORD", v1, v2));
            var agent = MovTestHarness.Load32(compilation);
            MovTestHarness.RunToEnd(agent);

            var addr = MovTestHarness.DataSymbolAddress(compilation, "counter");
            var mem = agent.PeekMemory(addr, 2);
            Assert.Equal(expectedSum, (ushort)(mem[0] | (mem[1] << 8)));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }

        [Theory]
        [InlineData("5", "3", 8u, false, false)]
        [InlineData("0xFFFFFFFF", "1", 0u, true, true)]
        public void DWord(string v1, string v2, uint expectedSum, bool expectedCarry, bool expectedZero)
        {
            var compilation = MovTestHarness.Compile32(Program("DWORD", v1, v2));
            var agent = MovTestHarness.Load32(compilation);
            MovTestHarness.RunToEnd(agent);

            var addr = MovTestHarness.DataSymbolAddress(compilation, "counter");
            var mem = agent.PeekMemory(addr, 4);
            Assert.Equal(expectedSum, (uint)(mem[0] | (mem[1] << 8) | (mem[2] << 16) | (mem[3] << 24)));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }

        // ---- IP advance past the immediate ------------------------------------------------

        /// <summary>
        /// Specifically pins Bug 1 from the review: without <c>InstructionPointer += size</c>
        /// after reading the immediate, the following instruction decodes garbage.  A
        /// distinctive literal in a dedicated register makes the failure mode obvious --
        /// "EDX is not 0x12345678" is unambiguously an IP misalignment rather than a flag
        /// or arithmetic error, which is what the baseline JC/JZ witnesses would surface as.
        /// </summary>
        [Theory]
        [InlineData("BYTE", "5")]
        [InlineData("WORD", "0xBEEF")]
        [InlineData("DWORD", "0xDEADBEEF")]
        public void FollowingInstruction_DecodesCorrectly(string hint, string literal)
        {
            var agent = MovTestHarness.Run32(Asm.WithData(
                ["counter db 0, 0, 0, 0"],
                $"ADD {hint} [counter], {literal}",
                "MOV EDX, 0x12345678"));

            Assert.Equal(0x12345678U, agent.ReadExtendedRegister(Register.EDX));
        }

        // ---- Store-width contract ---------------------------------------------------------

        /// <summary>
        /// A byte-width store must write exactly one byte.  The neighbour symbol sits at
        /// <c>first + 1</c>, so any wider write clobbers <c>second[0]</c>.  Same shape as
        /// the ADD_DIRECT_STORE byte-spill test.
        /// </summary>
        [Fact]
        public void ByteStore_DoesNotSpillIntoNeighbour()
        {
            var compilation = MovTestHarness.Compile32(Asm.WithData(
                ["first  db 0x00",
                 "second db 0x11, 0x22, 0x33, 0x44"],
                "ADD BYTE [first], 5",
                "MOV EDX, second")); // satisfy "symbol must be referenced" without touching its bytes
            var agent = MovTestHarness.Load32(compilation);
            MovTestHarness.RunToEnd(agent);

            Assert.Equal((byte)0x05, agent.PeekMemory(MovTestHarness.DataSymbolAddress(compilation, "first"), 1)[0]);
            Assert.Equal(
                new byte[] { 0x11, 0x22, 0x33, 0x44 },
                agent.PeekMemory(MovTestHarness.DataSymbolAddress(compilation, "second"), 4));
        }

        /// <summary>
        /// Two adjacent multi-byte symbols: wide stores must land at the right address and
        /// not spill.  Also verifies the atomic-w.r.t.-IP ordering: a wrong IP advance
        /// after the first ADD would corrupt the decoding of the second.
        /// </summary>
        [Fact]
        public void StoresToSymbolAddress_NotNeighbour()
        {
            var compilation = MovTestHarness.Compile32(Asm.WithData(
                ["first  db 0x01, 0x00, 0x00, 0x00",
                 "second db 0x02, 0x00, 0x00, 0x00"],
                "ADD DWORD [first], 10",
                "ADD DWORD [second], 20"));
            var agent = MovTestHarness.Load32(compilation);
            MovTestHarness.RunToEnd(agent);

            var firstMem = agent.PeekMemory(MovTestHarness.DataSymbolAddress(compilation, "first"), 4);
            var secondMem = agent.PeekMemory(MovTestHarness.DataSymbolAddress(compilation, "second"), 4);
            Assert.Equal(11u, (uint)(firstMem[0] | (firstMem[1] << 8) | (firstMem[2] << 16) | (firstMem[3] << 24)));
            Assert.Equal(22u, (uint)(secondMem[0] | (secondMem[1] << 8) | (secondMem[2] << 16) | (secondMem[3] << 24)));
        }

        // ---- Size hints -------------------------------------------------------------------

        /// <summary>
        /// Unlike LOAD/STORE, IMMEDIATE has no register to infer width from, so the hint is
        /// required.  Rejected at compile time.
        /// </summary>
        [Fact]
        public void UnhintedStore_IsRejected()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                MovTestHarness.TryCompile32(Asm.WithData(
                    ["counter db 0, 0, 0, 0"], "ADD [counter], 5")));

            Assert.Contains("unhinted symbol immediates", ex.Message);
        }

        /// <summary>
        /// A 32-bit agent's size-byte validator accepts only 1, 2 or 4, so a QWORD store is
        /// rejected at compile time rather than crashing mid-execution.
        /// </summary>
        [Fact]
        public void QwordStoreIn32Bit_IsRejected()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                MovTestHarness.TryCompile32(Asm.WithData(
                    ["counter db 0, 0, 0, 0, 0, 0, 0, 0"], "ADD QWORD [counter], 1")));

            Assert.Contains("exceeds the 4-byte machine width", ex.Message);
        }
    }

    /// <summary>64-bit mirror of <see cref="AgentAddDirectImmediateTest"/>, plus a QWord row.</summary>
    public class Agent64AddDirectImmediateTest
    {
        private static string[] Program(string hint, string v1, string v2) => Asm.WithData(
            ["counter db 0, 0, 0, 0, 0, 0, 0, 0"],
            $"MOV {hint} [counter], {v1}",
            $"ADD {hint} [counter], {v2}",
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
            var compilation = MovTestHarness.Compile64(Program("BYTE", v1, v2));
            var agent = MovTestHarness.Load64(compilation);
            MovTestHarness.RunToEnd(agent);

            var addr = MovTestHarness.DataSymbolAddress(compilation, "counter");
            Assert.Equal(expectedSum, agent.PeekMemory(addr, 1)[0]);
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }

        [Theory]
        [InlineData("5", "3", (ushort)8, false, false)]
        [InlineData("0xFFFF", "1", (ushort)0, true, true)]
        public void Word(string v1, string v2, ushort expectedSum, bool expectedCarry, bool expectedZero)
        {
            var compilation = MovTestHarness.Compile64(Program("WORD", v1, v2));
            var agent = MovTestHarness.Load64(compilation);
            MovTestHarness.RunToEnd(agent);

            var addr = MovTestHarness.DataSymbolAddress(compilation, "counter");
            var mem = agent.PeekMemory(addr, 2);
            Assert.Equal(expectedSum, (ushort)(mem[0] | (mem[1] << 8)));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }

        [Theory]
        [InlineData("5", "3", 8u, false, false)]
        [InlineData("0xFFFFFFFF", "1", 0u, true, true)]
        public void DWord(string v1, string v2, uint expectedSum, bool expectedCarry, bool expectedZero)
        {
            var compilation = MovTestHarness.Compile64(Program("DWORD", v1, v2));
            var agent = MovTestHarness.Load64(compilation);
            MovTestHarness.RunToEnd(agent);

            var addr = MovTestHarness.DataSymbolAddress(compilation, "counter");
            var mem = agent.PeekMemory(addr, 4);
            Assert.Equal(expectedSum, (uint)(mem[0] | (mem[1] << 8) | (mem[2] << 16) | (mem[3] << 24)));
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
            var compilation = MovTestHarness.Compile64(Program("QWORD", v1, v2));
            var agent = MovTestHarness.Load64(compilation);
            MovTestHarness.RunToEnd(agent);

            var addr = MovTestHarness.DataSymbolAddress(compilation, "counter");
            var mem = agent.PeekMemory(addr, 8);
            var sum = 0UL;
            for (var i = 0; i < 8; i++)
                sum |= (ulong)mem[i] << (i * 8);
            Assert.Equal(expectedSum, sum);
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }

        [Theory]
        [InlineData("BYTE", "5")]
        [InlineData("WORD", "0xBEEF")]
        [InlineData("DWORD", "0xDEADBEEF")]
        [InlineData("QWORD", "0xDEADBEEFCAFEB00B")]
        public void FollowingInstruction_DecodesCorrectly(string hint, string literal)
        {
            var agent = MovTestHarness.Run64(Asm.WithData(
                ["counter db 0, 0, 0, 0, 0, 0, 0, 0"],
                $"ADD {hint} [counter], {literal}",
                "MOV EDX, 0x12345678"));

            Assert.Equal(0x12345678U, agent.ReadExtendedRegister(Register.EDX));
        }

        [Fact]
        public void ByteStore_DoesNotSpillIntoNeighbour()
        {
            var compilation = MovTestHarness.Compile64(Asm.WithData(
                ["first  db 0x00",
                 "second db 0x11, 0x22, 0x33, 0x44"],
                "ADD BYTE [first], 5",
                "MOV RDX, second")); // satisfy "symbol must be referenced" without touching its bytes
            var agent = MovTestHarness.Load64(compilation);
            MovTestHarness.RunToEnd(agent);

            Assert.Equal((byte)0x05, agent.PeekMemory(MovTestHarness.DataSymbolAddress(compilation, "first"), 1)[0]);
            Assert.Equal(
                new byte[] { 0x11, 0x22, 0x33, 0x44 },
                agent.PeekMemory(MovTestHarness.DataSymbolAddress(compilation, "second"), 4));
        }

        [Fact]
        public void StoresToSymbolAddress_NotNeighbour()
        {
            var compilation = MovTestHarness.Compile64(Asm.WithData(
                ["first  db 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00",
                 "second db 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00"],
                "ADD QWORD [first], 10",
                "ADD QWORD [second], 20"));
            var agent = MovTestHarness.Load64(compilation);
            MovTestHarness.RunToEnd(agent);

            var firstMem = agent.PeekMemory(MovTestHarness.DataSymbolAddress(compilation, "first"), 8);
            var secondMem = agent.PeekMemory(MovTestHarness.DataSymbolAddress(compilation, "second"), 8);
            var firstSum = 0UL;
            var secondSum = 0UL;
            for (var i = 0; i < 8; i++)
            {
                firstSum |= (ulong)firstMem[i] << (i * 8);
                secondSum |= (ulong)secondMem[i] << (i * 8);
            }
            Assert.Equal(11UL, firstSum);
            Assert.Equal(22UL, secondSum);
        }

        [Fact]
        public void UnhintedStore_IsRejected()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                MovTestHarness.TryCompile64(Asm.WithData(
                    ["counter db 0, 0, 0, 0, 0, 0, 0, 0"], "ADD [counter], 5")));

            Assert.Contains("unhinted symbol immediates", ex.Message);
        }
    }
}
