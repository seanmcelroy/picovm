using System;
using System.Collections.Generic;
using picovm.Assembler;
using Xunit;

namespace picovm.Tests
{
    /// <summary>
    /// Verifies every jump mnemonic is recognized by the compiler and that the operand
    /// reserved for its target address matches the compiler's address width (4 bytes for
    /// <see cref="BytecodeCompiler{UInt32}"/>, 8 bytes for <see cref="BytecodeCompiler{UInt64}"/>)
    /// rather than a size hardcoded independently of <c>TAddrSize</c>.
    /// </summary>
    public class BytecodeCompilerJumpTest
    {
        public static IEnumerable<object[]> JumpMnemonics()
        {
            foreach (var mnemonic in new[]
            {
                "JZ", "JE", "JNZ", "JNE", "JO", "JNO", "JS", "JNS",
                "JB", "JNAE", "JC", "JNB", "JAE", "JNC",
                "JBE", "JNA", "JA", "JNBE",
                "JL", "JNGE", "JGE", "JNL", "JLE", "JNG", "JG", "JNLE",
                "JP", "JPE", "JNP", "JPO",
                "JCXZ", "JECXZ", "JMP"
            })
                yield return new object[] { mnemonic };
        }

        private static string[] ProgramText(string mnemonic, string addressRegister) => new[]
        {
            "section .data",
            "    dat db 0",
            "section .text",
            "global _start",
            "_start:",
            $"    {mnemonic} target",
            "target:",
            $"    MOV {addressRegister}, dat",
        };

        [Theory]
        [MemberData(nameof(JumpMnemonics))]
        public void Compiles_With_4Byte_Target_For_32Bit(string mnemonic)
        {
            var compiler = new BytecodeCompiler<UInt32>();
            var compiled = compiler.Compile(ProgramText(mnemonic, "EAX"), "UNIT_TEST");

            Assert.Empty(compiled.Errors);
            var text = compiled.TextSegment!.Value;

            Assert.Equal((byte)Enum.Parse<Bytecode>(mnemonic), text[0]);

            var target = BitConverter.ToUInt32(text.AsSpan(1, 4));
            Assert.Equal(5u, target); // 1 opcode byte + 4 operand bytes
        }

        [Theory]
        [MemberData(nameof(JumpMnemonics))]
        public void Compiles_With_8Byte_Target_For_64Bit(string mnemonic)
        {
            var compiler = new BytecodeCompiler<UInt64>();
            var compiled = compiler.Compile(ProgramText(mnemonic, "RAX"), "UNIT_TEST");

            Assert.Empty(compiled.Errors);
            var text = compiled.TextSegment!.Value;

            Assert.Equal((byte)Enum.Parse<Bytecode>(mnemonic), text[0]);

            var target = BitConverter.ToUInt64(text.AsSpan(1, 8));
            Assert.Equal(9ul, target); // 1 opcode byte + 8 operand bytes
        }
    }
}
