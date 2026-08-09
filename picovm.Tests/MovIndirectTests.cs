using System;
using picovm.Tests.Support;
using picovm.VM;
using Xunit;

namespace picovm.Tests
{
    /// <summary>
    /// Coverage for <c>MOV_INDIRECT</c> (<c>MOV EAX, [EBX]</c>), where the source register holds
    /// an address rather than a value and the <em>destination</em> register's width decides how
    /// many bytes are loaded.
    /// </summary>
    public class MovIndirectTests
    {
        /// <summary>
        /// A 32-bit program that seeds <see cref="Asm.ScratchAddress"/> with a known dword by
        /// way of the data segment, then leaves EBX pointing at it.
        /// </summary>
        private static string[] Seeded32(params string[] instructions) =>
            Asm.WithData(
                ["source db 0xEF, 0xBE, 0xAD, 0xDE, 0x0B, 0xB0, 0xFE, 0xCA"],
                ["MOV EBX, source", .. instructions]);

        private static string[] Seeded64(params string[] instructions) =>
            Asm.WithData(
                ["source db 0x0B, 0xB0, 0xFE, 0xCA, 0xEF, 0xBE, 0xAD, 0xDE"],
                ["MOV RBX, source", .. instructions]);

        #region Destination width decides the load width

        /// <summary>
        /// Memory at <c>source</c> is <c>EF BE AD DE</c>, i.e. little-endian 0xDEADBEEF.
        /// </summary>
        [Fact]
        public void DestinationWidthDecidesBytesRead32()
        {
            var agent = MovTestHarness.Run32(Seeded32(
                "MOV ECX, [EBX]",
                "MOV DX, [EBX]",
                "MOV AL, [EBX]"));

            Assert.Equal(0xDEADBEEFU, agent.ReadExtendedRegister(Register.ECX));
            Assert.Equal((ushort)0xBEEF, agent.ReadRegister(Register.DX));
            Assert.Equal((byte)0xEF, agent.ReadHalfRegister(Register.AL));
        }

        /// <summary>
        /// Memory at <c>source</c> is <c>0B B0 FE CA EF BE AD DE</c>, i.e. little-endian
        /// 0xDEADBEEFCAFEB00B.
        /// </summary>
        [Fact]
        public void DestinationWidthDecidesBytesRead64()
        {
            var agent = MovTestHarness.Run64(Seeded64(
                "MOV RCX, [RBX]",
                "MOV EDX, [RBX]",
                "MOV SI, [RBX]",
                "MOV AL, [RBX]"));

            Assert.Equal(0xDEADBEEFCAFEB00BUL, agent.ReadR64Register(Register.RCX));
            Assert.Equal(0xCAFEB00BU, agent.ReadExtendedRegister(Register.EDX));
            Assert.Equal((ushort)0xB00B, agent.ReadRegister(Register.SI));
            Assert.Equal((byte)0x0B, agent.ReadHalfRegister(Register.AL));
        }

        #endregion

        #region Overlay behaviour on the destination

        /// <summary>
        /// A narrow load must leave the untouched part of the destination register alone.  The
        /// destination is deliberately dirtied first: a test that zeroes it beforehand cannot
        /// tell "preserved the high bytes" from "cleared the whole register".
        /// </summary>
        [Fact]
        public void ByteLoad_PreservesUpperBitsOfDestination()
        {
            var agent = MovTestHarness.Run32(Seeded32(
                "MOV EAX, 0x11223344",
                "MOV AL, [EBX]"));

            Assert.Equal(0x112233EFU, agent.ReadExtendedRegister(Register.EAX));
        }

        [Fact]
        public void WordLoad_PreservesUpperBitsOfDestination()
        {
            var agent = MovTestHarness.Run32(Seeded32(
                "MOV EAX, 0x11223344",
                "MOV AX, [EBX]"));

            Assert.Equal(0x1122BEEFU, agent.ReadExtendedRegister(Register.EAX));
        }

        [Fact]
        public void DwordLoad_ZeroExtendsInto64BitRegister()
        {
            var agent = MovTestHarness.Run64(Seeded64(
                "MOV RAX, 0xFFFFFFFFFFFFFFFF",
                "MOV EAX, [RBX]"));

            Assert.Equal(0x00000000CAFEB00BUL, agent.ReadR64Register(Register.RAX));
        }

        #endregion

        #region Address register widths

        /// <summary>
        /// The address may be held in a register of any width; only its value matters.
        /// </summary>
        [Fact]
        public void AddressInWordRegister32()
        {
            var agent = MovTestHarness.Run32(Seeded32(
                "MOV CX, BX",          // narrow the address into a 16-bit register
                "MOV EAX, [CX]"));

            Assert.Equal(0xDEADBEEFU, agent.ReadExtendedRegister(Register.EAX));
        }

        /// <summary>
        /// In 64-bit mode the address register may be named at any width, and the agent reads it
        /// through the matching accessor.  <c>Seeded64</c> leaves the address in RBX, and EBX and
        /// BX alias its low bytes, so all three name the same (small) address.
        /// </summary>
        [Theory]
        [InlineData("RBX")]   // 8-byte address register
        [InlineData("EBX")]   // 4-byte
        [InlineData("BX")]    // 2-byte
        public void AddressRegisterWidths64(string addressRegister)
        {
            var agent = MovTestHarness.Run64(Seeded64($"MOV RAX, [{addressRegister}]"));

            Assert.Equal(0xDEADBEEFCAFEB00BUL, agent.ReadR64Register(Register.RAX));
        }

        /// <summary>
        /// An 8-bit address register in 64-bit mode, reaching into the text segment as it does
        /// in 32-bit mode.
        /// </summary>
        [Fact]
        public void AddressInHalfRegister64_ReadsLowMemory()
        {
            var compilation = MovTestHarness.Compile64(Asm.Text(
                "MOV BL, 2",
                "MOV AL, [BL]"));
            var agent = MovTestHarness.Load64(compilation);
            MovTestHarness.RunToEnd(agent);

            Assert.Equal(compilation.TextSegment!.Value[2], agent.ReadHalfRegister(Register.AL));
        }

        /// <summary>
        /// An 8-bit address register can only reach the first 256 bytes of memory, which is
        /// inside the text segment.  The load is still performed -- there is no addressability
        /// check -- so this pins that it reads the program's own bytes rather than failing.
        /// </summary>
        [Fact]
        public void AddressInHalfRegister_ReadsLowMemory()
        {
            var compilation = MovTestHarness.Compile32(Asm.Text(
                "MOV BL, 2",
                "MOV AL, [BL]"));
            var agent = MovTestHarness.Load32(compilation);
            MovTestHarness.RunToEnd(agent);

            Assert.Equal(compilation.TextSegment!.Value[2], agent.ReadHalfRegister(Register.AL));
        }

        #endregion

        #region Interaction with other MOV forms

        /// <summary>
        /// The address-of form and the dereference form must agree: taking a symbol's address
        /// with <c>MOV_IMMEDIATE</c> and dereferencing it with <c>MOV_INDIRECT</c> yields the
        /// bytes the data section declared.
        /// </summary>
        [Fact]
        public void AddressOfThenDereference_ReturnsDeclaredBytes()
        {
            var agent = MovTestHarness.Run32(Asm.WithData(
                ["value db 0x78, 0x56, 0x34, 0x12"],
                "MOV ECX, value",
                "MOV EAX, [ECX]"));

            Assert.Equal(0x12345678U, agent.ReadExtendedRegister(Register.EAX));
        }

        [Fact]
        public void AddressOfThenDereference_ReturnsDeclaredBytes64()
        {
            var agent = MovTestHarness.Run64(Asm.WithData(
                ["value db 0xF0, 0xDE, 0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12"],
                "MOV RCX, value",
                "MOV RAX, [RCX]"));

            Assert.Equal(0x123456789ABCDEF0UL, agent.ReadR64Register(Register.RAX));
        }

        /// <summary>
        /// Reading the same address at three widths must produce three consistent prefixes of
        /// the same little-endian value, and must not disturb memory.
        /// </summary>
        [Fact]
        public void RepeatedLoadsAtDifferentWidths_AreConsistent()
        {
            var compilation = MovTestHarness.Compile32(Seeded32(
                "MOV ECX, [EBX]",
                "MOV DX, [EBX]",
                "MOV AL, [EBX]"));
            var agent = MovTestHarness.Load32(compilation);
            MovTestHarness.RunToEnd(agent);

            var address = MovTestHarness.DataSymbolAddress(compilation, "source");
            Assert.Equal(new byte[] { 0xEF, 0xBE, 0xAD, 0xDE }, agent.PeekMemory(address, 4));
        }

        #endregion

        #region Bounds

        /// <summary>
        /// Nothing range-checks the dereference, so a wide load close to the top of the address
        /// space runs off the end of memory and surfaces as an argument exception rather than a
        /// VM-level fault.  Characterisation: if bounds checking is ever added, this should
        /// become an <c>ExecutionError</c> assertion.
        /// </summary>
        [Fact]
        public void LoadPastEndOfMemory_Throws()
        {
            // 0xFFFF is the last addressable byte, so a dword read starting there overruns.
            Assert.ThrowsAny<MemoryAccessViolationException>(() =>
                MovTestHarness.Run32(Asm.Text(
                    "MOV EBX, 0xFFFF",
                    "MOV EAX, [EBX]")));
        }

        [Fact]
        public void ByteLoadAtLastAddress_Succeeds()
        {
            var agent = MovTestHarness.Run32(Asm.Text(
                "MOV EBX, 0xFFFF",
                "MOV AL, [EBX]"));

            Assert.Equal((byte)0, agent.ReadHalfRegister(Register.AL));
        }

        #endregion
    }
}
