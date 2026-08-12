using System;
using picovm.Tests.Support;
using picovm.VM;
using Xunit;

namespace picovm.Tests
{
    /// <summary>
    /// Coverage for <c>MOV_DIRECT_STORE</c> (<c>MOV [counter], EAX</c>) -- a store of a
    /// register value to an absolute address resolved at compile time.
    /// </summary>
    /// <remarks>
    /// The source register's width tells the VM how many bytes to write.  Sister opcodes:
    /// <c>MOV_DIRECT_LOAD</c> mirrors this direction (address &#8594; register);
    /// <c>MOV_DIRECT_IMMEDIATE</c> carries an explicit size byte because neither of its
    /// operands is a register that could imply one.
    /// </remarks>
    public class MovDirectStoreTests
    {
        /// <summary>Eight zero bytes, so narrow stores have neighbours that can be clobbered.</summary>
        private const string Counter = "counter db 0, 0, 0, 0, 0, 0, 0, 0";

        #region Stores at each width

        [Theory]
        [InlineData("AL",  "0x41",       new byte[] { 0x41, 0, 0, 0, 0, 0, 0, 0 })]
        [InlineData("AX",  "0xBEEF",     new byte[] { 0xEF, 0xBE, 0, 0, 0, 0, 0, 0 })]
        [InlineData("EAX", "0xDEADBEEF", new byte[] { 0xEF, 0xBE, 0xAD, 0xDE, 0, 0, 0, 0 })]
        public void Store32(string reg, string literal, byte[] expected)
        {
            var compilation = MovTestHarness.Compile32(
                Asm.WithData([Counter],
                    $"MOV {reg}, {literal}",
                    $"MOV [counter], {reg}"));
            var agent = MovTestHarness.Load32(compilation);
            MovTestHarness.RunToEnd(agent);

            var address = MovTestHarness.DataSymbolAddress(compilation, "counter");
            Assert.Equal(expected, agent.PeekMemory(address, 8));
        }

        [Theory]
        [InlineData("AL",  "0x41",               new byte[] { 0x41, 0, 0, 0, 0, 0, 0, 0 })]
        [InlineData("AX",  "0xBEEF",             new byte[] { 0xEF, 0xBE, 0, 0, 0, 0, 0, 0 })]
        [InlineData("EAX", "0xDEADBEEF",         new byte[] { 0xEF, 0xBE, 0xAD, 0xDE, 0, 0, 0, 0 })]
        [InlineData("RAX", "0x1122334455667788", new byte[] { 0x88, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11 })]
        public void Store64(string reg, string literal, byte[] expected)
        {
            var compilation = MovTestHarness.Compile64(
                Asm.WithData([Counter],
                    $"MOV {reg}, {literal}",
                    $"MOV [counter], {reg}"));
            var agent = MovTestHarness.Load64(compilation);
            MovTestHarness.RunToEnd(agent);

            var address = MovTestHarness.DataSymbolAddress(compilation, "counter");
            Assert.Equal(expected, agent.PeekMemory(address, 8));
        }

        /// <summary>
        /// A narrow store must touch only its own width.  Pre-fill the symbol with 0xFF so the
        /// assertion is meaningful: if the store overshoots, the neighbours become non-0xFF.
        /// </summary>
        [Fact]
        public void NarrowStore_LeavesNeighbouringBytesUntouched()
        {
            var compilation = MovTestHarness.Compile64(
                Asm.WithData([Counter],
                    "MOV RAX, 0xFFFFFFFFFFFFFFFF",
                    "MOV [counter], RAX",
                    "MOV AL, 0x41",
                    "MOV [counter], AL"));
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
                    "MOV RAX, 0xFFFFFFFFFFFFFFFF",
                    "MOV [counter], RAX",
                    "MOV AX, 0xBEEF",
                    "MOV [counter], AX"));
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
            // MOV ECX, second only takes the address, so second's bytes are inspected but
            // never touched -- proving the store into first cannot reach them.
            var compilation = MovTestHarness.Compile32(
                Asm.WithData(
                    ["first  db 0, 0, 0, 0",
                     "second db 0x11, 0x22, 0x33, 0x44"],
                    "MOV EAX, 0xDEADBEEF",
                    "MOV [first], EAX",
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

        #region Size hints

        /// <summary>
        /// A hint that agrees with the source register is redundant but harmless.
        /// </summary>
        [Theory]
        [InlineData("BYTE",  "AL",  "0x41",       new byte[] { 0x41, 0, 0, 0, 0, 0, 0, 0 })]
        [InlineData("WORD",  "AX",  "0xBEEF",     new byte[] { 0xEF, 0xBE, 0, 0, 0, 0, 0, 0 })]
        [InlineData("DWORD", "EAX", "0xDEADBEEF", new byte[] { 0xEF, 0xBE, 0xAD, 0xDE, 0, 0, 0, 0 })]
        public void AgreeingSizeHint_IsAccepted(string hint, string reg, string literal, byte[] expected)
        {
            var compilation = MovTestHarness.Compile32(
                Asm.WithData([Counter],
                    $"MOV {reg}, {literal}",
                    $"MOV {hint} [counter], {reg}"));
            var agent = MovTestHarness.Load32(compilation);
            MovTestHarness.RunToEnd(agent);

            var address = MovTestHarness.DataSymbolAddress(compilation, "counter");
            Assert.Equal(expected, agent.PeekMemory(address, 8));
        }

        /// <summary>
        /// STORE takes its width from the source register, so a disagreeing hint would be
        /// silently ignored -- the VM would still write the register's width.  Reject at
        /// compile time so callers cannot lie about what the instruction does.
        /// </summary>
        [Theory]
        [InlineData("MOV BYTE [counter], EAX")]
        [InlineData("MOV WORD [counter], EAX")]
        [InlineData("MOV DWORD [counter], AL")]
        public void DisagreeingSizeHint32_Throws(string instruction)
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                MovTestHarness.TryCompile32(Asm.WithData([Counter],
                    "MOV EAX, 1",
                    instruction)));

            Assert.Contains("disagrees with source register", ex.Message);
        }

        [Theory]
        [InlineData("MOV DWORD [counter], RAX")]
        [InlineData("MOV BYTE [counter], RAX")]
        public void DisagreeingSizeHint64_Throws(string instruction)
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                MovTestHarness.TryCompile64(Asm.WithData([Counter],
                    "MOV RAX, 1",
                    instruction)));

            Assert.Contains("disagrees with source register", ex.Message);
        }

        /// <summary>
        /// Contrast with <see cref="MovDirectImmediateTests"/>: IMMEDIATE requires an explicit
        /// hint because it has no register to imply one.  STORE does not.
        /// </summary>
        [Fact]
        public void UnhintedStore_IsAccepted()
        {
            var compilation = MovTestHarness.Compile32(
                Asm.WithData([Counter],
                    "MOV EAX, 0xDEADBEEF",
                    "MOV [counter], EAX"));
            var agent = MovTestHarness.Load32(compilation);
            MovTestHarness.RunToEnd(agent);

            var address = MovTestHarness.DataSymbolAddress(compilation, "counter");
            Assert.Equal(
                new byte[] { 0xEF, 0xBE, 0xAD, 0xDE, 0, 0, 0, 0 },
                agent.PeekMemory(address, 8));
        }

        #endregion

        #region Rejected forms

        /// <summary>
        /// Memory-to-memory is not implemented (and real x86 doesn't allow it either).  Pinned
        /// as characterisation so whoever eventually implements it knows a test is watching.
        /// </summary>
        [Fact]
        public void MemoryToMemoryStore_Throws() =>
            Assert.Throws<NotImplementedException>(() =>
                MovTestHarness.TryCompile32(Asm.WithData(
                    ["src db 0, 0, 0, 0",
                     "dst db 0, 0, 0, 0"],
                    "MOV DWORD [dst], [src]")));

        #endregion
    }
}
