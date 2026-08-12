using System;
using picovm.Tests.Support;
using picovm.VM;
using Xunit;

namespace picovm.Tests
{
    /// <summary>
    /// Coverage for <c>MOV_INDIRECT_STORE_IMMEDIATE</c> (<c>MOV BYTE [EBX], 0x42</c>): storing
    /// a literal to an address held in a register.
    /// </summary>
    /// <remarks>
    /// Neither operand carries a register width and the destination is raw memory, so -- like
    /// the sister opcode <c>MOV_DIRECT_IMMEDIATE</c> -- the assembler bakes an explicit size
    /// byte into the instruction stream ahead of the immediate, and the VM decodes it before
    /// reading the immediate.  The encoding invariant at the bottom is what pins the size
    /// byte's presence and placement; the width tests exercise the store itself.
    /// </remarks>
    public class MovIndirectStoreImmediateTests
    {
        /// <summary>
        /// An 8-byte buffer seeded with a sentinel (0xFF), so a store narrower than the buffer
        /// leaves a visible, distinguishable tail: any sentinel byte that flips proves the
        /// store overran its declared width.
        /// </summary>
        private static string[] ScratchBuffer32(params string[] instructions) =>
            Asm.WithData(
                ["buffer db 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF"],
                ["MOV EBX, buffer", .. instructions]);

        /// <summary>
        /// As <see cref="ScratchBuffer32"/>, but with a ninth sentinel byte so a qword store
        /// still has a guard byte past its end.
        /// </summary>
        private static string[] ScratchBuffer64(params string[] instructions) =>
            Asm.WithData(
                ["buffer db 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF"],
                ["MOV RBX, buffer", .. instructions]);

        #region Type hint decides the store width

        [Theory]
        [InlineData("BYTE", "0x42", new byte[] { 0x42, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF })]
        [InlineData("WORD", "0xBEEF", new byte[] { 0xEF, 0xBE, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF })]
        [InlineData("DWORD", "0xDEADBEEF", new byte[] { 0xEF, 0xBE, 0xAD, 0xDE, 0xFF, 0xFF, 0xFF, 0xFF })]
        public void Store32(string hint, string literal, byte[] expected)
        {
            var compilation = MovTestHarness.Compile32(ScratchBuffer32($"MOV {hint} [EBX], {literal}"));
            var agent = MovTestHarness.Load32(compilation);
            MovTestHarness.RunToEnd(agent);

            var address = MovTestHarness.DataSymbolAddress(compilation, "buffer");
            Assert.Equal(expected, agent.PeekMemory(address, 8));
        }

        [Theory]
        [InlineData("BYTE", "0x42", new byte[] { 0x42, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF })]
        [InlineData("WORD", "0xBEEF", new byte[] { 0xEF, 0xBE, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF })]
        [InlineData("DWORD", "0xDEADBEEF", new byte[] { 0xEF, 0xBE, 0xAD, 0xDE, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF })]
        [InlineData("QWORD", "0x1122334455667788", new byte[] { 0x88, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11, 0xFF })]
        public void Store64(string hint, string literal, byte[] expected)
        {
            var compilation = MovTestHarness.Compile64(ScratchBuffer64($"MOV {hint} [RBX], {literal}"));
            var agent = MovTestHarness.Load64(compilation);
            MovTestHarness.RunToEnd(agent);

            var address = MovTestHarness.DataSymbolAddress(compilation, "buffer");
            Assert.Equal(expected, agent.PeekMemory(address, 9));
        }

        #endregion

        #region Narrow store leaves neighbours untouched

        /// <summary>
        /// Two indirect-immediates back to back: the second only decodes correctly if the first
        /// consumed exactly its declared width from the instruction stream, so this simultaneously
        /// checks store isolation and IP advancement.
        /// </summary>
        [Fact]
        public void NarrowStore_LeavesNeighbouringBytesUntouched32()
        {
            var compilation = MovTestHarness.Compile32(ScratchBuffer32(
                "MOV DWORD [EBX], 0",
                "MOV BYTE [EBX], 0x42"));
            var agent = MovTestHarness.Load32(compilation);
            MovTestHarness.RunToEnd(agent);

            var address = MovTestHarness.DataSymbolAddress(compilation, "buffer");
            Assert.Equal(
                new byte[] { 0x42, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF },
                agent.PeekMemory(address, 8));
        }

        [Fact]
        public void NarrowStore_LeavesNeighbouringBytesUntouched64()
        {
            var compilation = MovTestHarness.Compile64(ScratchBuffer64(
                "MOV QWORD [RBX], 0",
                "MOV BYTE [RBX], 0x42"));
            var agent = MovTestHarness.Load64(compilation);
            MovTestHarness.RunToEnd(agent);

            var address = MovTestHarness.DataSymbolAddress(compilation, "buffer");
            Assert.Equal(
                new byte[] { 0x42, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF },
                agent.PeekMemory(address, 9));
        }

        #endregion

        #region Round trip: store an immediate, load it back

        /// <summary>
        /// A value stored via the indirect immediate must read back unchanged through the
        /// dereferencing load -- pinning that this opcode and <c>MOV_INDIRECT_LOAD</c> agree on
        /// endianness and store width.
        /// </summary>
        [Fact]
        public void StoreThenLoad_RoundTrips32()
        {
            var agent = MovTestHarness.Run32(ScratchBuffer32(
                "MOV DWORD [EBX], 0xDEADBEEF",
                "MOV ECX, [EBX]"));

            Assert.Equal(0xDEADBEEFU, agent.ReadExtendedRegister(Register.ECX));
        }

        [Fact]
        public void StoreThenLoad_RoundTrips64()
        {
            var agent = MovTestHarness.Run64(ScratchBuffer64(
                "MOV QWORD [RBX], 0xDEADBEEFCAFEB00B",
                "MOV RCX, [RBX]"));

            Assert.Equal(0xDEADBEEFCAFEB00BUL, agent.ReadR64Register(Register.RCX));
        }

        #endregion

        #region Address register widths

        /// <summary>
        /// In 64-bit mode the address register may be named at any width; only its value matters.
        /// RBX, EBX and BX alias the low bytes of the same address after <c>ScratchBuffer64</c>
        /// loads the buffer's address.
        /// </summary>
        [Theory]
        [InlineData("RBX")]
        [InlineData("EBX")]
        [InlineData("BX")]
        public void AddressRegisterWidths64(string addressRegister)
        {
            var compilation = MovTestHarness.Compile64(ScratchBuffer64(
                $"MOV BYTE [{addressRegister}], 0x42"));
            var agent = MovTestHarness.Load64(compilation);
            MovTestHarness.RunToEnd(agent);

            var address = MovTestHarness.DataSymbolAddress(compilation, "buffer");
            Assert.Equal((byte)0x42, agent.PeekMemory(address, 1)[0]);
        }

        /// <summary>
        /// An 8-bit address register reaches only the first 256 bytes.  The target sits well past
        /// the tiny text segment so the store cannot corrupt an instruction still to run.
        /// </summary>
        [Fact]
        public void AddressInHalfRegister_WritesLowMemory32()
        {
            var agent = MovTestHarness.Run32(Asm.Text(
                "MOV BL, 100",
                "MOV BYTE [BL], 0x42"));

            Assert.Equal((byte)0x42, agent.PeekMemory(100, 1)[0]);
        }

        [Fact]
        public void AddressInHalfRegister_WritesLowMemory64()
        {
            var agent = MovTestHarness.Run64(Asm.Text(
                "MOV BL, 100",
                "MOV BYTE [BL], 0x42"));

            Assert.Equal((byte)0x42, agent.PeekMemory(100, 1)[0]);
        }

        #endregion

        #region Rejected forms

        /// <summary>
        /// The destination is raw memory and the immediate carries no width of its own, so the
        /// assembler cannot pick a store width without a hint.  Pinned so the requirement
        /// survives future refactors of the encoding helper.
        /// </summary>
        [Fact]
        public void UnhintedStore_IsRejected()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                MovTestHarness.TryCompile32(ScratchBuffer32("MOV [EBX], 5")));

            Assert.Contains("unhinted constant loads", ex.Message);
        }

        #endregion

        #region Bounds

        [Fact]
        public void DwordStorePastEndOfMemory_Throws32()
        {
            Assert.ThrowsAny<MemoryAccessViolationException>(() =>
                MovTestHarness.Run32(Asm.Text(
                    "MOV EBX, 0xFFFF",
                    "MOV DWORD [EBX], 0xDEADBEEF")));
        }

        [Fact]
        public void QwordStorePastEndOfMemory_Throws64()
        {
            Assert.ThrowsAny<MemoryAccessViolationException>(() =>
                MovTestHarness.Run64(Asm.Text(
                    "MOV RBX, 0xFFFF",
                    "MOV QWORD [RBX], 0xDEADBEEFCAFEB00B")));
        }

        [Fact]
        public void ByteStoreAtLastAddress_Succeeds32()
        {
            var agent = MovTestHarness.Run32(Asm.Text(
                "MOV EBX, 0xFFFF",
                "MOV BYTE [EBX], 0x42"));

            Assert.Equal((byte)0x42, agent.PeekMemory(0xFFFF, 1)[0]);
        }

        #endregion

        #region Encoding-level invariants

        /// <summary>
        /// The encoding is <c>opcode, ptr-reg, size, constant</c>: the size byte precedes the
        /// immediate so the VM knows how many immediate bytes to consume.  A missing or
        /// misplaced size byte would leave IP mid-immediate and eventually clobber the text
        /// segment as the runaway decoder wrote to memory-mapped addresses -- or wander off the
        /// end and trip <c>RunToEnd</c>'s tick cap.  Comparing the text segment to the compiled
        /// image catches both.
        /// </summary>
        [Fact]
        public void ExecutionLeavesTextSegmentIntact32()
        {
            var compilation = MovTestHarness.Compile32(ScratchBuffer32(
                "MOV BYTE [EBX], 0x42"));
            var agent = MovTestHarness.Load32(compilation);
            MovTestHarness.RunToEnd(agent);

            var textSize = (int)compilation.TextSegmentSize!.Value;
            Assert.Equal(compilation.TextSegment!.Value, agent.PeekMemory(0, textSize).ToArray());
        }

        [Fact]
        public void ExecutionLeavesTextSegmentIntact64()
        {
            var compilation = MovTestHarness.Compile64(ScratchBuffer64(
                "MOV QWORD [RBX], 0x1122334455667788"));
            var agent = MovTestHarness.Load64(compilation);
            MovTestHarness.RunToEnd(agent);

            var textSize = (int)compilation.TextSegmentSize!.Value;
            Assert.Equal(compilation.TextSegment!.Value, agent.PeekMemory(0, textSize).ToArray());
        }

        #endregion
    }
}
