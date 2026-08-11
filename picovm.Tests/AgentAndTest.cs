using System;
using System.Collections.Generic;
using System.Numerics;
using picovm.Tests.Support;
using picovm.VM;
using Xunit;

namespace picovm.Tests
{
    /// <summary>
    /// Coverage for <c>AND_IMMEDIATE</c> and <c>AND_REGISTER</c> across every operand width,
    /// on both agents. Each case captures ZF/SF/PF/CF/OF into distinct extended registers via
    /// conditional jumps, so a wrong flag <em>or</em> a decode desynchronisation (a stray IP
    /// increment corrupting whatever runs after AND) shows up as a specific sentinel mismatch
    /// rather than a vague failure.
    /// </summary>
    /// <remarks>
    /// The destination register (DL/DX/EDX/RDX) is chosen so it aliases only into RDX; the
    /// source register for the reg-reg form (CL/CX/ECX/RCX) aliases only into RCX; the
    /// flag-capture registers (EAX/EBX/ESI/EDI/EBP) live in RAX/RBX/RSI/RDI/RBP and never
    /// overlap either operand path.
    /// </remarks>
    public class AgentAndTest
    {
        // ---- Independent oracle -----------------------------------------------------------
        // Derived from x86 semantics (AND clears CF/OF/AF, sets PF/ZF/SF from the result),
        // not from the VM's own formulas -- so a bug in Agent.cs cannot also alibi itself in
        // the oracle. CF and OF are always false for AND, so we bake that into AssertFlags
        // rather than the oracle return.
        private static (bool zf, bool sf, bool pf) ExpectedLogicFlags(ulong a, ulong b, int bits)
        {
            var mask = Mask(bits);
            var r = (a & b) & mask;
            var signBit = 1UL << (bits - 1);
            return (r == 0, (r & signBit) != 0, BitOperations.PopCount((byte)r) % 2 == 0);
        }

        private static ulong Mask(int bits) => bits == 64 ? ulong.MaxValue : (1UL << bits) - 1;

        private static Register DestReg(int bits) => bits switch
        {
            8 => Register.DL,
            16 => Register.DX,
            32 => Register.EDX,
            64 => Register.RDX,
            _ => throw new ArgumentOutOfRangeException(nameof(bits)),
        };

        private static Register SrcReg(int bits) => bits switch
        {
            8 => Register.CL,
            16 => Register.CX,
            32 => Register.ECX,
            64 => Register.RCX,
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

        // ---- Scenarios --------------------------------------------------------------------
        // Chosen to exercise every combination WriteLogicFlags can produce, plus the width
        // boundaries most likely to expose sign-bit-mask and low-byte-parity bugs.
        private static IEnumerable<(string name, Func<int, (ulong v1, ulong v2)> values)> Scenarios()
        {
            // Result = 0: ZF=1, SF=0, PF=1 (popcount 0 is even).
            yield return ("Zero", _ => (0xF0F0F0F0F0F0F0F0UL, 0x0F0F0F0F0F0F0F0FUL));
            // Result = only the sign bit: SF=1, ZF=0. PF varies by width (low byte is 0x80
            // at 8 bits, 0 at wider widths) -- the oracle handles that automatically.
            yield return ("SignBitOnly", bits => (1UL << (bits - 1), Mask(bits)));
            // Result low byte = 0x03 (popcount 2, even): PF=1, ZF=0, SF=0.
            yield return ("LowByteEvenParity", _ => (0x33UL, 0x0FUL));
            // Result low byte = 0x01 (popcount 1, odd): PF=0, ZF=0, SF=0.
            yield return ("LowByteOddParity", _ => (0x33UL, 0x01UL));
            // Result = all bits set: SF=1, PF=1 (0xFF popcount 8 is even), ZF=0.
            yield return ("AllOnes", bits => (Mask(bits), Mask(bits)));
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

        // ---- Program builders -------------------------------------------------------------
        // Both forms end with the same flag-capture tail. Labels are locally unique within
        // each compiled program, so it's safe to reuse them across cases.
        private static string[] FlagCaptureTail() =>
        [
            "    JZ zf1", "    MOV EAX, 0", "    JMP zfd",
            "zf1:", "    MOV EAX, 1",
            "zfd:",
            "    JS sf1", "    MOV EBX, 0", "    JMP sfd",
            "sf1:", "    MOV EBX, 1",
            "sfd:",
            "    JP pf1", "    MOV ESI, 0", "    JMP pfd",
            "pf1:", "    MOV ESI, 1",
            "pfd:",
            "    JC cf1", "    MOV EDI, 0", "    JMP cfd",
            "cf1:", "    MOV EDI, 1",
            "cfd:",
            "    JO of1", "    MOV EBP, 0", "    JMP ofd",
            "of1:", "    MOV EBP, 1",
            "ofd:",
            "    END",
        ];

        private static string[] BuildImmediateProgram(int bits, Register dest, ulong v1, ulong v2)
        {
            var mask = Mask(bits);
            return
            [
                "section .text",
                "global _start",
                "_start:",
                $"    MOV {dest}, 0x{v1 & mask:X}",
                $"    AND {dest}, 0x{v2 & mask:X}",
                .. FlagCaptureTail(),
            ];
        }

        private static string[] BuildRegisterProgram(int bits, Register dest, Register src, ulong v1, ulong v2)
        {
            var mask = Mask(bits);
            return
            [
                "section .text",
                "global _start",
                "_start:",
                $"    MOV {dest}, 0x{v1 & mask:X}",
                $"    MOV {src}, 0x{v2 & mask:X}",
                $"    AND {dest}, {src}",
                .. FlagCaptureTail(),
            ];
        }

        // AND always clears CF and OF (WriteLogicFlags wipes the ALU flags mask), so those
        // trackers must always read zero -- worth asserting explicitly, since a regression
        // that OR-ed instead of assigned the flag mask would only show up here.
        private static void AssertFlags(Agent agent, (bool zf, bool sf, bool pf) expected)
        {
            Assert.Equal(expected.zf ? 1u : 0u, agent.ReadExtendedRegister(Register.EAX));
            Assert.Equal(expected.sf ? 1u : 0u, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(expected.pf ? 1u : 0u, agent.ReadExtendedRegister(Register.ESI));
            Assert.Equal(0u, agent.ReadExtendedRegister(Register.EDI));
            Assert.Equal(0u, agent.ReadExtendedRegister(Register.EBP));
        }

        // ---- Immediate form ---------------------------------------------------------------

        [Theory]
        [MemberData(nameof(Agent32Cases))]
        public void Agent32_AndImmediate_MutatesDestAndSetsFlags(int bits, string scenario, ulong v1, ulong v2)
        {
            _ = scenario; // display label only
            var dest = DestReg(bits);
            var expected = ExpectedLogicFlags(v1, v2, bits);
            var expectedResult = (v1 & v2) & Mask(bits);

            var agent = MovTestHarness.Run32(BuildImmediateProgram(bits, dest, v1, v2));

            Assert.Equal(expectedResult, ReadOperand(agent, dest, bits));
            AssertFlags(agent, expected);
        }

        [Theory]
        [MemberData(nameof(Agent64Cases))]
        public void Agent64_AndImmediate_MutatesDestAndSetsFlags(int bits, string scenario, ulong v1, ulong v2)
        {
            _ = scenario;
            var dest = DestReg(bits);
            var expected = ExpectedLogicFlags(v1, v2, bits);
            var expectedResult = (v1 & v2) & Mask(bits);

            var agent = MovTestHarness.Run64(BuildImmediateProgram(bits, dest, v1, v2));

            Assert.Equal(expectedResult, ReadOperand(agent, dest, bits));
            AssertFlags(agent, expected);
        }

        // ---- Register form ----------------------------------------------------------------

        [Theory]
        [MemberData(nameof(Agent32Cases))]
        public void Agent32_AndRegister_MutatesDestPreservesSrcAndSetsFlags(int bits, string scenario, ulong v1, ulong v2)
        {
            _ = scenario;
            var dest = DestReg(bits);
            var src = SrcReg(bits);
            var expected = ExpectedLogicFlags(v1, v2, bits);
            var expectedResult = (v1 & v2) & Mask(bits);

            var agent = MovTestHarness.Run32(BuildRegisterProgram(bits, dest, src, v1, v2));

            Assert.Equal(expectedResult, ReadOperand(agent, dest, bits));
            // AND writes back to dest only; src must be untouched.
            Assert.Equal(v2 & Mask(bits), ReadOperand(agent, src, bits));
            AssertFlags(agent, expected);
        }

        [Theory]
        [MemberData(nameof(Agent64Cases))]
        public void Agent64_AndRegister_MutatesDestPreservesSrcAndSetsFlags(int bits, string scenario, ulong v1, ulong v2)
        {
            _ = scenario;
            var dest = DestReg(bits);
            var src = SrcReg(bits);
            var expected = ExpectedLogicFlags(v1, v2, bits);
            var expectedResult = (v1 & v2) & Mask(bits);

            var agent = MovTestHarness.Run64(BuildRegisterProgram(bits, dest, src, v1, v2));

            Assert.Equal(expectedResult, ReadOperand(agent, dest, bits));
            Assert.Equal(v2 & Mask(bits), ReadOperand(agent, src, bits));
            AssertFlags(agent, expected);
        }

        // ---- Mismatched register widths must be rejected ---------------------------------
        // The assembler blocks this at compile time -- it throws rather than returning an
        // ICompilationResult with errors, so we assert the throw directly.

        [Fact]
        public void Agent32_AndRegister_MismatchedSizes_RejectedByAssembler()
        {
            Assert.Throws<Exception>(() => MovTestHarness.TryCompile32(Asm.Text(
                "MOV EAX, 5",
                "MOV BL, 3",
                "AND EAX, BL")));
        }

        [Fact]
        public void Agent64_AndRegister_MismatchedSizes_RejectedByAssembler()
        {
            Assert.Throws<Exception>(() => MovTestHarness.TryCompile64(Asm.Text(
                "MOV RAX, 5",
                "MOV EBX, 3",
                "AND RAX, EBX")));
        }
    }
}
