using System;
using Xunit;
using agent_playground;

namespace agent_playground.Tests
{
    public class AgentTest
    {
        [Fact]
        public void GetOperand_Constant()
        {
            Xunit.Assert.Equal(Compiler.ParameterType.Constant, Compiler.GetOperandType("4294945365"));
            Xunit.Assert.Equal(Compiler.ParameterType.Constant, Compiler.GetOperandType("2863315917"));
        }

        [Fact]
        public void MOV_REG_CON_Simple()
        {
            var programText = new string[] {
                "MOV EAX, 4294967295", // copy the value 11111111111111111111111111111111 into eax
                "END"
            };

            var compiler = new Compiler();
            var bytecode = compiler.Compile(programText);

            var agent = new Agent(bytecode);
            var ret = agent.Tick();
            Xunit.Assert.Null(ret);
            Xunit.Assert.Equal(4294967295, agent.ReadExtendedRegister(Register.EAX));
            ret = agent.Tick();
            Xunit.Assert.NotNull(ret);
            Xunit.Assert.Equal(0, ret); // Program should have terminated on the second tick
        }

        [Fact]
        public void MOV_REG_CON_Overlayed()
        {
            var programText = new string[] {
                "MOV EAX, 4294967295", // copy the value 11111111111111111111111111111111 into eax
                "MOV AX, 0", // copy the value 0000000000000000 into ax
                "MOV AH, 170", // copy the value 10101010 (0xAA) into ah
                "MOV AL, 85", // copy the value 01010101 (0x55) into al
                "MOV EAX, 0", // copy the value 11111111111111111111111111111111 into eax
                "END"
            };

            var compiler = new Compiler();
            var bytecode = compiler.Compile(programText);

            var agent = new Agent(bytecode);
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
        public void PUSH_POP_Overlayed()
        {
            var programText = new string[] {
                "PUSH 4294945365", // push the value 1111 1111 1111 1111 1010 1010 0101 0101‬ (FFFF AA55‬) onto the stack
                "POP EAX", // pop it back into eax
                "PUSH 2863315917", // push the value 1010 1010 1010 1010 1011 1011 1100 1101 (AAAA BBCD‬) onto the stack
                "POP EAX", // pop it back into eax
                "END"
            };

            var compiler = new Compiler();
            var bytecode = compiler.Compile(programText);

            var agent = new Agent(bytecode);
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
