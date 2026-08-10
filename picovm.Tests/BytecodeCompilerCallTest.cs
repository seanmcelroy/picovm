using System;
using picovm.Assembler;
using Xunit;

namespace picovm.Tests
{
    /// <summary>
    /// Compile-time rejection and encoding tests for CALL and RET.
    /// Runtime behavior lives in <see cref="AgentCall32Test"/> / <see cref="AgentCall64Test"/>.
    /// </summary>
    public class BytecodeCompilerCallTest
    {
        private static string[] Program(string callLine) => new[]
        {
            "section .text",
            "global _start",
            "_start:",
            $"    {callLine}",
            "    END",
            "callee:",
            "    RET",
        };

        [Fact]
        public void Ret_Is_Bare_Opcode_On_32Bit()
        {
            var compiler = new BytecodeCompiler<UInt32>();
            var compiled = compiler.Compile(new[]
            {
                "section .text",
                "global _start",
                "_start:",
                "    RET",
            }, "UNIT_TEST");

            Assert.Empty(compiled.Errors);
            var text = compiled.TextSegment!.Value;
            Assert.Single(text);
            Assert.Equal((byte)Bytecode.RET, text[0]);
        }

        [Fact]
        public void Ret_Is_Bare_Opcode_On_64Bit()
        {
            var compiler = new BytecodeCompiler<UInt64>();
            var compiled = compiler.Compile(new[]
            {
                "section .text",
                "global _start",
                "_start:",
                "    RET",
            }, "UNIT_TEST");

            Assert.Empty(compiled.Errors);
            var text = compiled.TextSegment!.Value;
            Assert.Single(text);
            Assert.Equal((byte)Bytecode.RET, text[0]);
        }

        [Fact]
        public void Call_Immediate_Uses_4Byte_Operand_On_32Bit()
        {
            var compiler = new BytecodeCompiler<UInt32>();
            var compiled = compiler.Compile(Program("CALL callee"), "UNIT_TEST");

            Assert.Empty(compiled.Errors);
            var text = compiled.TextSegment!.Value;
            Assert.Equal((byte)Bytecode.CALL_IMMEDIATE, text[0]);

            var target = BitConverter.ToUInt32(text.AsSpan(1, 4));
            var calleeOffset = ((CompilationResult<UInt32>)compiled).TextLabelsOffsets!["callee"];
            Assert.Equal(calleeOffset, target);
        }

        [Fact]
        public void Call_Immediate_Uses_8Byte_Operand_On_64Bit()
        {
            var compiler = new BytecodeCompiler<UInt64>();
            var compiled = compiler.Compile(Program("CALL callee"), "UNIT_TEST");

            Assert.Empty(compiled.Errors);
            var text = compiled.TextSegment!.Value;
            Assert.Equal((byte)Bytecode.CALL_IMMEDIATE, text[0]);

            var target = BitConverter.ToUInt64(text.AsSpan(1, 8));
            var calleeOffset = ((CompilationResult<UInt64>)compiled).TextLabelsOffsets!["callee"];
            Assert.Equal(calleeOffset, target);
        }

        [Fact]
        public void Call_Constant_Emits_Literal_Little_Endian_On_32Bit()
        {
            var compiler = new BytecodeCompiler<UInt32>();
            var compiled = compiler.Compile(Program("CALL 0x12345678"), "UNIT_TEST");

            Assert.Empty(compiled.Errors);
            var text = compiled.TextSegment!.Value;
            Assert.Equal((byte)Bytecode.CALL_IMMEDIATE, text[0]);
            Assert.Equal(0x12345678u, BitConverter.ToUInt32(text.AsSpan(1, 4)));
        }

        [Fact]
        public void Call_Constant_Emits_Literal_Little_Endian_On_64Bit()
        {
            var compiler = new BytecodeCompiler<UInt64>();
            var compiled = compiler.Compile(Program("CALL 0x1122334455667788"), "UNIT_TEST");

            Assert.Empty(compiled.Errors);
            var text = compiled.TextSegment!.Value;
            Assert.Equal((byte)Bytecode.CALL_IMMEDIATE, text[0]);
            Assert.Equal(0x1122334455667788ul, BitConverter.ToUInt64(text.AsSpan(1, 8)));
        }

        [Fact]
        public void Call_Register_Emits_Two_Bytes()
        {
            var compiler = new BytecodeCompiler<UInt32>();
            var compiled = compiler.Compile(Program("CALL EAX"), "UNIT_TEST");

            Assert.Empty(compiled.Errors);
            var text = compiled.TextSegment!.Value;
            Assert.Equal((byte)Bytecode.CALL_REGISTER, text[0]);
            Assert.Equal((byte)Register.EAX, text[1]);
        }

        [Fact]
        public void Call_Register_Emits_Two_Bytes_On_64Bit()
        {
            var compiler = new BytecodeCompiler<UInt64>();
            var compiled = compiler.Compile(Program("CALL RAX"), "UNIT_TEST");

            Assert.Empty(compiled.Errors);
            var text = compiled.TextSegment!.Value;
            Assert.Equal((byte)Bytecode.CALL_REGISTER, text[0]);
            Assert.Equal((byte)Register.RAX, text[1]);
        }

        [Theory]
        [InlineData("RAX")]
        [InlineData("R8")]
        [InlineData("R15")]
        public void Call_RFamily_Rejected_On_32Bit(string reg)
        {
            var compiler = new BytecodeCompiler<UInt32>();
            Assert.Throws<InvalidOperationException>(() => compiler.Compile(Program($"CALL {reg}"), "UNIT_TEST"));
        }

        [Theory]
        [InlineData("EAX")]
        [InlineData("EBX")]
        [InlineData("ECX")]
        [InlineData("EDX")]
        public void Call_EFamily_Rejected_On_64Bit(string reg)
        {
            var compiler = new BytecodeCompiler<UInt64>();
            Assert.Throws<InvalidOperationException>(() => compiler.Compile(Program($"CALL {reg}"), "UNIT_TEST"));
        }

        [Theory]
        [InlineData("RSP")]
        [InlineData("RBP")]
        [InlineData("RSI")]
        [InlineData("RDI")]
        [InlineData("RIP")]
        public void Call_Forbidden_RRegisters_Rejected_On_64Bit(string reg)
        {
            var compiler = new BytecodeCompiler<UInt64>();
            Assert.Throws<InvalidOperationException>(() => compiler.Compile(Program($"CALL {reg}"), "UNIT_TEST"));
        }

        [Theory]
        [InlineData("ESP")]
        [InlineData("EBP")]
        [InlineData("ESI")]
        [InlineData("EDI")]
        [InlineData("EIP")]
        public void Call_Forbidden_ERegisters_Rejected_On_32Bit(string reg)
        {
            var compiler = new BytecodeCompiler<UInt32>();
            Assert.Throws<InvalidOperationException>(() => compiler.Compile(Program($"CALL {reg}"), "UNIT_TEST"));
        }

        [Theory]
        [InlineData("AX")]
        [InlineData("AL")]
        [InlineData("AH")]
        [InlineData("CS")]
        public void Call_SubWord_And_Segment_Registers_Rejected(string reg)
        {
            var compiler = new BytecodeCompiler<UInt32>();
            Assert.Throws<InvalidOperationException>(() => compiler.Compile(Program($"CALL {reg}"), "UNIT_TEST"));
        }
    }
}
