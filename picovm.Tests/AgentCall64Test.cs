using System;
using picovm.Assembler;
using picovm.VM;
using Xunit;

namespace picovm.Tests
{
    /// <summary>
    /// 64-bit mirror of <see cref="AgentCall32Test"/>. Same cases, R-family registers,
    /// 8-byte return addresses.
    /// </summary>
    public class AgentCall64Test
    {
        private static readonly Linux64Kernel kernel = new();

        private static Agent64 Assemble(string[] program)
        {
            var compiler = new BytecodeCompiler<UInt64>();
            var compiled = compiler.Compile(program, "UNIT_TEST");
            Assert.Empty(compiled.Errors);
            Assert.NotEmpty(compiled.TextSegment);
            return new Agent64(kernel, [.. compiled.TextSegment.Value], 0);
        }

        private static void RunToHalt(Agent64 agent)
        {
            TickResult ret;
            do { ret = agent.Tick(); } while (!ret.Done);
            Assert.Equal(TickErrorCode.Ok, ret.ErrorCode);
        }

        [Fact]
        public void Call_Ret_RoundTrip_Executes_Callee_And_Returns()
        {
            var agent = Assemble(new[]
            {
                "section .text",
                "global _start",
                "_start:",
                "    MOV RAX, 111",
                "    CALL callee",
                "    MOV RCX, 333",
                "    END",
                "callee:",
                "    MOV RBX, 222",
                "    RET",
            });

            RunToHalt(agent);

            Assert.Equal(111ul, agent.ReadR64Register(Register.RAX));
            Assert.Equal(222ul, agent.ReadR64Register(Register.RBX));
            Assert.Equal(333ul, agent.ReadR64Register(Register.RCX));
        }

        [Fact]
        public void Call_Register_RoundTrip()
        {
            // See AgentCall32Test.Call_Register_RoundTrip for why the address is seeded
            // rather than loaded via MOV.
            var compiler = new BytecodeCompiler<UInt64>();
            var compiled = compiler.Compile(new[]
            {
                "section .text",
                "global _start",
                "_start:",
                "    MOV RAX, 111",
                "    CALL RDX",
                "    MOV RCX, 333",
                "    END",
                "callee:",
                "    MOV RBX, 222",
                "    RET",
            }, "UNIT_TEST");
            Assert.Empty(compiled.Errors);
            var calleeOffset = ((CompilationResult<UInt64>)compiled).TextLabelsOffsets!["callee"];

            var agent = new Agent64(kernel, [.. compiled.TextSegment!.Value], 0);
            // WriteExtendedRegister only knows E-family names, but RDX and EDX share the
            // same backing slot, so seeding EDX with a zero-extended uint lands correctly
            // in RDX for the CALL RDX to read. Safe here because addresses fit in 16 bits.
            agent.WriteExtendedRegister(Register.EDX, (uint)calleeOffset);

            RunToHalt(agent);

            Assert.Equal(111ul, agent.ReadR64Register(Register.RAX));
            Assert.Equal(222ul, agent.ReadR64Register(Register.RBX));
            Assert.Equal(333ul, agent.ReadR64Register(Register.RCX));
        }

        [Fact]
        public void Call_Register_Via_Mov_Label_RoundTrip()
        {
            // See AgentCall32Test.Call_Register_Via_Mov_Label_RoundTrip for the rationale.
            var agent = Assemble(new[]
            {
                "section .text",
                "global _start",
                "_start:",
                "    MOV RDX, callee",
                "    CALL RDX",
                "    END",
                "callee:",
                "    MOV RAX, 222",
                "    RET",
            });

            RunToHalt(agent);

            Assert.Equal(222ul, agent.ReadR64Register(Register.RAX));
        }

        [Fact]
        public void Call_Constant_RoundTrip()
        {
            string[] Program(string target) => new[]
            {
                "section .text",
                "global _start",
                "_start:",
                $"    CALL {target}",
                "    MOV RCX, 333",
                "    END",
                "callee:",
                "    MOV RBX, 222",
                "    RET",
            };

            var compiler = new BytecodeCompiler<UInt64>();
            var probe = compiler.Compile(Program("callee"), "PROBE");
            Assert.Empty(probe.Errors);
            var calleeOffset = ((CompilationResult<UInt64>)probe).TextLabelsOffsets!["callee"];

            var agent = Assemble(Program(calleeOffset.ToString()));

            RunToHalt(agent);

            Assert.Equal(222ul, agent.ReadR64Register(Register.RBX));
            Assert.Equal(333ul, agent.ReadR64Register(Register.RCX));
        }

        [Fact]
        public void Nested_Calls_Unwind_In_Order()
        {
            var agent = Assemble(new[]
            {
                "section .text",
                "global _start",
                "_start:",
                "    MOV RAX, 1",
                "    CALL b",
                "    MOV RCX, 4",
                "    END",
                "b:",
                "    MOV RDX, 2",
                "    CALL c",
                "    MOV RDI, 6",
                "    RET",
                "c:",
                "    MOV RSI, 3",
                "    RET",
            });

            RunToHalt(agent);

            Assert.Equal(1ul, agent.ReadR64Register(Register.RAX));
            Assert.Equal(2ul, agent.ReadR64Register(Register.RDX));
            Assert.Equal(3ul, agent.ReadR64Register(Register.RSI));
            Assert.Equal(4ul, agent.ReadR64Register(Register.RCX));
            Assert.Equal(6ul, agent.ReadR64Register(Register.RDI));
        }

        [Fact]
        public void Recursion_Terminates_And_Restores_Sp()
        {
            // See AgentCall32Test.Recursion_Terminates_And_Restores_Sp for the count-up rationale.
            var initialSp = new Agent64(kernel, new byte[] { (byte)Bytecode.END }, 0).StackPointer;

            var agent = Assemble(new[]
            {
                "section .text",
                "global _start",
                "_start:",
                "    MOV RCX, 0",
                "    CALL countup",
                "    END",
                "countup:",
                "    CMP RCX, 5",
                "    JE done",
                "    ADD RCX, 1",
                "    CALL countup",
                "done:",
                "    RET",
            });

            RunToHalt(agent);

            Assert.Equal(5ul, agent.ReadR64Register(Register.RCX));
            Assert.Equal(initialSp, agent.StackPointer);
        }

        [Fact]
        public void Sp_Is_Restored_After_Balanced_Call_Ret()
        {
            var initialSp = new Agent64(kernel, new byte[] { (byte)Bytecode.END }, 0).StackPointer;

            var agent = Assemble(new[]
            {
                "section .text",
                "global _start",
                "_start:",
                "    CALL noop",
                "    END",
                "noop:",
                "    RET",
            });

            RunToHalt(agent);

            Assert.Equal(initialSp, agent.StackPointer);
        }

        [Fact]
        public void Return_Address_Pushed_Equals_Fallthrough()
        {
            var compiler = new BytecodeCompiler<UInt64>();
            var compiled = compiler.Compile(new[]
            {
                "section .text",
                "global _start",
                "_start:",
                "    CALL callee",
                "after_call:",
                "    END",
                "callee:",
                "    POP RAX",
                "    END",
            }, "UNIT_TEST");
            Assert.Empty(compiled.Errors);

            var expected = ((CompilationResult<UInt64>)compiled).TextLabelsOffsets!["after_call"];

            var agent = new Agent64(kernel, [.. compiled.TextSegment!.Value], 0);
            RunToHalt(agent);

            Assert.Equal(expected, agent.ReadR64Register(Register.RAX));
        }

        [Fact]
        public void Callee_Push_Pop_Does_Not_Corrupt_Return()
        {
            var agent = Assemble(new[]
            {
                "section .text",
                "global _start",
                "_start:",
                "    MOV RBX, 999",
                "    CALL callee",
                "    MOV RCX, 333",
                "    END",
                "callee:",
                "    PUSH RBX",
                "    MOV RBX, 222",
                "    POP RBX",
                "    RET",
            });

            RunToHalt(agent);

            Assert.Equal(999ul, agent.ReadR64Register(Register.RBX));
            Assert.Equal(333ul, agent.ReadR64Register(Register.RCX));
        }

        [Fact]
        public void Call_Inside_Taken_Branch_Executes()
        {
            var agent = Assemble(new[]
            {
                "section .text",
                "global _start",
                "_start:",
                "    JZ do_call",
                "    MOV RAX, 111",
                "    END",
                "do_call:",
                "    CALL callee",
                "    END",
                "callee:",
                "    MOV RAX, 222",
                "    RET",
            });
            agent.WriteStatusRegister(Flag.ZERO_FLAG, true);

            RunToHalt(agent);

            Assert.Equal(222ul, agent.ReadR64Register(Register.RAX));
        }

        [Fact]
        public void Call_Inside_Not_Taken_Branch_Does_Not_Execute()
        {
            var agent = Assemble(new[]
            {
                "section .text",
                "global _start",
                "_start:",
                "    JZ do_call",
                "    MOV RAX, 111",
                "    END",
                "do_call:",
                "    CALL callee",
                "    END",
                "callee:",
                "    MOV RAX, 222",
                "    RET",
            });
            agent.WriteStatusRegister(Flag.ZERO_FLAG, false);

            RunToHalt(agent);

            Assert.Equal(111ul, agent.ReadR64Register(Register.RAX));
        }

        [Fact]
        public void Ret_On_Fresh_Stack_Faults()
        {
            var agent = new Agent64(kernel, new byte[] { (byte)Bytecode.RET }, 0);

            Assert.Throws<MemoryAccessViolationException>(() => agent.Tick());
        }

        [Fact]
        public void Call_At_Sp_Underflow_Boundary_Faults()
        {
            var agent = Assemble(new[]
            {
                "section .text",
                "global _start",
                "_start:",
                "    CALL callee",
                "    END",
                "callee:",
                "    RET",
            });
            agent.WriteExtendedRegister(Register.SP, 7u);

            var ex = Assert.Throws<MemoryAccessViolationException>(() => agent.Tick());
            Assert.True(ex.IsWrite);
            Assert.Equal(8, ex.Width);
        }
    }
}
