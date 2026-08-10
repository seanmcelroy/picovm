using System;
using picovm.Assembler;
using picovm.VM;
using Xunit;

namespace picovm.Tests
{
    /// <summary>
    /// End-to-end tests for CALL_IMMEDIATE, CALL_REGISTER, and RET on the 32-bit agent.
    /// Programs are compiled through the assembler so the encoding contract is exercised
    /// alongside the VM dispatch.
    /// </summary>
    public class AgentCall32Test
    {
        private static readonly Linux32Kernel kernel = new();

        private static Agent Assemble(string[] program)
        {
            var compiler = new BytecodeCompiler<UInt32>();
            var compiled = compiler.Compile(program, "UNIT_TEST");
            Assert.Empty(compiled.Errors);
            Assert.NotEmpty(compiled.TextSegment);
            return new Agent(kernel, [.. compiled.TextSegment.Value], 0);
        }

        private static void RunToHalt(Agent agent)
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
                "    MOV EAX, 111",
                "    CALL callee",
                "    MOV ECX, 333",
                "    END",
                "callee:",
                "    MOV EBX, 222",
                "    RET",
            });

            RunToHalt(agent);

            Assert.Equal(111u, agent.ReadExtendedRegister(Register.EAX));
            Assert.Equal(222u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(333u, agent.ReadExtendedRegister(Register.ECX));
        }

        [Fact]
        public void Call_Register_RoundTrip()
        {
            // MOV can't currently load a text-label address into a register (it emits
            // the wrong placeholder for the label resolver), so we look up the callee's
            // offset at compile time and seed EDX directly. That keeps the test focused
            // on CALL_REGISTER's dispatch rather than an unrelated MOV limitation.
            var compiler = new BytecodeCompiler<UInt32>();
            var compiled = compiler.Compile(new[]
            {
                "section .text",
                "global _start",
                "_start:",
                "    MOV EAX, 111",
                "    CALL EDX",
                "    MOV ECX, 333",
                "    END",
                "callee:",
                "    MOV EBX, 222",
                "    RET",
            }, "UNIT_TEST");
            Assert.Empty(compiled.Errors);
            var calleeOffset = ((CompilationResult<UInt32>)compiled).TextLabelsOffsets!["callee"];

            var agent = new Agent(kernel, [.. compiled.TextSegment!.Value], 0);
            agent.WriteExtendedRegister(Register.EDX, calleeOffset);

            RunToHalt(agent);

            Assert.Equal(111u, agent.ReadExtendedRegister(Register.EAX));
            Assert.Equal(222u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(333u, agent.ReadExtendedRegister(Register.ECX));
        }

        [Fact]
        public void Call_Register_Via_Mov_Label_RoundTrip()
        {
            // MOV must be able to load a text-label address into a register, using a
            // lowercase source-side label (labels are historically case-sensitive but
            // MOV normalizes to uppercase; the resolver must bridge the two).
            var agent = Assemble(new[]
            {
                "section .text",
                "global _start",
                "_start:",
                "    MOV EDX, callee",
                "    CALL EDX",
                "    END",
                "callee:",
                "    MOV EAX, 222",
                "    RET",
            });

            RunToHalt(agent);

            Assert.Equal(222u, agent.ReadExtendedRegister(Register.EAX));
        }

        [Fact]
        public void Call_Constant_RoundTrip()
        {
            // Two-pass: compile once with a label to discover the callee's offset, then
            // re-compile the *same layout* with that offset spelled as a numeric literal.
            // Identical layouts are load-bearing here — any instruction size difference
            // between probe and real shifts `callee` and the literal jumps into the
            // middle of some other instruction.
            string[] Program(string target) => new[]
            {
                "section .text",
                "global _start",
                "_start:",
                $"    CALL {target}",
                "    MOV ECX, 333",
                "    END",
                "callee:",
                "    MOV EBX, 222",
                "    RET",
            };

            var compiler = new BytecodeCompiler<UInt32>();
            var probe = compiler.Compile(Program("callee"), "PROBE");
            Assert.Empty(probe.Errors);
            var calleeOffset = ((CompilationResult<UInt32>)probe).TextLabelsOffsets!["callee"];

            var agent = Assemble(Program(calleeOffset.ToString()));

            RunToHalt(agent);

            Assert.Equal(222u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(333u, agent.ReadExtendedRegister(Register.ECX));
        }

        [Fact]
        public void Nested_Calls_Unwind_In_Order()
        {
            // A calls B, B calls C. Each site sets a distinct marker before and after
            // its call, so a swapped return address would leave one of them unset.
            var agent = Assemble(new[]
            {
                "section .text",
                "global _start",
                "_start:",
                "    MOV EAX, 1",
                "    CALL b",
                "    MOV ECX, 4",
                "    END",
                "b:",
                "    MOV EDX, 2",
                "    CALL c",
                "    MOV EDI, 6",
                "    RET",
                "c:",
                "    MOV ESI, 3",
                "    RET",
            });

            RunToHalt(agent);

            Assert.Equal(1u, agent.ReadExtendedRegister(Register.EAX));
            Assert.Equal(2u, agent.ReadExtendedRegister(Register.EDX));
            Assert.Equal(3u, agent.ReadExtendedRegister(Register.ESI));
            Assert.Equal(4u, agent.ReadExtendedRegister(Register.ECX));
            Assert.Equal(6u, agent.ReadExtendedRegister(Register.EDI));
        }

        [Fact]
        public void Recursion_Terminates_And_Restores_Sp()
        {
            // Count *up* to 5. The assembler doesn't parse negative constants and has
            // no SUB/DEC, so counting up with `ADD ECX, 1` is the cleanest way to drive
            // a recursion. A balanced CALL/RET pair leaves SP exactly where it started.
            var initialSp = new Agent(kernel, new byte[] { (byte)Bytecode.END }, 0).StackPointer;

            var agent = Assemble(new[]
            {
                "section .text",
                "global _start",
                "_start:",
                "    MOV ECX, 0",
                "    CALL countup",
                "    END",
                "countup:",
                "    CMP ECX, 5",
                "    JE done",
                "    ADD ECX, 1",
                "    CALL countup",
                "done:",
                "    RET",
            });

            RunToHalt(agent);

            Assert.Equal(5u, agent.ReadExtendedRegister(Register.ECX));
            Assert.Equal(initialSp, agent.StackPointer);
        }

        [Fact]
        public void Sp_Is_Restored_After_Balanced_Call_Ret()
        {
            var initialSp = new Agent(kernel, new byte[] { (byte)Bytecode.END }, 0).StackPointer;

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
            // The callee never RETs; it POPs the return address into EAX and halts.
            // The expected value is the offset of the `after_call` label, which is the
            // byte immediately following the CALL_IMMEDIATE operand.
            var compiler = new BytecodeCompiler<UInt32>();
            var compiled = compiler.Compile(new[]
            {
                "section .text",
                "global _start",
                "_start:",
                "    CALL callee",
                "after_call:",
                "    END",
                "callee:",
                "    POP EAX",
                "    END",
            }, "UNIT_TEST");
            Assert.Empty(compiled.Errors);

            var expected = ((CompilationResult<UInt32>)compiled).TextLabelsOffsets!["after_call"];

            var agent = new Agent(kernel, [.. compiled.TextSegment!.Value], 0);
            RunToHalt(agent);

            Assert.Equal(expected, agent.ReadExtendedRegister(Register.EAX));
        }

        [Fact]
        public void Callee_Push_Pop_Does_Not_Corrupt_Return()
        {
            // Callee saves and restores EBX around its own work. If push/pop widths
            // ever fell out of sync with the return-address push, RET would jump to
            // a bogus address and the fall-through markers wouldn't get set.
            var agent = Assemble(new[]
            {
                "section .text",
                "global _start",
                "_start:",
                "    MOV EBX, 999",
                "    CALL callee",
                "    MOV ECX, 333",
                "    END",
                "callee:",
                "    PUSH EBX",
                "    MOV EBX, 222",
                "    POP EBX",
                "    RET",
            });

            RunToHalt(agent);

            Assert.Equal(999u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(333u, agent.ReadExtendedRegister(Register.ECX));
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
                "    MOV EAX, 111",
                "    END",
                "do_call:",
                "    CALL callee",
                "    END",
                "callee:",
                "    MOV EAX, 222",
                "    RET",
            });
            agent.WriteStatusRegister(Flag.ZERO_FLAG, true);

            RunToHalt(agent);

            Assert.Equal(222u, agent.ReadExtendedRegister(Register.EAX));
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
                "    MOV EAX, 111",
                "    END",
                "do_call:",
                "    CALL callee",
                "    END",
                "callee:",
                "    MOV EAX, 222",
                "    RET",
            });
            agent.WriteStatusRegister(Flag.ZERO_FLAG, false);

            RunToHalt(agent);

            Assert.Equal(111u, agent.ReadExtendedRegister(Register.EAX));
        }

        [Fact]
        public void Ret_On_Fresh_Stack_Faults()
        {
            // With SP at the top of memory, StackPop32 reads past the end of the
            // address space and the read-side bounds check fires.
            var agent = new Agent(kernel, new byte[] { (byte)Bytecode.RET }, 0);

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
            agent.WriteExtendedRegister(Register.SP, 3u);

            var ex = Assert.Throws<MemoryAccessViolationException>(() => agent.Tick());
            Assert.True(ex.IsWrite);
            Assert.Equal(4, ex.Width);
        }
    }
}
