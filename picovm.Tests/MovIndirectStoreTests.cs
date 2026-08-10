using System;
using picovm.Tests.Support;
using picovm.VM;
using Xunit;

namespace picovm.Tests
{
    /// <summary>
    /// Coverage for <c>MOV_INDIRECT_STORE</c> (<c>MOV [EBX], EAX</c>), where the destination
    /// register holds an address rather than a value and the <em>source</em> register's width
    /// decides how many bytes are written.
    /// </summary>
    public class MovIndirectStoreTests
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
        /// As <see cref="ScratchBuffer32"/>, but with a ninth sentinel byte so an 8-byte
        /// (qword) store still has a guard byte past its end.
        /// </summary>
        private static string[] ScratchBuffer64(params string[] instructions) =>
            Asm.WithData(
                ["buffer db 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF"],
                ["MOV RBX, buffer", .. instructions]);

        #region Source width decides the store width

        [Fact]
        public void ByteStore_WritesOnlyOneByte32()
        {
            var compilation = MovTestHarness.Compile32(ScratchBuffer32(
                "MOV AL, 0x42",
                "MOV [EBX], AL"));
            var agent = MovTestHarness.Load32(compilation);
            MovTestHarness.RunToEnd(agent);

            var address = MovTestHarness.DataSymbolAddress(compilation, "buffer");
            Assert.Equal(
                new byte[] { 0x42, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF },
                agent.PeekMemory(address, 8));
        }

        [Fact]
        public void WordStore_WritesOnlyTwoBytes32()
        {
            var compilation = MovTestHarness.Compile32(ScratchBuffer32(
                "MOV DX, 0xBEEF",
                "MOV [EBX], DX"));
            var agent = MovTestHarness.Load32(compilation);
            MovTestHarness.RunToEnd(agent);

            var address = MovTestHarness.DataSymbolAddress(compilation, "buffer");
            Assert.Equal(
                new byte[] { 0xEF, 0xBE, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF },
                agent.PeekMemory(address, 8));
        }

        [Fact]
        public void DwordStore_WritesOnlyFourBytes32()
        {
            var compilation = MovTestHarness.Compile32(ScratchBuffer32(
                "MOV EAX, 0xDEADBEEF",
                "MOV [EBX], EAX"));
            var agent = MovTestHarness.Load32(compilation);
            MovTestHarness.RunToEnd(agent);

            var address = MovTestHarness.DataSymbolAddress(compilation, "buffer");
            Assert.Equal(
                new byte[] { 0xEF, 0xBE, 0xAD, 0xDE, 0xFF, 0xFF, 0xFF, 0xFF },
                agent.PeekMemory(address, 8));
        }

        [Fact]
        public void QwordStore_WritesOnlyEightBytes64()
        {
            var compilation = MovTestHarness.Compile64(ScratchBuffer64(
                "MOV RAX, 0xDEADBEEFCAFEB00B",
                "MOV [RBX], RAX"));
            var agent = MovTestHarness.Load64(compilation);
            MovTestHarness.RunToEnd(agent);

            var address = MovTestHarness.DataSymbolAddress(compilation, "buffer");
            Assert.Equal(
                new byte[] { 0x0B, 0xB0, 0xFE, 0xCA, 0xEF, 0xBE, 0xAD, 0xDE, 0xFF },
                agent.PeekMemory(address, 9));
        }

        #endregion

        #region Address register widths

        /// <summary>
        /// The address may be held in a register of any width; only its value matters.
        /// </summary>
        [Fact]
        public void AddressInWordRegister32()
        {
            var compilation = MovTestHarness.Compile32(ScratchBuffer32(
                "MOV CX, BX",          // narrow the address into a 16-bit register
                "MOV AL, 0x42",
                "MOV [CX], AL"));
            var agent = MovTestHarness.Load32(compilation);
            MovTestHarness.RunToEnd(agent);

            var address = MovTestHarness.DataSymbolAddress(compilation, "buffer");
            Assert.Equal((byte)0x42, agent.PeekMemory(address, 1)[0]);
        }

        /// <summary>
        /// In 64-bit mode the address register may be named at any width, and the agent writes
        /// through the matching accessor.  <c>ScratchBuffer64</c> leaves the address in RBX,
        /// and EBX and BX alias its low bytes, so all three name the same address.
        /// </summary>
        [Theory]
        [InlineData("RBX")]   // 8-byte address register
        [InlineData("EBX")]   // 4-byte
        [InlineData("BX")]    // 2-byte
        public void AddressRegisterWidths64(string addressRegister)
        {
            var compilation = MovTestHarness.Compile64(ScratchBuffer64(
                "MOV AL, 0x42",
                $"MOV [{addressRegister}], AL"));
            var agent = MovTestHarness.Load64(compilation);
            MovTestHarness.RunToEnd(agent);

            var address = MovTestHarness.DataSymbolAddress(compilation, "buffer");
            Assert.Equal((byte)0x42, agent.PeekMemory(address, 1)[0]);
        }

        /// <summary>
        /// An 8-bit address register can only reach the first 256 bytes of memory.  The target
        /// offset here is chosen well past the end of this short program's own bytecode, so the
        /// store cannot corrupt instructions still to be executed.
        /// </summary>
        [Fact]
        public void AddressInHalfRegister64_WritesLowMemory()
        {
            var agent = MovTestHarness.Run64(Asm.Text(
                "MOV AL, 0x42",
                "MOV BL, 100",
                "MOV [BL], AL"));

            Assert.Equal((byte)0x42, agent.PeekMemory(100, 1)[0]);
        }

        [Fact]
        public void AddressInHalfRegister_WritesLowMemory32()
        {
            var agent = MovTestHarness.Run32(Asm.Text(
                "MOV AL, 0x42",
                "MOV BL, 100",
                "MOV [BL], AL"));

            Assert.Equal((byte)0x42, agent.PeekMemory(100, 1)[0]);
        }

        #endregion

        #region Interaction with MOV_INDIRECT_LOAD

        /// <summary>
        /// A stored value must read back unchanged through the dereferencing load form.
        /// </summary>
        [Fact]
        public void StoreThenLoad_RoundTrips32()
        {
            var agent = MovTestHarness.Run32(ScratchBuffer32(
                "MOV EAX, 0xDEADBEEF",
                "MOV [EBX], EAX",
                "MOV ECX, [EBX]"));

            Assert.Equal(0xDEADBEEFU, agent.ReadExtendedRegister(Register.ECX));
        }

        [Fact]
        public void StoreThenLoad_RoundTrips64()
        {
            var agent = MovTestHarness.Run64(ScratchBuffer64(
                "MOV RAX, 0xDEADBEEFCAFEB00B",
                "MOV [RBX], RAX",
                "MOV RCX, [RBX]"));

            Assert.Equal(0xDEADBEEFCAFEB00BUL, agent.ReadR64Register(Register.RCX));
        }

        #endregion

        #region Bounds

        [Fact]
        public void DwordStorePastEndOfMemory_Throws32()
        {
            Assert.ThrowsAny<MemoryAccessViolationException>(() =>
                MovTestHarness.Run32(Asm.Text(
                    "MOV EBX, 0xFFFF",
                    "MOV EAX, 0xDEADBEEF",
                    "MOV [EBX], EAX")));
        }

        [Fact]
        public void QwordStorePastEndOfMemory_Throws64()
        {
            Assert.ThrowsAny<MemoryAccessViolationException>(() =>
                MovTestHarness.Run64(Asm.Text(
                    "MOV RBX, 0xFFFF",
                    "MOV RAX, 0xDEADBEEFCAFEB00B",
                    "MOV [RBX], RAX")));
        }

        [Fact]
        public void ByteStoreAtLastAddress_Succeeds()
        {
            var agent = MovTestHarness.Run32(Asm.Text(
                "MOV EBX, 0xFFFF",
                "MOV AL, 0x42",
                "MOV [EBX], AL"));

            Assert.Equal((byte)0x42, agent.PeekMemory(0xFFFF, 1)[0]);
        }

        /// <summary>
        /// Pins that an out-of-range store reports itself accurately -- the width of the
        /// attempted access and <c>IsWrite = true</c> -- rather than just any exception type.
        /// </summary>
        [Fact]
        public void ByteStoreOutOfBounds_ReportsWidthAndIsWrite()
        {
            var ex = Assert.ThrowsAny<MemoryAccessViolationException>(() =>
                MovTestHarness.Run32(Asm.Text(
                    "MOV EBX, 0x10000",
                    "MOV AL, 0x42",
                    "MOV [EBX], AL")));

            Assert.Equal(1, ex.Width);
            Assert.True(ex.IsWrite);
        }

        #endregion
    }
}
