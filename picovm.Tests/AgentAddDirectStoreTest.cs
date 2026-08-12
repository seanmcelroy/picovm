using System;
using picovm.Tests.Support;
using picovm.VM;
using Xunit;

namespace picovm.Tests
{
    /// <summary>
    /// Coverage for <c>ADD_DIRECT_STORE</c> (<c>ADD [symbol], reg</c>): read-modify-write of
    /// memory at an absolute address resolved at compile time, using a register as the
    /// addend.  Mirrors <see cref="AgentAddDirectLoadTest"/>'s sentinel pattern: each case
    /// runs a <c>JC</c> and a <c>JZ</c> after the <c>ADD</c>, so a row only passes if the
    /// sum landed at the symbol, both flags were set correctly, and the decoder advanced IP
    /// past exactly the instruction's bytes -- not one extra width worth, which was the
    /// original bug.
    /// </summary>
    public class AgentAddDirectStoreTest
    {
        // Memory seeded with v1 via MOV_DIRECT_IMMEDIATE, register loaded with v2, then
        // ADD [counter], reg.  Expected sum lands in memory at [counter]; register is
        // unchanged (this is a memory RMW, not a register RMW).
        private static string[] Program(string reg, string hint, string v1, string v2) => Asm.WithData(
            ["counter db 0, 0, 0, 0, 0, 0, 0, 0"],
            $"MOV {hint} [counter], {v1}",
            $"MOV {reg}, {v2}",
            $"ADD [counter], {reg}",
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
            var compilation = MovTestHarness.Compile32(Program("AL", "BYTE", v1, v2));
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
            var compilation = MovTestHarness.Compile32(Program("AX", "WORD", v1, v2));
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
            var compilation = MovTestHarness.Compile32(Program("EAX", "DWORD", v1, v2));
            var agent = MovTestHarness.Load32(compilation);
            MovTestHarness.RunToEnd(agent);

            var addr = MovTestHarness.DataSymbolAddress(compilation, "counter");
            var mem = agent.PeekMemory(addr, 4);
            Assert.Equal(expectedSum, (uint)(mem[0] | (mem[1] << 8) | (mem[2] << 16) | (mem[3] << 24)));
            Assert.Equal(expectedCarry ? 200u : 100u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expectedZero ? 200u : 100u, agent.ReadExtendedRegister(Register.ECX));
        }

        // ---- Instruction contract ---------------------------------------------------------

        /// <summary>
        /// ADD_DIRECT_STORE is a memory RMW: the source register must be unchanged.  If the
        /// implementation accidentally wrote the sum back into the register (misidentified as
        /// ADD_DIRECT_LOAD), this catches it.
        /// </summary>
        [Fact]
        public void SourceRegister_IsNotClobbered()
        {
            var agent = MovTestHarness.Run32(Asm.WithData(
                ["counter db 0, 0, 0, 0"],
                "MOV EAX, 42",
                "ADD [counter], EAX"));

            Assert.Equal(42U, agent.ReadExtendedRegister(Register.EAX));
        }

        /// <summary>
        /// A byte-width store must write exactly one byte.  The neighbour symbol sits at
        /// <c>first + 1</c>, so if the byte arm accidentally wrote two bytes (the earlier
        /// <c>WriteMemoryUInt16</c> bug), <c>second[0]</c> would be zeroed.  Pinning both
        /// the value that landed and the neighbour that didn't.
        /// </summary>
        [Fact]
        public void ByteStore_DoesNotSpillIntoNeighbour()
        {
            var compilation = MovTestHarness.Compile32(Asm.WithData(
                ["first  db 0x00",
                 "second db 0x11, 0x22, 0x33, 0x44"],
                "MOV AL, 5",
                "ADD [first], AL",
                "MOV EDX, second")); // satisfy "symbol must be referenced" without touching its bytes
            var agent = MovTestHarness.Load32(compilation);
            MovTestHarness.RunToEnd(agent);

            Assert.Equal((byte)0x05, agent.PeekMemory(MovTestHarness.DataSymbolAddress(compilation, "first"), 1)[0]);
            Assert.Equal(
                new byte[] { 0x11, 0x22, 0x33, 0x44 },
                agent.PeekMemory(MovTestHarness.DataSymbolAddress(compilation, "second"), 4));
        }

        /// <summary>
        /// Two adjacent multi-byte symbols: a wide store into one must not spill into the
        /// other and must not read from the wrong address.
        /// </summary>
        [Fact]
        public void StoresToSymbolAddress_NotNeighbour()
        {
            var compilation = MovTestHarness.Compile32(Asm.WithData(
                ["first  db 0x01, 0x00, 0x00, 0x00",
                 "second db 0x02, 0x00, 0x00, 0x00"],
                "MOV EAX, 10",
                "ADD [first], EAX",
                "MOV EBX, 20",
                "ADD [second], EBX"));
            var agent = MovTestHarness.Load32(compilation);
            MovTestHarness.RunToEnd(agent);

            var firstAddr = MovTestHarness.DataSymbolAddress(compilation, "first");
            var secondAddr = MovTestHarness.DataSymbolAddress(compilation, "second");
            var firstMem = agent.PeekMemory(firstAddr, 4);
            var secondMem = agent.PeekMemory(secondAddr, 4);
            Assert.Equal(11u, (uint)(firstMem[0] | (firstMem[1] << 8) | (firstMem[2] << 16) | (firstMem[3] << 24)));
            Assert.Equal(22u, (uint)(secondMem[0] | (secondMem[1] << 8) | (secondMem[2] << 16) | (secondMem[3] << 24)));
        }

        // ---- Size hints -------------------------------------------------------------------

        /// <summary>
        /// Width is inferred from the source register, so no hint is required.
        /// </summary>
        [Fact]
        public void UnhintedStore_IsAccepted()
        {
            var compilation = MovTestHarness.Compile32(Asm.WithData(
                ["counter db 3, 0, 0, 0"],
                "MOV EAX, 5",
                "ADD [counter], EAX"));
            var agent = MovTestHarness.Load32(compilation);
            MovTestHarness.RunToEnd(agent);

            var mem = agent.PeekMemory(MovTestHarness.DataSymbolAddress(compilation, "counter"), 4);
            Assert.Equal(8u, (uint)(mem[0] | (mem[1] << 8) | (mem[2] << 16) | (mem[3] << 24)));
        }

        /// <summary>
        /// A hint that disagrees with the source register would silently be ignored -- the VM
        /// uses the register's width -- so it is rejected at compile time.
        /// </summary>
        [Theory]
        [InlineData("ADD BYTE [counter], EAX")]
        [InlineData("ADD DWORD [counter], AL")]
        [InlineData("ADD WORD [counter], AL")]
        public void DisagreeingSizeHint_Throws(string instruction)
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                MovTestHarness.TryCompile32(Asm.WithData(
                    ["counter db 0, 0, 0, 0"], instruction)));

            Assert.Contains("disagrees with", ex.Message);
        }
    }

    /// <summary>64-bit mirror of <see cref="AgentAddDirectStoreTest"/>, plus a QWord row.</summary>
    public class Agent64AddDirectStoreTest
    {
        private static string[] Program(string reg, string hint, string v1, string v2) => Asm.WithData(
            ["counter db 0, 0, 0, 0, 0, 0, 0, 0"],
            $"MOV {hint} [counter], {v1}",
            $"MOV {reg}, {v2}",
            $"ADD [counter], {reg}",
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
            var compilation = MovTestHarness.Compile64(Program("AL", "BYTE", v1, v2));
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
            var compilation = MovTestHarness.Compile64(Program("AX", "WORD", v1, v2));
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
            var compilation = MovTestHarness.Compile64(Program("EAX", "DWORD", v1, v2));
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
            var compilation = MovTestHarness.Compile64(Program("RAX", "QWORD", v1, v2));
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

        [Fact]
        public void SourceRegister_IsNotClobbered()
        {
            var agent = MovTestHarness.Run64(Asm.WithData(
                ["counter db 0, 0, 0, 0, 0, 0, 0, 0"],
                "MOV RAX, 42",
                "ADD [counter], RAX"));

            Assert.Equal(42UL, agent.ReadR64Register(Register.RAX));
        }

        [Fact]
        public void ByteStore_DoesNotSpillIntoNeighbour()
        {
            var compilation = MovTestHarness.Compile64(Asm.WithData(
                ["first  db 0x00",
                 "second db 0x11, 0x22, 0x33, 0x44"],
                "MOV AL, 5",
                "ADD [first], AL",
                "MOV RDX, second")); // satisfy "symbol must be referenced" without touching its bytes
            var agent = MovTestHarness.Load64(compilation);
            MovTestHarness.RunToEnd(agent);

            Assert.Equal((byte)0x05, agent.PeekMemory(MovTestHarness.DataSymbolAddress(compilation, "first"), 1)[0]);
            Assert.Equal(
                new byte[] { 0x11, 0x22, 0x33, 0x44 },
                agent.PeekMemory(MovTestHarness.DataSymbolAddress(compilation, "second"), 4));
        }

        [Fact]
        public void UnhintedStore_IsAccepted()
        {
            var compilation = MovTestHarness.Compile64(Asm.WithData(
                ["counter db 3, 0, 0, 0, 0, 0, 0, 0"],
                "MOV RAX, 5",
                "ADD [counter], RAX"));
            var agent = MovTestHarness.Load64(compilation);
            MovTestHarness.RunToEnd(agent);

            var mem = agent.PeekMemory(MovTestHarness.DataSymbolAddress(compilation, "counter"), 8);
            var sum = 0UL;
            for (var i = 0; i < 8; i++)
                sum |= (ulong)mem[i] << (i * 8);
            Assert.Equal(8UL, sum);
        }

        [Theory]
        [InlineData("ADD DWORD [counter], RAX")]
        [InlineData("ADD BYTE [counter], RAX")]
        [InlineData("ADD QWORD [counter], EAX")]
        public void DisagreeingSizeHint_Throws(string instruction)
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                MovTestHarness.TryCompile64(Asm.WithData(
                    ["counter db 0, 0, 0, 0, 0, 0, 0, 0"], instruction)));

            Assert.Contains("disagrees with", ex.Message);
        }
    }
}
