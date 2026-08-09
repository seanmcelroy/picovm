using System;
using System.Linq;
using picovm.Assembler;
using picovm.Tests.Support;
using picovm.VM;
using Xunit;

namespace picovm.Tests
{
    /// <summary>
    /// Coverage for <c>MOV_IMMEDIATE</c>, which the compiler emits from three distinct paths
    /// that all produce the same opcode byte: a literal constant, the address of a symbol
    /// ("address-of"), and a symbol whose value is a compile-time constant that gets inlined
    /// into the text segment rather than relocated.
    /// </summary>
    public class MovImmediateTests
    {
        #region Literal immediates: widths and boundaries

        [Theory]
        [InlineData("MOV AL, 0", 0x00)]
        [InlineData("MOV AL, 1", 0x01)]
        [InlineData("MOV AL, 255", 0xFF)]
        [InlineData("MOV AL, 0x5A", 0x5A)]
        public void Byte_Boundaries(string instruction, byte expected) =>
            Assert.Equal(expected, MovTestHarness.Run32(Asm.Text(instruction)).ReadHalfRegister(Register.AL));

        [Theory]
        [InlineData("MOV AX, 0", 0x0000)]
        [InlineData("MOV AX, 1", 0x0001)]
        [InlineData("MOV AX, 65535", 0xFFFF)]
        [InlineData("MOV AX, 0xBEEF", 0xBEEF)]
        public void Word_Boundaries(string instruction, ushort expected) =>
            Assert.Equal(expected, MovTestHarness.Run32(Asm.Text(instruction)).ReadRegister(Register.AX));

        [Theory]
        [InlineData("MOV EAX, 0", 0x00000000U)]
        [InlineData("MOV EAX, 1", 0x00000001U)]
        [InlineData("MOV EAX, 4294967295", 0xFFFFFFFFU)]
        [InlineData("MOV EAX, 0x12345678", 0x12345678U)]
        public void Dword_Boundaries(string instruction, uint expected) =>
            Assert.Equal(expected, MovTestHarness.Run32(Asm.Text(instruction)).ReadExtendedRegister(Register.EAX));

        [Theory]
        [InlineData("MOV RAX, 0", 0x0000000000000000UL)]
        [InlineData("MOV RAX, 1", 0x0000000000000001UL)]
        [InlineData("MOV RAX, 18446744073709551615", 0xFFFFFFFFFFFFFFFFUL)]
        [InlineData("MOV RAX, 0x123456789ABCDEF0", 0x123456789ABCDEF0UL)]
        public void Qword_Boundaries(string instruction, ulong expected) =>
            Assert.Equal(expected, MovTestHarness.Run64(Asm.Text(instruction)).ReadR64Register(Register.RAX));

        #endregion

        #region Literal formats

        /// <summary>
        /// The assembler accepts decimal, C-style <c>0x</c> hex, and NASM-style trailing-<c>h</c>
        /// hex.  All three must produce the same immediate.
        /// </summary>
        [Theory]
        [InlineData("255")]
        [InlineData("0xFF")]
        [InlineData("0xff")]
        [InlineData("0FFh")]
        [InlineData("0ffH")]
        public void LiteralFormats_AreEquivalent(string literal) =>
            Assert.Equal((byte)0xFF, MovTestHarness.Run32(Asm.Text($"MOV AL, {literal}")).ReadHalfRegister(Register.AL));

        /// <summary>
        /// A trailing-h literal must start with a numeral, which is why the assembler's own
        /// samples write <c>0FFh</c> rather than <c>FFh</c>.  Without the leading zero the token
        /// is indistinguishable from a symbol name and is treated as one.
        /// </summary>
        [Fact]
        public void HexSuffixWithoutLeadingNumeral_IsTreatedAsASymbol()
        {
            var result = MovTestHarness.TryCompile32(Asm.Text("MOV AL, FFh"));
            Assert.NotEmpty(result.Errors);
            Assert.Contains(result.Errors, e => e.Message.Contains("FFh", StringComparison.OrdinalIgnoreCase));
        }

        #endregion

        #region Immediates that do not fit

        /// <summary>
        /// An immediate too wide for its destination is rejected by the constant parser rather
        /// than silently truncated.
        /// </summary>
        [Theory]
        [InlineData("MOV AL, 256")]
        [InlineData("MOV AL, 0x100")]
        [InlineData("MOV AX, 65536")]
        [InlineData("MOV AX, 0x10000")]
        public void OverwideImmediate32_Throws(string instruction) =>
            Assert.Throws<OverflowException>(() => MovTestHarness.TryCompile32(Asm.Text(instruction)));

        [Fact]
        public void OverwideImmediate_Dword_Throws() =>
            Assert.Throws<OverflowException>(() => MovTestHarness.TryCompile32(Asm.Text("MOV EAX, 4294967296")));

        #endregion

        #region Size hints

        /// <summary>
        /// A hint that agrees with the destination register is redundant but harmless.
        /// </summary>
        [Theory]
        [InlineData("MOV BYTE AL, 0x5A", "AL")]
        [InlineData("MOV WORD AX, 0x5A", "AX")]
        [InlineData("MOV DWORD EAX, 0x5A", "EAX")]
        public void AgreeingSizeHint_IsAccepted(string instruction, string _)
        {
            var agent = MovTestHarness.Run32(Asm.Text(instruction));
            Assert.Equal((byte)0x5A, agent.ReadHalfRegister(Register.AL));
        }

        /// <summary>
        /// A hint that disagrees with the destination register is a compile error.  It used to
        /// size the immediate from the hint while the VM sized its read from the register,
        /// which desynchronised the instruction stream and corrupted every instruction after
        /// it -- with no diagnostic at all.
        /// </summary>
        [Theory]
        [InlineData("MOV BYTE EAX, 5")]
        [InlineData("MOV WORD EAX, 5")]
        [InlineData("MOV DWORD AL, 5")]
        [InlineData("MOV QWORD EAX, 5")]
        public void DisagreeingSizeHint32_Throws(string instruction)
        {
            var ex = Assert.Throws<InvalidOperationException>(() => MovTestHarness.TryCompile32(Asm.Text(instruction)));
            Assert.Contains("disagrees with destination register", ex.Message);
        }

        [Theory]
        [InlineData("MOV DWORD RAX, 5")]
        [InlineData("MOV BYTE RAX, 5")]
        public void DisagreeingSizeHint64_Throws(string instruction)
        {
            var ex = Assert.Throws<InvalidOperationException>(() => MovTestHarness.TryCompile64(Asm.Text(instruction)));
            Assert.Contains("disagrees with destination register", ex.Message);
        }

        #endregion

        #region Symbol immediates (address-of)

        /// <summary>
        /// <c>MOV ECX, msg</c> loads the <em>address</em> of a data symbol, not its contents.
        /// </summary>
        [Fact]
        public void DataSymbol_LoadsItsAddress32()
        {
            var compilation = MovTestHarness.Compile32(
                Asm.WithData(["msg db 'Hi', 0"], "MOV ECX, msg"));
            var agent = MovTestHarness.Load32(compilation);
            MovTestHarness.RunToEnd(agent);

            var expected = MovTestHarness.DataSymbolAddress(compilation, "msg");
            Assert.Equal(expected, agent.ReadExtendedRegister(Register.ECX));

            // And that address really does point at the symbol's bytes.
            Assert.Equal("Hi"u8.ToArray(), agent.PeekMemory(expected, 2));
        }

        [Fact]
        public void DataSymbol_LoadsItsAddress64()
        {
            var compilation = MovTestHarness.Compile64(
                Asm.WithData(["msg db 'Hi', 0"], "MOV RCX, msg"));
            var agent = MovTestHarness.Load64(compilation);
            MovTestHarness.RunToEnd(agent);

            var expected = MovTestHarness.DataSymbolAddress(compilation, "msg");
            Assert.Equal(expected, agent.ReadR64Register(Register.RCX));
            Assert.Equal("Hi"u8.ToArray(), agent.PeekMemory(expected, 2));
        }

        /// <summary>
        /// The address is resolved for the symbol, not for the instruction, so referencing the
        /// same symbol twice must relocate both sites to the same value.
        /// </summary>
        [Fact]
        public void SymbolReferencedTwice_RelocatesBothSites()
        {
            var compilation = MovTestHarness.Compile32(
                Asm.WithData(["msg db 'Hi', 0"], "MOV ECX, msg", "MOV EDX, msg"));
            var agent = MovTestHarness.Load32(compilation);
            MovTestHarness.RunToEnd(agent);

            var expected = MovTestHarness.DataSymbolAddress(compilation, "msg");
            Assert.Equal(expected, agent.ReadExtendedRegister(Register.ECX));
            Assert.Equal(expected, agent.ReadExtendedRegister(Register.EDX));
        }

        /// <summary>
        /// Symbols are resolved after the whole text section is assembled, so a symbol may be
        /// referenced before the section that defines it.
        /// </summary>
        [Fact]
        public void ForwardReference_Resolves()
        {
            // .text is emitted before .data here, so the reference precedes the definition.
            var compilation = MovTestHarness.Compile32(
                "section .text",
                "global _start",
                "_start:",
                "MOV ECX, msg",
                "END",
                "section .data",
                "msg db 'Hi', 0");
            var agent = MovTestHarness.Load32(compilation);
            MovTestHarness.RunToEnd(agent);

            Assert.Equal(MovTestHarness.DataSymbolAddress(compilation, "msg"), agent.ReadExtendedRegister(Register.ECX));
        }

        [Fact]
        public void UndefinedSymbol_IsACompileError()
        {
            var result = MovTestHarness.TryCompile32(
                Asm.WithData(["msg db 'Hi', 0"], "MOV ECX, msg", "MOV EDX, nosuchsymbol"));

            Assert.NotEmpty(result.Errors);
            Assert.Contains(result.Errors, e => e.Message.Contains("NOSUCHSYMBOL", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void SymbolWithNoDataSection_IsACompileError()
        {
            var result = MovTestHarness.TryCompile32(Asm.Text("MOV ECX, msg"));

            Assert.NotEmpty(result.Errors);
            Assert.Contains(result.Errors, e => e.Message.Contains("undefined", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// A <c>.bss</c> symbol is addressed above the text and data segments.  Asserted on the
        /// relocated bytes rather than by execution, because a large BSS address is not
        /// reachable inside the agent's 64KB memory.
        /// </summary>
        [Fact]
        public void BssSymbol_RelocatesAboveTextAndData32()
        {
            var compilation = MovTestHarness.Compile32(
                Asm.WithBss(["msg db 'Hi', 0"], ["buffer resb 16"], "MOV ECX, msg", "MOV EDX, buffer"));

            var expected = compilation.TextSegmentSize!.Value + compilation.DataSegmentSize!.Value;

            // Locate the relocated immediate belonging to the buffer reference.
            var reference = compilation.TextSymbolReferenceOffsets.Single(r => r.Name == "BUFFER");
            var actual = BitConverter.ToUInt32([.. compilation.TextSegment!.Value], (int)reference.TextSegmentReferenceOffset);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void BssSymbol_RelocatesFullWidth64()
        {
            var compilation = MovTestHarness.Compile64(
                Asm.WithBss(["msg db 'Hi', 0"], ["buffer resb 16"], "MOV RCX, msg", "MOV RDX, buffer"));

            var reference = compilation.TextSymbolReferenceOffsets.Single(r => r.Name == "BUFFER");
            Assert.Equal(8, reference.ReferenceLength);

            var expected = (ulong)compilation.TextSegmentSize!.Value + compilation.DataSegmentSize!.Value;
            var actual = BitConverter.ToUInt64([.. compilation.TextSegment!.Value], (int)reference.TextSegmentReferenceOffset);

            Assert.Equal(expected, actual);
        }

        #endregion

        #region Constant inlining

        /// <summary>
        /// A symbol defined with <c>equ</c> has no storage: its value is inlined into the text
        /// segment at relocation time instead of its address being taken.  This is the path
        /// <c>hello-world-linux32.asm</c> uses for its message length.
        /// </summary>
        [Fact]
        public void EquConstant_IsInlinedByValue32()
        {
            var compilation = MovTestHarness.Compile32(
                Asm.WithData(
                    [
                        "msg db 'Hello, world!', 0xa",
                        "len equ $ - msg"
                    ],
                    "MOV ECX, msg",
                    "MOV EDX, len"));
            var agent = MovTestHarness.Load32(compilation);
            MovTestHarness.RunToEnd(agent);

            // 'Hello, world!' is 13 characters plus the newline.
            Assert.Equal(14U, agent.ReadExtendedRegister(Register.EDX));
        }

        [Fact]
        public void EquConstant_IsInlinedByValue64()
        {
            var compilation = MovTestHarness.Compile64(
                Asm.WithData(
                    [
                        "msg db 'Hello world!', 10",
                        "len equ $ - msg"
                    ],
                    "MOV RCX, msg",
                    "MOV RDX, len"));
            var agent = MovTestHarness.Load64(compilation);
            MovTestHarness.RunToEnd(agent);

            Assert.Equal(13UL, agent.ReadR64Register(Register.RDX));
        }

        /// <summary>
        /// The inlining path widens the stored constant to the destination register's width.
        /// </summary>
        [Theory]
        [InlineData("EDX", 14U)]
        [InlineData("ECX", 14U)]
        public void EquConstant_WidensToDestination32(string register, uint expected)
        {
            var compilation = MovTestHarness.Compile32(
                Asm.WithData(
                    [
                        "msg db 'Hello, world!', 0xa",
                        "len equ $ - msg"
                    ],
                    "MOV EAX, msg",
                    $"MOV {register}, len"));
            var agent = MovTestHarness.Load32(compilation);
            MovTestHarness.RunToEnd(agent);

            Assert.Equal(expected, agent.ReadExtendedRegister(Enum.Parse<Register>(register)));
        }

        #endregion

        #region Overlay behaviour

        /// <summary>
        /// A narrow immediate leaves the rest of the destination register alone, while a
        /// full-width one replaces it.  This is the behaviour the original
        /// <c>MOV_IMMEDIATE_Overlayed</c> test covered; kept here in parameterised form.
        /// </summary>
        [Fact]
        public void NarrowImmediates_PreserveSurroundingBits()
        {
            var agent = MovTestHarness.Boot32(Asm.Text(
                "MOV EAX, 0xFFFFFFFF",
                "MOV AX, 0",
                "MOV AH, 0xAA",
                "MOV AL, 0x55",
                "MOV EAX, 0"));

            MovTestHarness.Step(agent);
            Assert.Equal(0xFFFFFFFFU, agent.ReadExtendedRegister(Register.EAX));

            MovTestHarness.Step(agent);
            Assert.Equal(0xFFFF0000U, agent.ReadExtendedRegister(Register.EAX));

            MovTestHarness.Step(agent);
            Assert.Equal(0xFFFFAA00U, agent.ReadExtendedRegister(Register.EAX));

            MovTestHarness.Step(agent);
            Assert.Equal(0xFFFFAA55U, agent.ReadExtendedRegister(Register.EAX));

            MovTestHarness.Step(agent);
            Assert.Equal(0x00000000U, agent.ReadExtendedRegister(Register.EAX));

            Assert.Equal(0, MovTestHarness.RunToEnd(agent));
        }

        [Fact]
        public void NarrowImmediates_PreserveSurroundingBits64()
        {
            var agent = MovTestHarness.Boot64(Asm.Text(
                "MOV RAX, 0xFFFFFFFFFFFFFFFF",
                "MOV AX, 0x7777",
                "MOV AL, 0x11"));

            MovTestHarness.Step(agent);
            Assert.Equal(0xFFFFFFFFFFFFFFFFUL, agent.ReadR64Register(Register.RAX));

            MovTestHarness.Step(agent);
            Assert.Equal(0xFFFFFFFFFFFF7777UL, agent.ReadR64Register(Register.RAX));

            MovTestHarness.Step(agent);
            Assert.Equal(0xFFFFFFFFFFFF7711UL, agent.ReadR64Register(Register.RAX));
        }

        #endregion
    }
}
