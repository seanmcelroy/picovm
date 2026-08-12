using System;
using picovm.Tests.Support;
using picovm.VM;
using Xunit;

namespace picovm.Tests
{
    /// <summary>
    /// Coverage for <c>MOV_DIRECT_IMMEDIATE</c> (<c>MOV DWORD [counter], 0xDEADBEEF</c>) -- a
    /// store of a literal to an absolute address resolved at compile time.
    /// </summary>
    /// <remarks>
    /// This opcode is unique in carrying an explicit operand size: neither operand is a
    /// register that could imply a width, so the assembler bakes the size into the instruction
    /// stream and the VM decodes it.  Sister opcodes <c>MOV_DIRECT_STORE</c> and
    /// <c>MOV_DIRECT_LOAD</c> take their width from the register operand instead.
    /// </remarks>
    public class MovDirectImmediateTests
    {
        /// <summary>Eight zero bytes, so narrow stores have neighbours that can be clobbered.</summary>
        private const string Counter = "counter db 0, 0, 0, 0, 0, 0, 0, 0";

        #region Stores at each width

        [Theory]
        [InlineData("BYTE", "0x41", new byte[] { 0x41, 0, 0, 0, 0, 0, 0, 0 })]
        [InlineData("WORD", "0xBEEF", new byte[] { 0xEF, 0xBE, 0, 0, 0, 0, 0, 0 })]
        [InlineData("DWORD", "0xDEADBEEF", new byte[] { 0xEF, 0xBE, 0xAD, 0xDE, 0, 0, 0, 0 })]
        public void Store32(string hint, string literal, byte[] expected)
        {
            var compilation = MovTestHarness.Compile32(
                Asm.WithData([Counter], $"MOV {hint} [counter], {literal}"));
            var agent = MovTestHarness.Load32(compilation);
            MovTestHarness.RunToEnd(agent);

            var address = MovTestHarness.DataSymbolAddress(compilation, "counter");
            Assert.Equal(expected, agent.PeekMemory(address, 8));
        }

        [Theory]
        [InlineData("BYTE", "0x41", new byte[] { 0x41, 0, 0, 0, 0, 0, 0, 0 })]
        [InlineData("WORD", "0xBEEF", new byte[] { 0xEF, 0xBE, 0, 0, 0, 0, 0, 0 })]
        [InlineData("DWORD", "0xDEADBEEF", new byte[] { 0xEF, 0xBE, 0xAD, 0xDE, 0, 0, 0, 0 })]
        [InlineData("QWORD", "0x1122334455667788", new byte[] { 0x88, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11 })]
        public void Store64(string hint, string literal, byte[] expected)
        {
            var compilation = MovTestHarness.Compile64(
                Asm.WithData([Counter], $"MOV {hint} [counter], {literal}"));
            var agent = MovTestHarness.Load64(compilation);
            MovTestHarness.RunToEnd(agent);

            var address = MovTestHarness.DataSymbolAddress(compilation, "counter");
            Assert.Equal(expected, agent.PeekMemory(address, 8));
        }

        /// <summary>
        /// The neighbour assertions above only mean something if the bytes started non-zero as
        /// well, so this fills the symbol first and then stores narrowly over it.
        /// </summary>
        [Fact]
        public void NarrowStore_LeavesNeighbouringBytesUntouched()
        {
            var compilation = MovTestHarness.Compile64(
                Asm.WithData([Counter],
                    "MOV QWORD [counter], 0xFFFFFFFFFFFFFFFF",
                    "MOV BYTE [counter], 0x41"));
            var agent = MovTestHarness.Load64(compilation);
            MovTestHarness.RunToEnd(agent);

            var address = MovTestHarness.DataSymbolAddress(compilation, "counter");
            Assert.Equal(
                new byte[] { 0x41, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF },
                agent.PeekMemory(address, 8));
        }

        [Fact]
        public void WordStore_LeavesNeighbouringBytesUntouched()
        {
            var compilation = MovTestHarness.Compile64(
                Asm.WithData([Counter],
                    "MOV QWORD [counter], 0xFFFFFFFFFFFFFFFF",
                    "MOV WORD [counter], 0xBEEF"));
            var agent = MovTestHarness.Load64(compilation);
            MovTestHarness.RunToEnd(agent);

            var address = MovTestHarness.DataSymbolAddress(compilation, "counter");
            Assert.Equal(
                new byte[] { 0xEF, 0xBE, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF },
                agent.PeekMemory(address, 8));
        }

        /// <summary>
        /// Two symbols side by side: a store into one must not spill into the other.
        /// </summary>
        [Fact]
        public void Store_DoesNotSpillIntoAdjacentSymbol()
        {
            var compilation = MovTestHarness.Compile32(
                Asm.WithData(
                    ["first db 0, 0, 0, 0", "second db 0x11, 0x22, 0x33, 0x44"],
                    "MOV DWORD [first], 0xDEADBEEF",
                    "MOV ECX, second"));
            var agent = MovTestHarness.Load32(compilation);
            MovTestHarness.RunToEnd(agent);

            Assert.Equal(
                new byte[] { 0xEF, 0xBE, 0xAD, 0xDE },
                agent.PeekMemory(MovTestHarness.DataSymbolAddress(compilation, "first"), 4));
            Assert.Equal(
                new byte[] { 0x11, 0x22, 0x33, 0x44 },
                agent.PeekMemory(MovTestHarness.DataSymbolAddress(compilation, "second"), 4));
        }

        #endregion

        #region Round trip across all three addressing forms

        /// <summary>
        /// Stores a value with <c>MOV_DIRECT</c>, takes the symbol's address with
        /// <c>MOV_IMMEDIATE</c>, and reads it back with <c>MOV_INDIRECT</c>.  If the three forms
        /// disagree about where a symbol lives, this is what catches it.
        /// </summary>
        [Theory]
        [InlineData("BYTE", "0x41", 0x00000041U)]
        [InlineData("WORD", "0xBEEF", 0x0000BEEFU)]
        [InlineData("DWORD", "0xDEADBEEF", 0xDEADBEEFU)]
        public void RoundTrip32(string hint, string literal, uint expected)
        {
            var agent = MovTestHarness.Run32(Asm.WithData([Counter],
                $"MOV {hint} [counter], {literal}",
                "MOV ECX, counter",
                "MOV EAX, [ECX]"));

            Assert.Equal(expected, agent.ReadExtendedRegister(Register.EAX));
        }

        [Theory]
        [InlineData("BYTE", "0x41", 0x0000000000000041UL)]
        [InlineData("WORD", "0xBEEF", 0x000000000000BEEFUL)]
        [InlineData("DWORD", "0xDEADBEEF", 0x00000000DEADBEEFUL)]
        [InlineData("QWORD", "0x1122334455667788", 0x1122334455667788UL)]
        public void RoundTrip64(string hint, string literal, ulong expected)
        {
            var agent = MovTestHarness.Run64(Asm.WithData([Counter],
                $"MOV {hint} [counter], {literal}",
                "MOV RCX, counter",
                "MOV RAX, [RCX]"));

            Assert.Equal(expected, agent.ReadR64Register(Register.RAX));
        }

        #endregion

        #region Rejected forms

        /// <summary>
        /// Without a hint the assembler has no way to size the store; it does not yet infer the
        /// width from the data section.  Pinned so that whoever implements that inference knows
        /// a test is watching.
        /// </summary>
        [Fact]
        public void UnhintedStore_IsRejected()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                MovTestHarness.TryCompile32(Asm.WithData([Counter], "MOV [counter], 5")));

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
                MovTestHarness.TryCompile32(Asm.WithData([Counter], "MOV QWORD [counter], 1")));

            Assert.Contains("exceeds the 4-byte machine width", ex.Message);
        }

        /// <summary>
        /// Storing a register or an indirect value to an absolute address is not implemented.
        /// Characterisation: convert these to behavioural assertions when the feature lands.
        /// </summary>
        [Theory]
        [InlineData("MOV DWORD [counter], [EBX]")]
        public void UnimplementedSourceForms_Throw(string instruction) =>
            Assert.Throws<NotImplementedException>(() =>
                MovTestHarness.TryCompile32(Asm.WithData([Counter], instruction)));

        #endregion

        #region Encoding-level invariants

        /// <summary>
        /// The store writes exactly the immediate's bytes, so the address the compiler baked in
        /// must be the same address the symbol table reports.
        /// </summary>
        [Fact]
        public void StoreLandsAtTheSymbolTableAddress()
        {
            var compilation = MovTestHarness.Compile32(
                Asm.WithData([Counter], "MOV DWORD [counter], 0xDEADBEEF"));
            var agent = MovTestHarness.Load32(compilation);
            MovTestHarness.RunToEnd(agent);

            var address = MovTestHarness.DataSymbolAddress(compilation, "counter");

            // Nothing before the symbol was touched: the text segment is still intact.
            var peek = agent.PeekMemory(0, (int)compilation.TextSegmentSize!.Value).ToArray();
            Assert.Equal(compilation.TextSegment!.Value, peek);
            Assert.Equal(new byte[] { 0xEF, 0xBE, 0xAD, 0xDE }, agent.PeekMemory(address, 4));
        }

        #endregion
    }
}
