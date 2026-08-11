using System;
using System.Collections.Generic;
using System.Numerics;
using picovm.Tests.Support;
using picovm.VM;
using Xunit;

namespace picovm.Tests
{
    /// <summary>
    /// Coverage for <c>TEST_IMMEDIATE</c> and <c>TEST_REGISTER</c> across every operand width,
    /// on both agents. Structurally a twin of <see cref="AgentAndTest"/>: same oracle, same
    /// flag-capture harness, same scenarios -- but every case additionally asserts that the
    /// destination register is <em>unchanged</em> after execution. That is the property that
    /// distinguishes TEST from AND, and the specific property a recent regression violated
    /// (the reg-reg assembler branch emitted <c>AND_IMMEDIATE</c> instead of
    /// <c>TEST_REGISTER</c>, which would have both mutated the destination and desynchronised
    /// the instruction stream).
    /// </summary>
    public class AgentTestInstructionTest
    {
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

        // Same scenarios as AgentAndTest -- kept in sync so the AND/TEST pair share coverage.
        private static IEnumerable<(string name, Func<int, (ulong v1, ulong v2)> values)> Scenarios()
        {
            yield return ("Zero", _ => (0xF0F0F0F0F0F0F0F0UL, 0x0F0F0F0F0F0F0F0FUL));
            yield return ("SignBitOnly", bits => (1UL << (bits - 1), Mask(bits)));
            yield return ("LowByteEvenParity", _ => (0x33UL, 0x0FUL));
            yield return ("LowByteOddParity", _ => (0x33UL, 0x01UL));
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
                $"    TEST {dest}, 0x{v2 & mask:X}",
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
                $"    TEST {dest}, {src}",
                .. FlagCaptureTail(),
            ];
        }

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
        public void Agent32_TestImmediate_PreservesDestAndSetsFlags(int bits, string scenario, ulong v1, ulong v2)
        {
            _ = scenario;
            var dest = DestReg(bits);
            var expected = ExpectedLogicFlags(v1, v2, bits);

            var agent = MovTestHarness.Run32(BuildImmediateProgram(bits, dest, v1, v2));

            // TEST must leave the destination equal to v1; the AND result is discarded.
            Assert.Equal(v1 & Mask(bits), ReadOperand(agent, dest, bits));
            AssertFlags(agent, expected);
        }

        [Theory]
        [MemberData(nameof(Agent64Cases))]
        public void Agent64_TestImmediate_PreservesDestAndSetsFlags(int bits, string scenario, ulong v1, ulong v2)
        {
            _ = scenario;
            var dest = DestReg(bits);
            var expected = ExpectedLogicFlags(v1, v2, bits);

            var agent = MovTestHarness.Run64(BuildImmediateProgram(bits, dest, v1, v2));

            Assert.Equal(v1 & Mask(bits), ReadOperand(agent, dest, bits));
            AssertFlags(agent, expected);
        }

        // ---- Register form ----------------------------------------------------------------

        [Theory]
        [MemberData(nameof(Agent32Cases))]
        public void Agent32_TestRegister_PreservesBothOperandsAndSetsFlags(int bits, string scenario, ulong v1, ulong v2)
        {
            _ = scenario;
            var dest = DestReg(bits);
            var src = SrcReg(bits);
            var expected = ExpectedLogicFlags(v1, v2, bits);

            var agent = MovTestHarness.Run32(BuildRegisterProgram(bits, dest, src, v1, v2));

            // Neither operand may be mutated by TEST.
            Assert.Equal(v1 & Mask(bits), ReadOperand(agent, dest, bits));
            Assert.Equal(v2 & Mask(bits), ReadOperand(agent, src, bits));
            AssertFlags(agent, expected);
        }

        [Theory]
        [MemberData(nameof(Agent64Cases))]
        public void Agent64_TestRegister_PreservesBothOperandsAndSetsFlags(int bits, string scenario, ulong v1, ulong v2)
        {
            _ = scenario;
            var dest = DestReg(bits);
            var src = SrcReg(bits);
            var expected = ExpectedLogicFlags(v1, v2, bits);

            var agent = MovTestHarness.Run64(BuildRegisterProgram(bits, dest, src, v1, v2));

            Assert.Equal(v1 & Mask(bits), ReadOperand(agent, dest, bits));
            Assert.Equal(v2 & Mask(bits), ReadOperand(agent, src, bits));
            AssertFlags(agent, expected);
        }

        // ---- Mismatched register widths must be rejected ---------------------------------
        // Assembler-time throw, not a returned error.

        [Fact]
        public void Agent32_TestRegister_MismatchedSizes_RejectedByAssembler()
        {
            Assert.Throws<Exception>(() => MovTestHarness.TryCompile32(Asm.Text(
                "MOV EAX, 5",
                "MOV BL, 3",
                "TEST EAX, BL")));
        }

        [Fact]
        public void Agent64_TestRegister_MismatchedSizes_RejectedByAssembler()
        {
            Assert.Throws<Exception>(() => MovTestHarness.TryCompile64(Asm.Text(
                "MOV RAX, 5",
                "MOV EBX, 3",
                "TEST RAX, EBX")));
        }

        // ---- TEST vs AND: the destination-mutation property ------------------------------
        // The exact bug the earlier reg-reg assembler branch had was emitting AND_IMMEDIATE
        // for `TEST reg, reg`. That would (a) mutate the destination and (b) read the second
        // register byte as the low byte of an immediate. Running the same operands through
        // both mnemonics and asserting AND writes back while TEST does not is the paired
        // regression that locks in this property.

        [Fact]
        public void Agent32_TestImmediate_VsAndImmediate_OnlyAndMutatesDest()
        {
            const uint v1 = 0xF0F0F0F0;
            const uint v2 = 0x0F0FF0F0;
            var expectedAnd = v1 & v2;

            var afterAnd = MovTestHarness.Run32(Asm.Text(
                $"MOV EDX, 0x{v1:X}",
                $"AND EDX, 0x{v2:X}"));
            var afterTest = MovTestHarness.Run32(Asm.Text(
                $"MOV EDX, 0x{v1:X}",
                $"TEST EDX, 0x{v2:X}"));

            Assert.Equal(expectedAnd, afterAnd.ReadExtendedRegister(Register.EDX));
            Assert.Equal(v1, afterTest.ReadExtendedRegister(Register.EDX));
        }

        [Fact]
        public void Agent64_TestRegister_VsAndRegister_OnlyAndMutatesDest()
        {
            const ulong v1 = 0xF0F0F0F0F0F0F0F0;
            const ulong v2 = 0x0F0FF0F00F0FF0F0;
            var expectedAnd = v1 & v2;

            var afterAnd = MovTestHarness.Run64(Asm.Text(
                $"MOV RDX, 0x{v1:X}",
                $"MOV RCX, 0x{v2:X}",
                "AND RDX, RCX"));
            var afterTest = MovTestHarness.Run64(Asm.Text(
                $"MOV RDX, 0x{v1:X}",
                $"MOV RCX, 0x{v2:X}",
                "TEST RDX, RCX"));

            Assert.Equal(expectedAnd, afterAnd.ReadR64Register(Register.RDX));
            Assert.Equal(v2, afterAnd.ReadR64Register(Register.RCX));
            Assert.Equal(v1, afterTest.ReadR64Register(Register.RDX));
            Assert.Equal(v2, afterTest.ReadR64Register(Register.RCX));
        }

        // The idiomatic "TEST EAX, EAX" zero-check: the reason `TEST reg, reg` matters.
        [Fact]
        public void Agent32_TestRegister_SameRegister_ActsAsZeroCheck()
        {
            var agentZero = MovTestHarness.Run32(Asm.Text(
                "MOV EAX, 0",
                "TEST EAX, EAX",
                "JZ was_zero",
                "MOV EBX, 0",
                "END",
                "was_zero:",
                "MOV EBX, 1",
                "END"));
            Assert.Equal(1u, agentZero.ReadExtendedRegister(Register.EBX));

            var agentNonZero = MovTestHarness.Run32(Asm.Text(
                "MOV EAX, 0x1234",
                "TEST EAX, EAX",
                "JZ was_zero",
                "MOV EBX, 0",
                "END",
                "was_zero:",
                "MOV EBX, 1",
                "END"));
            Assert.Equal(0u, agentNonZero.ReadExtendedRegister(Register.EBX));
            // TEST must not have zeroed EAX.
            Assert.Equal(0x1234u, agentNonZero.ReadExtendedRegister(Register.EAX));
        }

        [Fact]
        public void Agent64_TestRegister_SameRegister_ActsAsZeroCheck()
        {
            var agentZero = MovTestHarness.Run64(Asm.Text(
                "MOV RAX, 0",
                "TEST RAX, RAX",
                "JZ was_zero",
                "MOV EBX, 0",
                "END",
                "was_zero:",
                "MOV EBX, 1",
                "END"));
            Assert.Equal(1u, agentZero.ReadExtendedRegister(Register.EBX));

            var agentNonZero = MovTestHarness.Run64(Asm.Text(
                "MOV RAX, 0x1234567890ABCDEF",
                "TEST RAX, RAX",
                "JZ was_zero",
                "MOV EBX, 0",
                "END",
                "was_zero:",
                "MOV EBX, 1",
                "END"));
            Assert.Equal(0u, agentNonZero.ReadExtendedRegister(Register.EBX));
            Assert.Equal(0x1234567890ABCDEFul, agentNonZero.ReadR64Register(Register.RAX));
        }
    }
}
