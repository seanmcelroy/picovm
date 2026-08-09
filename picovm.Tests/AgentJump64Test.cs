using System;
using System.Collections.Generic;
using picovm.Assembler;
using picovm.VM;
using Xunit;

namespace picovm.Tests
{
    /// <summary>
    /// 64-bit mirror of <see cref="AgentJump32Test"/>. This is the suite that would have
    /// caught the compiler/VM operand-width mismatch found this session: the compiler
    /// reserves an 8-byte jump target for <see cref="BytecodeCompiler{UInt64}"/>, so the VM
    /// must read/skip 8 bytes, not 4, on every not-taken branch.
    /// </summary>
    public class AgentJump64Test
    {
        private static readonly Linux64Kernel kernel = new();

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
            static void BelowOrEqual(Agent a, bool v)
            {
                a.WriteStatusRegister(Flag.ZERO_FLAG, false);
                a.WriteStatusRegister(Flag.CARRY_FLAG, v);
            }
            static void Above(Agent a, bool v)
            {
                a.WriteStatusRegister(Flag.ZERO_FLAG, false);
                a.WriteStatusRegister(Flag.CARRY_FLAG, !v);
            }
            static void Less(Agent a, bool v)
            {
                a.WriteStatusRegister(Flag.OVERFLOW_FLAG, false);
                a.WriteStatusRegister(Flag.SIGN_FLAG, v);
            }
            static void GreaterOrEqual(Agent a, bool v)
            {
                a.WriteStatusRegister(Flag.OVERFLOW_FLAG, false);
                a.WriteStatusRegister(Flag.SIGN_FLAG, !v);
            }
            static void LessOrEqual(Agent a, bool v) => a.WriteStatusRegister(Flag.ZERO_FLAG, v);
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

        private static string[] ProgramText(string mnemonic) => new[]
        {
            "section .data",
            "    dat db 0",
            "section .text",
            "global _start",
            "_start:",
            "    MOV RAX, dat",
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
            var compiler = new BytecodeCompiler<UInt64>();
            var compiled = compiler.Compile(ProgramText(mnemonic), "UNIT_TEST");
            Assert.Empty(compiled.Errors);

            var agent = new Agent64(kernel, compiled.TextSegment, 0);
            setup(agent, taken);

            TickResult ret;
            do { ret = agent.Tick(); } while (!ret.Done);

            Assert.Equal(TickErrorCode.Ok, ret.ErrorCode);
            Assert.Equal((uint)(taken ? 222 : 111), agent.ReadExtendedRegister(Register.EAX));
        }

        [Fact]
        public void JMP_Is_Always_Taken()
        {
            var compiler = new BytecodeCompiler<UInt64>();
            var compiled = compiler.Compile(ProgramText("JMP"), "UNIT_TEST");
            Assert.Empty(compiled.Errors);

            var agent = new Agent64(kernel, compiled.TextSegment, 0);

            TickResult ret;
            do { ret = agent.Tick(); } while (!ret.Done);

            Assert.Equal(TickErrorCode.Ok, ret.ErrorCode);
            Assert.Equal(222u, agent.ReadExtendedRegister(Register.EAX));
        }
    }
}
