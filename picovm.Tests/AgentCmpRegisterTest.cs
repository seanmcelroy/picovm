using System;
using System.Collections.Generic;
using System.Numerics;
using picovm.Assembler;
using picovm.VM;
using Xunit;

namespace picovm.Tests
{
    /// <summary>
    /// Coverage for <see cref="Bytecode.CMP_REGISTER"/> (register-to-register CMP) across all
    /// four operand widths, on both the 32-bit <see cref="Agent"/> and the 64-bit
    /// <see cref="Agent64"/>.
    /// </summary>
    /// <remarks>
    /// This suite would have caught two bugs found and fixed this session:
    /// <list type="number">
    /// <item>Agent64 fully reimplements <c>Tick()</c> rather than falling back to
    /// <c>Agent.Tick()</c> for unhandled opcodes, and had no <c>CMP_REGISTER</c> case at all.
    /// Every register-to-register CMP silently returned <see cref="TickErrorCode.UnknownBytecode"/>
    /// with <c>Done = true</c> instead of executing or throwing, which a bare
    /// "run until done" loop would not notice.</item>
    /// <item>The 1-byte-register branch of <c>CMP_REGISTER</c> called <c>ReadRegister</c> (which
    /// only knows 16-bit and segment registers) instead of <c>ReadHalfRegister</c>, so e.g.
    /// "CMP AL, BL" threw <see cref="InvalidOperationException"/>.</item>
    /// </list>
    /// </remarks>
    public class AgentCmpRegisterTest
    {
        private static readonly Linux32Kernel kernel32 = new();
        private static readonly Linux64Kernel kernel64 = new();

        // ---- Independent oracle for expected SUB/CMP flags -------------------------------
        // Derived directly from x86 flag semantics, not from Agent.cs's own formulas, so this
        // is a genuine cross-check rather than a restatement of the code under test.
        private static (bool cf, bool zf, bool sf, bool of, bool pf) ExpectedFlags(ulong a, ulong b, int bits)
        {
            var mask = Mask(bits);
            var ua = a & mask;
            var ub = b & mask;
            var cf = ua < ub;
            var rawResult = unchecked(ua - ub) & mask;
            var zf = rawResult == 0;
            var signBit = 1UL << (bits - 1);
            var sf = (rawResult & signBit) != 0;
            var signA = (ua & signBit) != 0;
            var signB = (ub & signBit) != 0;
            var of = signA != signB && signA != sf;
            var pf = BitOperations.PopCount((byte)rawResult) % 2 == 0;
            return (cf, zf, sf, of, pf);
        }

        private static ulong Mask(int bits) => bits == 64 ? ulong.MaxValue : (1UL << bits) - 1;

        private static (Register r1, Register r2) OperandRegs(int bits) => bits switch
        {
            8 => (Register.CL, Register.DL),
            16 => (Register.CX, Register.DX),
            32 => (Register.ECX, Register.EDX),
            64 => (Register.RCX, Register.RDX),
            _ => throw new ArgumentOutOfRangeException(nameof(bits)),
        };

        private static ulong ReadOperand(Agent agent, Register reg, int bits) => bits switch
        {
            8 => agent.ReadHalfRegister(reg),
            16 => agent.ReadRegister(reg),
            32 => agent.ReadExtendedRegister(reg),
            64 => ((Agent64)agent).ReadR64Register(reg),
            _ => throw new ArgumentOutOfRangeException(nameof(bits)),
        };

        // Five scenarios chosen to exercise CF, ZF, SF, OF and PF in varied combinations,
        // including the signed-overflow boundary at the width's sign bit.
        private static IEnumerable<(string name, Func<int, (ulong v1, ulong v2)> values)> Scenarios()
        {
            yield return ("Equal", bits => (5UL, 5UL));
            yield return ("LessUnsigned_NoOverflow", bits => (3UL, 5UL));
            yield return ("GreaterUnsigned_NoOverflow", bits => (5UL, 3UL));
            yield return ("SignedOverflowAtSignBit", bits => (1UL << (bits - 1), 1UL));
            yield return ("AllBitsSetVsZero", bits => (Mask(bits), 0UL));
        }

        private static IEnumerable<object[]> Cases(int[] widths)
        {
            foreach (var bits in widths)
                foreach (var (name, values) in Scenarios())
                {
                    var (v1, v2) = values(bits);
                    yield return new object[] { bits, name, v1, v2 };
                }
        }

        public static IEnumerable<object[]> Agent32Cases() => Cases(new[] { 8, 16, 32 });
        public static IEnumerable<object[]> Agent64Cases() => Cases(new[] { 8, 16, 32, 64 });

        private static string[] BuildProgram(Register r1, Register r2, ulong v1, ulong v2) => new[]
        {
            "section .text",
            "global _start",
            "_start:",
            $"    MOV {r1}, 0x{v1:X}",
            $"    MOV {r2}, 0x{v2:X}",
            $"    CMP {r1}, {r2}",
            "    JE zf_yes",
            "    MOV EAX, 0",
            "    JMP zf_done",
            "zf_yes:",
            "    MOV EAX, 1",
            "zf_done:",
            "    JB cf_yes",
            "    MOV EBX, 0",
            "    JMP cf_done",
            "cf_yes:",
            "    MOV EBX, 1",
            "cf_done:",
            "    JS sf_yes",
            "    MOV ESI, 0",
            "    JMP sf_done",
            "sf_yes:",
            "    MOV ESI, 1",
            "sf_done:",
            "    JO of_yes",
            "    MOV EDI, 0",
            "    JMP of_done",
            "of_yes:",
            "    MOV EDI, 1",
            "of_done:",
            "    JP pf_yes",
            "    MOV EBP, 0",
            "    JMP pf_done",
            "pf_yes:",
            "    MOV EBP, 1",
            "pf_done:",
            "    END",
        };

        private static void RunToHalt(Agent agent)
        {
            TickResult ret;
            do { ret = agent.Tick(); } while (!ret.Done);
            Assert.Equal(TickErrorCode.Ok, ret.ErrorCode);
        }

        private static void AssertFlags(Agent agent, (bool cf, bool zf, bool sf, bool of, bool pf) expected)
        {
            Assert.Equal(expected.zf ? 1u : 0u, agent.ReadExtendedRegister(Register.EAX));
            Assert.Equal(expected.cf ? 1u : 0u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expected.sf ? 1u : 0u, agent.ReadExtendedRegister(Register.ESI));
            Assert.Equal(expected.of ? 1u : 0u, agent.ReadExtendedRegister(Register.EDI));
            Assert.Equal(expected.pf ? 1u : 0u, agent.ReadExtendedRegister(Register.EBP));
        }

        [Theory]
        [MemberData(nameof(Agent32Cases))]
        public void Agent32_Cmp_SetsFlagsAndPreservesOperands(int bits, string scenario, ulong v1, ulong v2)
        {
            _ = scenario; // theory display label only
            var (r1, r2) = OperandRegs(bits);
            var expected = ExpectedFlags(v1, v2, bits);

            var compiler = new BytecodeCompiler<UInt32>();
            var compiled = compiler.Compile(BuildProgram(r1, r2, v1, v2), "UNIT_TEST");
            Assert.Empty(compiled.Errors);

            var agent = new Agent(kernel32, [.. compiled.TextSegment.Value], 0);
            RunToHalt(agent);

            AssertFlags(agent, expected);
            // CMP must not mutate the compared registers -- only flags change.
            Assert.Equal(v1 & Mask(bits), ReadOperand(agent, r1, bits));
            Assert.Equal(v2 & Mask(bits), ReadOperand(agent, r2, bits));
        }

        [Theory]
        [MemberData(nameof(Agent64Cases))]
        public void Agent64_Cmp_SetsFlagsAndPreservesOperands(int bits, string scenario, ulong v1, ulong v2)
        {
            _ = scenario; // theory display label only
            var (r1, r2) = OperandRegs(bits);
            var expected = ExpectedFlags(v1, v2, bits);

            var compiler = new BytecodeCompiler<UInt64>();
            var compiled = compiler.Compile(BuildProgram(r1, r2, v1, v2), "UNIT_TEST");
            Assert.Empty(compiled.Errors);

            var agent = new Agent64(kernel64, [.. compiled.TextSegment.Value], 0);
            RunToHalt(agent);

            AssertFlags(agent, expected);
            Assert.Equal(v1 & Mask(bits), ReadOperand(agent, r1, bits));
            Assert.Equal(v2 & Mask(bits), ReadOperand(agent, r2, bits));
        }

        [Fact]
        public void Agent64_Cmp_RAX_RBX_ExecutesInsteadOfUnknownBytecode()
        {
            // Regression test for the primary bug: Agent64 had no CMP_REGISTER case, so this
            // silently returned TickErrorCode.UnknownBytecode instead of executing.
            var compiler = new BytecodeCompiler<UInt64>();
            var compiled = compiler.Compile(new[]
            {
                "section .text",
                "global _start",
                "_start:",
                "    MOV RAX, 5",
                "    MOV RBX, 3",
                "    CMP RAX, RBX",
                "    END",
            }, "UNIT_TEST");
            Assert.Empty(compiled.Errors);

            var agent = new Agent64(kernel64, [.. compiled.TextSegment.Value], 0);
            RunToHalt(agent);
        }

        [Fact]
        public void Agent32_Cmp_HighByteRegisters_AH_AL()
        {
            // Regression test for the secondary bug: the 1-byte branch called ReadRegister
            // (16-bit/segment only) instead of ReadHalfRegister, so this threw
            // InvalidOperationException: ERROR: Unknown register AL!
            var compiler = new BytecodeCompiler<UInt32>();
            var compiled = compiler.Compile(new[]
            {
                "section .text",
                "global _start",
                "_start:",
                "    MOV AH, 3",
                "    MOV AL, 2",
                "    CMP AH, AL",
                "    JA above",
                "    MOV EBX, 0",
                "    END",
                "above:",
                "    MOV EBX, 1",
                "    END",
            }, "UNIT_TEST");
            Assert.Empty(compiled.Errors);

            var agent = new Agent(kernel32, [.. compiled.TextSegment.Value], 0);
            RunToHalt(agent);

            Assert.Equal(1u, agent.ReadExtendedRegister(Register.EBX));
        }

        [Fact]
        public void Agent64_Cmp_HighByteRegisters_AH_AL()
        {
            var compiler = new BytecodeCompiler<UInt64>();
            var compiled = compiler.Compile(new[]
            {
                "section .text",
                "global _start",
                "_start:",
                "    MOV AH, 3",
                "    MOV AL, 2",
                "    CMP AH, AL",
                "    JA above",
                "    MOV EBX, 0",
                "    END",
                "above:",
                "    MOV EBX, 1",
                "    END",
            }, "UNIT_TEST");
            Assert.Empty(compiled.Errors);

            var agent = new Agent64(kernel64, [.. compiled.TextSegment.Value], 0);
            RunToHalt(agent);

            Assert.Equal(1u, agent.ReadExtendedRegister(Register.EBX));
        }

        [Fact]
        public void Agent32_Cmp_MismatchedRegisterSizes_Throws()
        {
            var compiler = new BytecodeCompiler<UInt32>();
            var compiled = compiler.Compile(new[]
            {
                "section .text",
                "global _start",
                "_start:",
                "    MOV EAX, 5",
                "    MOV BL, 3",
                "    CMP EAX, BL",
                "    END",
            }, "UNIT_TEST");
            Assert.Empty(compiled.Errors);

            var agent = new Agent(kernel32, [.. compiled.TextSegment.Value], 0);
            _ = agent.Tick(); // MOV EAX, 5
            _ = agent.Tick(); // MOV BL, 3
            Assert.Throws<InvalidOperationException>(() => agent.Tick()); // CMP EAX, BL
        }

        [Fact]
        public void Agent64_Cmp_MismatchedRegisterSizes_Throws()
        {
            var compiler = new BytecodeCompiler<UInt64>();
            var compiled = compiler.Compile(new[]
            {
                "section .text",
                "global _start",
                "_start:",
                "    MOV RAX, 5",
                "    MOV EBX, 3",
                "    CMP RAX, EBX",
                "    END",
            }, "UNIT_TEST");
            Assert.Empty(compiled.Errors);

            var agent = new Agent64(kernel64, [.. compiled.TextSegment.Value], 0);
            _ = agent.Tick(); // MOV RAX, 5
            _ = agent.Tick(); // MOV EBX, 3
            Assert.Throws<InvalidOperationException>(() => agent.Tick()); // CMP RAX, EBX
        }
    }
}
