using System;
using System.Collections.Generic;
using picovm.Assembler;
using picovm.VM;
using Xunit;

namespace picovm.Tests
{
    /// <summary>
    /// Exercises every jump mnemonic on the 32-bit agent, both taken and not-taken.
    /// The not-taken case matters as much as the taken case: it is the one that catches a
    /// decode desync between the compiler's reserved operand width and the amount the VM
    /// advances the instruction pointer by when it doesn't branch.
    /// </summary>
    public class AgentJump32Test
    {
        private static readonly Linux32Kernel kernel = new();

        public static IEnumerable<object[]> ConditionalCases()
        {
            foreach (var (mnemonic, setup) in Cases())
                foreach (var taken in new[] { true, false })
                    yield return new object[] { mnemonic, taken, setup };
        }

        private static IEnumerable<(string mnemonic, Action<Agent, bool> setup)> Cases()
        {
            static void Zero(Agent a, bool v) => a.WriteStatusRegister(Flag.ZERO_FLAG, v);
            static void NotZero(Agent a, bool v) => a.WriteStatusRegister(Flag.ZERO_FLAG, !v);
            static void Overflow(Agent a, bool v) => a.WriteStatusRegister(Flag.OVERFLOW_FLAG, v);
            static void NotOverflow(Agent a, bool v) => a.WriteStatusRegister(Flag.OVERFLOW_FLAG, !v);
            static void Sign(Agent a, bool v) => a.WriteStatusRegister(Flag.SIGN_FLAG, v);
            static void NotSign(Agent a, bool v) => a.WriteStatusRegister(Flag.SIGN_FLAG, !v);
            static void Carry(Agent a, bool v) => a.WriteStatusRegister(Flag.CARRY_FLAG, v);
            static void NotCarry(Agent a, bool v) => a.WriteStatusRegister(Flag.CARRY_FLAG, !v);
            // CF=1 or ZF=1: hold ZF=0 and drive CF.
            static void BelowOrEqual(Agent a, bool v)
            {
                a.WriteStatusRegister(Flag.ZERO_FLAG, false);
                a.WriteStatusRegister(Flag.CARRY_FLAG, v);
            }
            // CF=0 and ZF=0: hold ZF=0 and drive CF.
            static void Above(Agent a, bool v)
            {
                a.WriteStatusRegister(Flag.ZERO_FLAG, false);
                a.WriteStatusRegister(Flag.CARRY_FLAG, !v);
            }
            // SF != OF: hold OF=0 and drive SF.
            static void Less(Agent a, bool v)
            {
                a.WriteStatusRegister(Flag.OVERFLOW_FLAG, false);
                a.WriteStatusRegister(Flag.SIGN_FLAG, v);
            }
            // SF == OF: hold OF=0 and drive SF (inverted vs. Less).
            static void GreaterOrEqual(Agent a, bool v)
            {
                a.WriteStatusRegister(Flag.OVERFLOW_FLAG, false);
                a.WriteStatusRegister(Flag.SIGN_FLAG, !v);
            }
            // ZF=1 or SF!=OF: hold SF=OF=0 and drive ZF.
            static void LessOrEqual(Agent a, bool v) => a.WriteStatusRegister(Flag.ZERO_FLAG, v);
            // ZF=0 and SF==OF: hold SF=OF=0 and drive ZF (inverted vs. LessOrEqual).
            static void Greater(Agent a, bool v) => a.WriteStatusRegister(Flag.ZERO_FLAG, !v);
            static void Parity(Agent a, bool v) => a.WriteStatusRegister(Flag.PARITY_FLAG, v);
            static void NotParity(Agent a, bool v) => a.WriteStatusRegister(Flag.PARITY_FLAG, !v);
            static void CxZero(Agent a, bool v) => a.WriteRegister(Register.CX, (ushort)(v ? 0 : 5));
            static void EcxZero(Agent a, bool v) => a.WriteExtendedRegister(Register.ECX, v ? 0u : 5u);

            yield return ("JZ", Zero);
            yield return ("JE", Zero);
            yield return ("JNZ", NotZero);
            yield return ("JNE", NotZero);
            yield return ("JO", Overflow);
            yield return ("JNO", NotOverflow);
            yield return ("JS", Sign);
            yield return ("JNS", NotSign);
            yield return ("JB", Carry);
            yield return ("JNAE", Carry);
            yield return ("JC", Carry);
            yield return ("JNB", NotCarry);
            yield return ("JAE", NotCarry);
            yield return ("JNC", NotCarry);
            yield return ("JBE", BelowOrEqual);
            yield return ("JNA", BelowOrEqual);
            yield return ("JA", Above);
            yield return ("JNBE", Above);
            yield return ("JL", Less);
            yield return ("JNGE", Less);
            yield return ("JGE", GreaterOrEqual);
            yield return ("JNL", GreaterOrEqual);
            yield return ("JLE", LessOrEqual);
            yield return ("JNG", LessOrEqual);
            yield return ("JG", Greater);
            yield return ("JNLE", Greater);
            yield return ("JP", Parity);
            yield return ("JPE", Parity);
            yield return ("JNP", NotParity);
            yield return ("JPO", NotParity);
            yield return ("JCXZ", CxZero);
            yield return ("JECXZ", EcxZero);
        }

        // Taken skips straight to EAX=222; not-taken falls through to EAX=111 and halts
        // before ever reaching the target, so the two outcomes can't be confused.
        private static string[] ProgramText(string mnemonic) => new[]
        {
            "section .data",
            "    dat db 0",
            "section .text",
            "global _start",
            "_start:",
            "    MOV EAX, dat",
            $"    {mnemonic} target",
            "    MOV EAX, 111",
            "    END",
            "target:",
            "    MOV EAX, 222",
            "    END",
        };

        [Theory]
        [MemberData(nameof(ConditionalCases))]
        public void Branches_As_Expected(string mnemonic, bool taken, Action<Agent, bool> setup)
        {
            var compiler = new BytecodeCompiler<UInt32>();
            var compiled = compiler.Compile(ProgramText(mnemonic), "UNIT_TEST");
            Assert.Empty(compiled.Errors);

            var agent = new Agent(kernel, compiled.TextSegment, 0);
            setup(agent, taken);

            TickResult ret;
            do { ret = agent.Tick(); } while (!ret.Done);

            Assert.Equal(TickErrorCode.Ok, ret.ErrorCode);
            Assert.Equal((uint)(taken ? 222 : 111), agent.ReadExtendedRegister(Register.EAX));
        }

        [Fact]
        public void JMP_Is_Always_Taken()
        {
            var compiler = new BytecodeCompiler<UInt32>();
            var compiled = compiler.Compile(ProgramText("JMP"), "UNIT_TEST");
            Assert.Empty(compiled.Errors);

            var agent = new Agent(kernel, compiled.TextSegment, 0);

            TickResult ret;
            do { ret = agent.Tick(); } while (!ret.Done);

            Assert.Equal(TickErrorCode.Ok, ret.ErrorCode);
            Assert.Equal(222u, agent.ReadExtendedRegister(Register.EAX));
        }
    }
}
