using System;
using System.Linq;
using picovm.Assembler;
using picovm.Tests.Support;
using Xunit;

namespace picovm.Tests
{
    /// <summary>
    /// Byte-exact assertions on what the compiler emits for each MOV form, independent of
    /// whether the VM decodes it correctly.
    /// </summary>
    /// <remarks>
    /// Execution tests alone cannot tell you whether a failure came from the compiler emitting
    /// the wrong bytes or the VM misreading correct ones.  These tests pin the encoding down so
    /// that split is unambiguous.  They also pin instruction <em>lengths</em>, which the older
    /// tests probed only indirectly by checking that <c>END</c> arrived on an expected tick.
    /// </remarks>
    public class MovEncodingTests
    {
        private static byte[] Text32(params string[] instructions) =>
            [.. MovTestHarness.Compile32(Asm.Text(instructions)).TextSegment!.Value];

        private static byte[] Text64(params string[] instructions) =>
            [.. MovTestHarness.Compile64(Asm.Text(instructions)).TextSegment!.Value];

        #region MOV_REGISTER

        [Theory]
        [InlineData("MOV EAX, EBX", Register.EAX, Register.EBX)]
        [InlineData("MOV AX, BX", Register.AX, Register.BX)]
        [InlineData("MOV AH, BL", Register.AH, Register.BL)]
        [InlineData("MOV ESI, EDI", Register.ESI, Register.EDI)]
        public void MovRegister_Encoding32(string instruction, Register dst, Register src) =>
            Assert.Equal(
                [(byte)Bytecode.MOV_REGISTER, (byte)dst, (byte)src, (byte)Bytecode.END],
                Text32(instruction));

        [Theory]
        [InlineData("MOV RAX, RBX", Register.RAX, Register.RBX)]
        [InlineData("MOV R8, R9", Register.R8, Register.R9)]
        [InlineData("MOV RAX, EBX", Register.RAX, Register.EBX)]
        public void MovRegister_Encoding64(string instruction, Register dst, Register src) =>
            Assert.Equal(
                [(byte)Bytecode.MOV_REGISTER, (byte)dst, (byte)src, (byte)Bytecode.END],
                Text64(instruction));

        #endregion

        #region MOV_IMMEDIATE

        [Fact]
        public void MovImmediate_Byte_Encoding() =>
            Assert.Equal(
                [(byte)Bytecode.MOV_IMMEDIATE, (byte)Register.AL, 0x41, (byte)Bytecode.END],
                Text32("MOV AL, 0x41"));

        [Fact]
        public void MovImmediate_Word_Encoding() =>
            Assert.Equal(
                [(byte)Bytecode.MOV_IMMEDIATE, (byte)Register.AX, 0xEF, 0xBE, (byte)Bytecode.END],
                Text32("MOV AX, 0xBEEF"));

        [Fact]
        public void MovImmediate_Dword_Encoding() =>
            Assert.Equal(
                [(byte)Bytecode.MOV_IMMEDIATE, (byte)Register.EAX, 0x78, 0x56, 0x34, 0x12, (byte)Bytecode.END],
                Text32("MOV EAX, 0x12345678"));

        [Fact]
        public void MovImmediate_Qword_Encoding() =>
            Assert.Equal(
                [(byte)Bytecode.MOV_IMMEDIATE, (byte)Register.RAX, 0xF0, 0xDE, 0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12, (byte)Bytecode.END],
                Text64("MOV RAX, 0x123456789ABCDEF0"));

        /// <summary>
        /// The immediate is little-endian.  A palindromic literal would hide a byte-order bug,
        /// so this uses one whose bytes are all distinct.
        /// </summary>
        [Fact]
        public void MovImmediate_IsLittleEndian() =>
            Assert.Equal(
                [0x78, 0x56, 0x34, 0x12],
                Text32("MOV EAX, 0x12345678").Skip(2).Take(4));

        #endregion

        #region MOV_INDIRECT

        [Theory]
        [InlineData("MOV EAX, [EBX]", Register.EAX, Register.EBX)]
        [InlineData("MOV DX, [EBX]", Register.DX, Register.EBX)]
        [InlineData("MOV AL, [ECX]", Register.AL, Register.ECX)]
        public void MovIndirect_Encoding32(string instruction, Register dst, Register src) =>
            Assert.Equal(
                [(byte)Bytecode.MOV_INDIRECT, (byte)dst, (byte)src, (byte)Bytecode.END],
                Text32(instruction));

        [Theory]
        [InlineData("MOV RAX, [RBX]", Register.RAX, Register.RBX)]
        [InlineData("MOV EDX, [RBX]", Register.EDX, Register.RBX)]
        public void MovIndirect_Encoding64(string instruction, Register dst, Register src) =>
            Assert.Equal(
                [(byte)Bytecode.MOV_INDIRECT, (byte)dst, (byte)src, (byte)Bytecode.END],
                Text64(instruction));

        #endregion

        #region MOV_DIRECT

        /// <summary>
        /// Unlike every other MOV, the destination is a bare address with no register to imply
        /// a width, so the encoding carries an explicit size byte between the address and the
        /// immediate.  The address itself is always machine-width.
        /// </summary>
        [Theory]
        [InlineData("BYTE", "0x41", new byte[] { 0x41 })]
        [InlineData("WORD", "0xBEEF", new byte[] { 0xEF, 0xBE })]
        [InlineData("DWORD", "0xDEADBEEF", new byte[] { 0xEF, 0xBE, 0xAD, 0xDE })]
        public void MovDirect_Encoding32(string hint, string literal, byte[] expectedImmediate)
        {
            var compilation = MovTestHarness.Compile32(
                Asm.WithData(["counter db 0, 0, 0, 0, 0, 0, 0, 0"], $"MOV {hint} [counter], {literal}"));
            var text = compilation.TextSegment!.Value;
            var address = MovTestHarness.DataSymbolAddress(compilation, "counter");

            byte[] expected =
            [
                (byte)Bytecode.MOV_DIRECT,
                .. BitConverter.GetBytes(address),      // 4-byte address
                (byte)expectedImmediate.Length,          // explicit operand size
                .. expectedImmediate,
                (byte)Bytecode.END
            ];

            Assert.Equal(expected, text);
        }

        [Theory]
        [InlineData("BYTE", "0x41", new byte[] { 0x41 })]
        [InlineData("WORD", "0xBEEF", new byte[] { 0xEF, 0xBE })]
        [InlineData("DWORD", "0xDEADBEEF", new byte[] { 0xEF, 0xBE, 0xAD, 0xDE })]
        [InlineData("QWORD", "0x1122334455667788", new byte[] { 0x88, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11 })]
        public void MovDirect_Encoding64(string hint, string literal, byte[] expectedImmediate)
        {
            var compilation = MovTestHarness.Compile64(
                Asm.WithData(["counter db 0, 0, 0, 0, 0, 0, 0, 0"], $"MOV {hint} [counter], {literal}"));
            var text = compilation.TextSegment!.Value;
            var address = MovTestHarness.DataSymbolAddress(compilation, "counter");

            byte[] expected =
            [
                (byte)Bytecode.MOV_DIRECT,
                .. BitConverter.GetBytes(address),      // 8-byte address
                (byte)expectedImmediate.Length,
                .. expectedImmediate,
                (byte)Bytecode.END
            ];

            Assert.Equal(expected, text);
        }

        #endregion

        #region Instruction lengths

        /// <summary>
        /// Pins the length of every MOV form directly, rather than inferring it from where a
        /// later instruction lands.
        /// </summary>
        [Theory]
        // opcode + dst + src
        [InlineData("MOV EAX, EBX", 3)]
        [InlineData("MOV EAX, [EBX]", 3)]
        // opcode + dst + immediate
        [InlineData("MOV AL, 1", 3)]
        [InlineData("MOV AX, 1", 4)]
        [InlineData("MOV EAX, 1", 6)]
        public void InstructionLength32(string instruction, int expectedLength) =>
            Assert.Equal(expectedLength + 1, Text32(instruction).Length); // +1 for the trailing END

        [Theory]
        [InlineData("MOV RAX, RBX", 3)]
        [InlineData("MOV RAX, [RBX]", 3)]
        [InlineData("MOV AL, 1", 3)]
        [InlineData("MOV AX, 1", 4)]
        [InlineData("MOV EAX, 1", 6)]
        [InlineData("MOV RAX, 1", 10)]
        public void InstructionLength64(string instruction, int expectedLength) =>
            Assert.Equal(expectedLength + 1, Text64(instruction).Length);

        /// <summary>
        /// MOV_DIRECT is 1 opcode + machine-width address + 1 size byte + the immediate.
        /// </summary>
        [Theory]
        [InlineData("BYTE", "1", 1 + 4 + 1 + 1)]
        [InlineData("WORD", "1", 1 + 4 + 1 + 2)]
        [InlineData("DWORD", "1", 1 + 4 + 1 + 4)]
        public void MovDirect_InstructionLength32(string hint, string literal, int expectedLength) =>
            Assert.Equal(
                expectedLength + 1,
                MovTestHarness.Compile32(Asm.WithData(["counter db 0, 0, 0, 0, 0, 0, 0, 0"], $"MOV {hint} [counter], {literal}"))
                    .TextSegment!.Value.Length);

        [Theory]
        [InlineData("BYTE", "1", 1 + 8 + 1 + 1)]
        [InlineData("WORD", "1", 1 + 8 + 1 + 2)]
        [InlineData("DWORD", "1", 1 + 8 + 1 + 4)]
        [InlineData("QWORD", "1", 1 + 8 + 1 + 8)]
        public void MovDirect_InstructionLength64(string hint, string literal, int expectedLength) =>
            Assert.Equal(
                expectedLength + 1,
                MovTestHarness.Compile64(Asm.WithData(["counter db 0, 0, 0, 0, 0, 0, 0, 0"], $"MOV {hint} [counter], {literal}"))
                    .TextSegment!.Value.Length);

        #endregion
    }
}
