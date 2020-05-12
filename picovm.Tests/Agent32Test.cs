using picovm.Compiler;
using picovm.VM;
using Xunit;

namespace picovm.Tests
{
    public class Agent32Test
    {
        private static Linux32Kernel kernel = new Linux32Kernel();

        [Fact]
        public void GetOperand_Constant()
        {
            Xunit.Assert.Equal(BytecodeCompiler.ParameterType.Constant, BytecodeCompiler.GetOperandType("4294945365"));
            Xunit.Assert.Equal(BytecodeCompiler.ParameterType.Constant, BytecodeCompiler.GetOperandType("2863315917"));
        }

        [Fact]
        public void MOV_REG_CON_Simple()
        {
            var programText = new string[] {
                "section	.text",
                "global _start",
                "_start:",
                "   MOV EAX, 4294967295 ; copy the value 11111111111111111111111111111111 into eax",
                "   MOV AX, 0           ; copy the value 0000000000000000 into ax",
                "   MOV AH, 170         ; copy the value 10101010 (0xAA) into ah",
                "   MOV AL, 85          ; copy the value 01010101 (0x55) into al",
                "   MOV EBX, 5          ; copy the value 5 into ebx",
                "   MOV EAX, EBX        ; copy the value in ebx into eax",
                "   PUSH 4              ; push 4 on the stack",
                "   PUSH EAX            ; push eax (5) on the stack",
                "   PUSH 6              ; push 6 on the stack",
                "   POP EBX             ; pop stack (6) into ebx",
                "   POP EBX             ; pop stack (5) into ebx",
                "   POP [EBX]           ; pop stack (4) into [ebx] memory location = 5",
                "   ADD [EBX], 10       ; add 10 to the value in [ebx] which would change 4 to 14",
                "   PUSH [EBX]          ; push [ebx] memory location=5 value=14 onto the stack",
                "END"
            };

            var compiler = new BytecodeCompiler();
            var compiled = compiler.Compile("UNIT_TEST", programText);

            var agent = new Agent(kernel, compiled.textSegment);
            var ret = agent.Tick();
            Xunit.Assert.Null(ret);
            Xunit.Assert.Equal(4294967295, agent.ReadExtendedRegister(Register.EAX));

            ret = agent.Tick();
            Xunit.Assert.Equal(0U, agent.ReadRegister(Register.AX));

            ret = agent.Tick();
            Xunit.Assert.Equal(170, agent.ReadHalfRegister(Register.AH));

            ret = agent.Tick();
            Xunit.Assert.Equal(85, agent.ReadHalfRegister(Register.AL));

            ret = agent.Tick();
            Xunit.Assert.Equal(5U, agent.ReadExtendedRegister(Register.EBX));

            ret = agent.Tick();
            Xunit.Assert.Equal(5U, agent.ReadExtendedRegister(Register.EAX));
            Xunit.Assert.Equal(5U, agent.ReadExtendedRegister(Register.EBX));

            ret = agent.Tick();
            ret = agent.Tick();
            ret = agent.Tick();

            ret = agent.Tick();
            Xunit.Assert.Equal(6U, agent.ReadExtendedRegister(Register.EBX));

            ret = agent.Tick();
            Xunit.Assert.Equal(5U, agent.ReadExtendedRegister(Register.EBX));

            ret = agent.Tick();
            Xunit.Assert.Equal(5U, agent.ReadExtendedRegister(Register.EBX));

            ret = agent.Tick();
            ret = agent.Tick();
            ret = agent.Tick();
            Xunit.Assert.NotNull(ret);
            Xunit.Assert.Equal(0, ret); // Program should have terminated on the second tick
        }

        [Fact]
        public void MOV_REG_CON_Overlayed()
        {
            var programText = new string[] {
                "section	.text",
                "global _start",
                "_start:",
                "MOV EAX, 4294967295", // copy the value 11111111111111111111111111111111 into eax
                "MOV AX, 0", // copy the value 0000000000000000 into ax
                "MOV AH, 170", // copy the value 10101010 (0xAA) into ah
                "MOV AL, 85", // copy the value 01010101 (0x55) into al
                "MOV EAX, 0", // copy the value 11111111111111111111111111111111 into eax
                "END"
            };

            var compiler = new BytecodeCompiler();
            var compiled = compiler.Compile("UNIT_TEST", programText);

            var agent = new Agent(kernel, compiled.textSegment);
            var ret = agent.Tick();
            Xunit.Assert.Null(ret);
            Xunit.Assert.Equal((uint)0xFFFFFFFF, agent.ReadExtendedRegister(Register.EAX));
            Xunit.Assert.Equal((ushort)0xFFFF, agent.ReadRegister(Register.AX));
            Xunit.Assert.Equal((byte)0xFF, agent.ReadHalfRegister(Register.AH));
            Xunit.Assert.Equal((byte)0xFF, agent.ReadHalfRegister(Register.AL));

            ret = agent.Tick();
            Xunit.Assert.Null(ret);
            Xunit.Assert.Equal((uint)0xFFFF0000, agent.ReadExtendedRegister(Register.EAX));
            Xunit.Assert.Equal((uint)0, agent.ReadRegister(Register.AX));
            Xunit.Assert.Equal((uint)0, agent.ReadHalfRegister(Register.AH));
            Xunit.Assert.Equal((uint)0, agent.ReadHalfRegister(Register.AL));

            ret = agent.Tick();
            Xunit.Assert.Null(ret);
            Xunit.Assert.Equal((uint)0xFFFFAA00, agent.ReadExtendedRegister(Register.EAX));
            Xunit.Assert.Equal((uint)170, agent.ReadHalfRegister(Register.AH));
            Xunit.Assert.Equal((uint)0, agent.ReadHalfRegister(Register.AL));

            ret = agent.Tick();
            Xunit.Assert.Null(ret);
            Xunit.Assert.Equal((uint)0xFFFFAA55, agent.ReadExtendedRegister(Register.EAX));
            Xunit.Assert.Equal((uint)170, agent.ReadHalfRegister(Register.AH));
            Xunit.Assert.Equal((uint)85, agent.ReadHalfRegister(Register.AL));

            ret = agent.Tick();
            Xunit.Assert.Null(ret);
            Xunit.Assert.Equal((uint)0, agent.ReadExtendedRegister(Register.EAX));

            ret = agent.Tick();
            Xunit.Assert.NotNull(ret);
            Xunit.Assert.Equal(0, ret); // Program should have terminated on the second tick
        }

        [Fact]
        public void MOV_Bonanza32()
        {
            var programText = new string[] {
                "section	.text",
                "global _start",
                "_start:",
                "mov  eax, 0x11112222 ; eax = 0x11112222",
                "mov  ax, 0x3333      ; eax = 0x11113333 (works, only low 16 bits changed)",
                "mov  al, 0x44        ; eax = 0x11113344 (works, only low 8 bits changed)",
                "mov  ah, 0x55        ; eax = 0x11115544 (works, only high 8 bits changed)",
                "xor  ah, ah          ; eax = 0x11110044 (works, only high 8 bits cleared)",
                "mov  eax, 0x11112222 ; eax = 0x11112222",
                "xor  al, al          ; eax = 0x11112200 (works, only low 8 bits cleared)",
                "mov  eax, 0x11112222 ; eax = 0x11112222",
                "xor  ax, ax          ; eax = 0x11110000 (works, only low 16 bits cleared)"
            };

            var compiler = new BytecodeCompiler();
            var compiled = compiler.Compile("UNIT_TEST", programText);

            var agent = new Agent(kernel, compiled.textSegment);
            var ret = agent.Tick();
            Xunit.Assert.Null(ret);
            Xunit.Assert.Equal((uint)0x11112222, agent.ReadExtendedRegister(Register.EAX));

            ret = agent.Tick();
            Xunit.Assert.Null(ret);
            Xunit.Assert.Equal((uint)0x11113333, agent.ReadExtendedRegister(Register.EAX));

            ret = agent.Tick();
            Xunit.Assert.Null(ret);
            Xunit.Assert.Equal((uint)0x11113344, agent.ReadExtendedRegister(Register.EAX));

            ret = agent.Tick();
            Xunit.Assert.Null(ret);
            Xunit.Assert.Equal((uint)0x11115544, agent.ReadExtendedRegister(Register.EAX));

            ret = agent.Tick();
            Xunit.Assert.Null(ret);
            Xunit.Assert.Equal((uint)0x11110044, agent.ReadExtendedRegister(Register.EAX));

            ret = agent.Tick();
            Xunit.Assert.Null(ret);
            Xunit.Assert.Equal((uint)0x11112222, agent.ReadExtendedRegister(Register.EAX));

            ret = agent.Tick();
            Xunit.Assert.Null(ret);
            Xunit.Assert.Equal((uint)0x11112200, agent.ReadExtendedRegister(Register.EAX));

            ret = agent.Tick();
            Xunit.Assert.Null(ret);
            Xunit.Assert.Equal((uint)0x11112222, agent.ReadExtendedRegister(Register.EAX)); // Program should have terminated on the second tick

            ret = agent.Tick();
            Xunit.Assert.Null(ret);
            Xunit.Assert.Equal((uint)0x11110000, agent.ReadExtendedRegister(Register.EAX)); // Program should have terminated on the second tick
        }

        [Fact]
        public void PUSH_POP_Overlayed()
        {
            var programText = new string[] {
                "section	.text",
                "global _start",
                "_start:",
                "PUSH 4294945365", // push the value 1111 1111 1111 1111 1010 1010 0101 0101‬ (FFFF AA55‬) onto the stack
                "POP EAX", // pop it back into eax
                "PUSH 2863315917", // push the value 1010 1010 1010 1010 1011 1011 1100 1101 (AAAA BBCD‬) onto the stack
                "POP EAX", // pop it back into eax
                "END"
            };

            var compiler = new BytecodeCompiler();
            var compiled = compiler.Compile("UNIT_TEST", programText);

            var agent = new Agent(kernel, compiled.textSegment);
            var ret = agent.Tick();

            // PUSH 4294945365
            Xunit.Assert.Null(ret);
            Xunit.Assert.Equal((uint)(65535 - 4), agent.StackPointer);
            Xunit.Assert.Equal(4294945365, agent.StackPeek32());
            Xunit.Assert.Equal((uint)0, agent.ReadExtendedRegister(Register.EAX));

            // POP EAX #1 
            ret = agent.Tick();
            Xunit.Assert.Null(ret);
            Xunit.Assert.Equal((uint)(65535), agent.StackPointer);
            Xunit.Assert.Equal((uint)4294945365, agent.ReadExtendedRegister(Register.EAX));

            // PUSH 2863315917
            ret = agent.Tick();
            Xunit.Assert.Null(ret);
            Xunit.Assert.Equal((uint)(65535 - 4), agent.StackPointer);
            Xunit.Assert.Equal(2863315917, agent.StackPeek32());
            Xunit.Assert.Equal((uint)4294945365, agent.ReadExtendedRegister(Register.EAX));

            // POP EAX #2
            ret = agent.Tick();
            Xunit.Assert.Null(ret);
            Xunit.Assert.Equal((uint)(65535), agent.StackPointer);
            Xunit.Assert.Equal((uint)2863315917, agent.ReadExtendedRegister(Register.EAX));

            // END
            ret = agent.Tick();
            Xunit.Assert.NotNull(ret);
            Xunit.Assert.Equal(0, ret); // Program should have terminated on the second tick
        }
    }
}
