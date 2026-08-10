using System;
using picovm.Tests.Support;
using picovm.VM;
using Xunit;

namespace picovm.Tests
{
    /// <summary>
    /// Coverage for <c>MOV_REGISTER</c> (register-to-register).
    /// </summary>
    /// <remarks>
    /// Dispatch is a nested <c>switch (src.Size())</c> / <c>switch (dst.Size())</c>, so the
    /// interesting axis is the width pairing: which combinations widen, which are rejected, and
    /// what happens to the bits of the destination the move does not cover.  Every test seeds
    /// the destination with a dirty value first, so "preserved the other bytes" is a real
    /// assertion rather than an artefact of the register starting at zero.
    /// </remarks>
    public class MovRegisterTests
    {
        private const uint DstSeed32 = 0x11112222;
        private const uint SrcSeed32 = 0x33334455;   // BL=0x55, BH=0x44, BX=0x4455

        private const ulong DstSeed64 = 0x1111222233334444;
        private const ulong SrcSeed64 = 0x5555666677778899;   // BL=0x99, BH=0x88, BX=0x8899, EBX=0x77778899

        private static Agent Run32(string instruction) =>
            MovTestHarness.Run32(Asm.Text(
                $"MOV EAX, 0x{DstSeed32:X8}",
                $"MOV EBX, 0x{SrcSeed32:X8}",
                instruction));

        private static Agent64 Run64(string instruction) =>
            MovTestHarness.Run64(Asm.Text(
                $"MOV RAX, 0x{DstSeed64:X16}",
                $"MOV RBX, 0x{SrcSeed64:X16}",
                instruction));

        #region 32-bit width matrix

        /// <summary>
        /// Every legal source/destination width pairing in 32-bit mode, asserted against the
        /// full destination register so that untouched bytes are checked too.
        /// </summary>
        [Theory]
        // 1 -> 1: only the addressed byte changes
        [InlineData("MOV AL, BL", 0x11112255)]
        [InlineData("MOV AH, BL", 0x11115522)]
        [InlineData("MOV AL, BH", 0x11112244)]
        [InlineData("MOV AH, BH", 0x11114422)]
        // 1 -> 2: the word is replaced wholesale, so the source byte is zero-extended into it
        [InlineData("MOV AX, BL", 0x11110055)]
        // 1 -> 4: the whole register is replaced
        [InlineData("MOV EAX, BL", 0x00000055)]
        // 2 -> 2: high word preserved
        [InlineData("MOV AX, BX", 0x11114455)]
        // 2 -> 4: zero-extended into the full register
        [InlineData("MOV EAX, BX", 0x00004455)]
        // 4 -> 4
        [InlineData("MOV EAX, EBX", 0x33334455)]
        public void Widths32(string instruction, uint expected) =>
            Assert.Equal(expected, Run32(instruction).ReadExtendedRegister(Register.EAX));

        /// <summary>
        /// Narrowing moves are rejected outright rather than truncating.
        /// </summary>
        [Theory]
        [InlineData("MOV AX, EBX", "dst is a word but source is a dword")]
        [InlineData("MOV AL, EBX", "dst is a byte but source is a dword")]
        [InlineData("MOV AL, BX", "dst is a byte but source is a word")]
        public void Narrowing32_Throws(string instruction, string expectedMessage)
        {
            var ex = Assert.Throws<InvalidOperationException>(() => Run32(instruction));
            Assert.Contains(expectedMessage, ex.Message);
        }

        #endregion

        #region 64-bit width matrix

        [Theory]
        // 1 -> n
        [InlineData("MOV AL, BL", 0x1111222233334499UL)]
        [InlineData("MOV AH, BL", 0x1111222233339944UL)]
        [InlineData("MOV AX, BL", 0x1111222233330099UL)]
        [InlineData("MOV EAX, BL", 0x0000000000000099UL)]
        [InlineData("MOV RAX, BL", 0x0000000000000099UL)]
        // 2 -> n
        [InlineData("MOV AX, BX", 0x1111222233338899UL)]
        [InlineData("MOV EAX, BX", 0x0000000000008899UL)]
        [InlineData("MOV RAX, BX", 0x0000000000008899UL)]
        // 4 -> n
        [InlineData("MOV EAX, EBX", 0x0000000077778899UL)]
        [InlineData("MOV RAX, EBX", 0x0000000077778899UL)]
        // 8 -> 8
        [InlineData("MOV RAX, RBX", 0x5555666677778899UL)]
        public void Widths64(string instruction, ulong expected) =>
            Assert.Equal(expected, Run64(instruction).ReadR64Register(Register.RAX));

        [Theory]
        [InlineData("MOV EAX, RBX", "dst is a dword but source is a qword")]
        [InlineData("MOV AX, RBX", "dst is a word but source is a qword")]
        [InlineData("MOV AL, RBX", "dst is a byte but source is a qword")]
        [InlineData("MOV AX, EBX", "dst is a word but source is a dword")]
        [InlineData("MOV AL, EBX", "dst is a byte but source is a dword")]
        [InlineData("MOV AL, BX", "dst is a byte but source is a word")]
        public void Narrowing64_Throws(string instruction, string expectedMessage)
        {
            var ex = Assert.Throws<InvalidOperationException>(() => Run64(instruction));
            Assert.Contains(expectedMessage, ex.Message);
        }

        /// <summary>
        /// Writing a 32-bit register clears the upper half of its 64-bit container, matching
        /// x86-64.  Writing a 16- or 8-bit register does not.  The contrast is the point: it is
        /// easy to implement one and accidentally get the other.
        /// </summary>
        [Theory]
        [InlineData("EAX", "EBX", Register.RAX)]
        [InlineData("ECX", "EBX", Register.RCX)]
        [InlineData("EDX", "EBX", Register.RDX)]
        public void DwordWrite_ZeroExtendsToFullRegister(string dst, string src, Register full)
        {
            var agent = MovTestHarness.Run64(Asm.Text(
                $"MOV {full}, 0xFFFFFFFFFFFFFFFF",
                $"MOV {src}, 0x77778899",
                $"MOV {dst}, {src}"));

            Assert.Equal(0x0000000077778899UL, agent.ReadR64Register(full));
        }

        [Fact]
        public void WordWrite_DoesNotZeroExtend()
        {
            var agent = MovTestHarness.Run64(Asm.Text(
                "MOV RAX, 0xFFFFFFFFFFFFFFFF",
                "MOV RBX, 0x0000000000008899",
                "MOV AX, BX"));

            Assert.Equal(0xFFFFFFFFFFFF8899UL, agent.ReadR64Register(Register.RAX));
        }

        [Fact]
        public void ByteWrite_DoesNotZeroExtend()
        {
            var agent = MovTestHarness.Run64(Asm.Text(
                "MOV RAX, 0xFFFFFFFFFFFFFFFF",
                "MOV RBX, 0x0000000000000099",
                "MOV AL, BL"));

            Assert.Equal(0xFFFFFFFFFFFFFF99UL, agent.ReadR64Register(Register.RAX));
        }

        #endregion

        #region Register families

        /// <summary>
        /// The register accessors are per-register <c>switch</c> arms, so a transposed arm in
        /// any one family would be invisible to tests that only ever use the accumulator.
        /// </summary>
        [Theory]
        [InlineData("EAX", Register.EAX)]
        [InlineData("EBX", Register.EBX)]
        [InlineData("ECX", Register.ECX)]
        [InlineData("EDX", Register.EDX)]
        [InlineData("ESI", Register.ESI)]
        [InlineData("EDI", Register.EDI)]
        [InlineData("EBP", Register.EBP)]
        public void ExtendedFamilies32_RoundTrip(string name, Register register)
        {
            var agent = MovTestHarness.Run32(Asm.Text(
                "MOV EBX, 0xCAFEB00B",
                $"MOV {name}, EBX"));

            Assert.Equal(0xCAFEB00BU, agent.ReadExtendedRegister(register));
        }

        [Theory]
        [InlineData("AX", Register.AX)]
        [InlineData("BX", Register.BX)]
        [InlineData("CX", Register.CX)]
        [InlineData("DX", Register.DX)]
        [InlineData("SI", Register.SI)]
        [InlineData("DI", Register.DI)]
        [InlineData("BP", Register.BP)]
        public void WordFamilies32_RoundTrip(string name, Register register)
        {
            var agent = MovTestHarness.Run32(Asm.Text(
                "MOV CX, 0xBEEF",
                $"MOV {name}, CX"));

            Assert.Equal((ushort)0xBEEF, agent.ReadRegister(register));
        }

        [Theory]
        [InlineData("AH", Register.AH)]
        [InlineData("AL", Register.AL)]
        [InlineData("BH", Register.BH)]
        [InlineData("BL", Register.BL)]
        [InlineData("CH", Register.CH)]
        [InlineData("CL", Register.CL)]
        [InlineData("DH", Register.DH)]
        [InlineData("DL", Register.DL)]
        public void HalfFamilies32_RoundTrip(string name, Register register)
        {
            var agent = MovTestHarness.Run32(Asm.Text(
                "MOV BL, 0x5A",
                $"MOV {name}, BL"));

            Assert.Equal((byte)0x5A, agent.ReadHalfRegister(register));
        }

        [Theory]
        [InlineData("RAX", Register.RAX)]
        [InlineData("RBX", Register.RBX)]
        [InlineData("RCX", Register.RCX)]
        [InlineData("RDX", Register.RDX)]
        [InlineData("RSI", Register.RSI)]
        [InlineData("RDI", Register.RDI)]
        [InlineData("RBP", Register.RBP)]
        public void QwordFamilies64_RoundTrip(string name, Register register)
        {
            var agent = MovTestHarness.Run64(Asm.Text(
                "MOV RCX, 0xDEADBEEFCAFEB00B",
                $"MOV {name}, RCX"));

            Assert.Equal(0xDEADBEEFCAFEB00BUL, agent.ReadR64Register(register));
        }

        /// <summary>
        /// The extended registers introduced by x86-64.  These have no coverage elsewhere in
        /// the suite despite being backed by their own slots in the register file.
        /// </summary>
        [Theory]
        [InlineData("R8", Register.R8)]
        [InlineData("R9", Register.R9)]
        [InlineData("R10", Register.R10)]
        [InlineData("R11", Register.R11)]
        [InlineData("R12", Register.R12)]
        [InlineData("R13", Register.R13)]
        [InlineData("R14", Register.R14)]
        [InlineData("R15", Register.R15)]
        public void ExtendedRegisters64_RoundTrip(string name, Register register)
        {
            var agent = MovTestHarness.Run64(Asm.Text(
                "MOV RCX, 0x0123456789ABCDEF",
                $"MOV {name}, RCX"));

            Assert.Equal(0x0123456789ABCDEFUL, agent.ReadR64Register(register));
        }

        [Fact]
        public void ExtendedRegisters64_AreIndependentSlots()
        {
            var agent = MovTestHarness.Run64(Asm.Text(
                "MOV RCX, 0x1111111111111111",
                "MOV R8, RCX",
                "MOV RCX, 0x2222222222222222",
                "MOV R9, RCX"));

            Assert.Equal(0x1111111111111111UL, agent.ReadR64Register(Register.R8));
            Assert.Equal(0x2222222222222222UL, agent.ReadR64Register(Register.R9));
        }

        #endregion

        #region Index and pointer registers at dword width

        /// <summary>
        /// The three registers occupy distinct slots in the register file, so writing one must
        /// not disturb the others.  A transposed slot index would be invisible to a test that
        /// only ever writes one of them.
        /// </summary>
        [Fact]
        public void IndexRegisters32_AreIndependentSlots()
        {
            var agent = MovTestHarness.Run32(Asm.Text(
                "MOV EBX, 0x11111111",
                "MOV ESI, EBX",
                "MOV EBX, 0x22222222",
                "MOV EDI, EBX",
                "MOV EBX, 0x33333333",
                "MOV EBP, EBX"));

            Assert.Equal(0x11111111U, agent.ReadExtendedRegister(Register.ESI));
            Assert.Equal(0x22222222U, agent.ReadExtendedRegister(Register.EDI));
            Assert.Equal(0x33333333U, agent.ReadExtendedRegister(Register.EBP));

            // ...and none of them collided with the general registers or the stack pointer.
            Assert.Equal(0x33333333U, agent.ReadExtendedRegister(Register.EBX));
            Assert.Equal(65535U, agent.StackPointer);
        }

        /// <summary>
        /// ESI and SI are two views of one slot, and the 16- and 32-bit accessors reach it
        /// through separate <c>switch</c> arms.  Writing the wide view then the narrow one, and
        /// reading each back, pins that they agree about which bits they own.
        /// </summary>
        [Theory]
        [InlineData("ESI", "SI", Register.ESI, Register.SI)]
        [InlineData("EDI", "DI", Register.EDI, Register.DI)]
        [InlineData("EBP", "BP", Register.EBP, Register.BP)]
        public void IndexRegisters32_WideAndNarrowViewsShareOneSlot(
            string wide, string narrow, Register wideRegister, Register narrowRegister)
        {
            var agent = MovTestHarness.Run32(Asm.Text(
                "MOV EBX, 0xCAFEB00B",
                $"MOV {wide}, EBX",           // whole slot
                $"MOV EAX, {wide}",           // read the wide view back
                $"MOV CX, {narrow}"));        // read the narrow view of the same slot

            Assert.Equal(0xCAFEB00BU, agent.ReadExtendedRegister(wideRegister));
            Assert.Equal(0xCAFEB00BU, agent.ReadExtendedRegister(Register.EAX));
            Assert.Equal((ushort)0xB00B, agent.ReadRegister(narrowRegister));
            Assert.Equal((ushort)0xB00B, agent.ReadRegister(Register.CX));
        }

        /// <summary>
        /// The converse: a 16-bit write into the shared slot must leave the upper half of the
        /// 32-bit view intact.
        /// </summary>
        [Theory]
        [InlineData("ESI", "SI", Register.ESI)]
        [InlineData("EDI", "DI", Register.EDI)]
        [InlineData("EBP", "BP", Register.EBP)]
        public void IndexRegisters32_NarrowWritePreservesUpperHalf(string wide, string narrow, Register wideRegister)
        {
            var agent = MovTestHarness.Run32(Asm.Text(
                "MOV EBX, 0xCAFEB00B",
                $"MOV {wide}, EBX",
                "MOV CX, 0x1234",
                $"MOV {narrow}, CX"));

            Assert.Equal(0xCAFE1234U, agent.ReadExtendedRegister(wideRegister));
        }

        /// <summary>
        /// In 64-bit mode a dword write to one of these clears the upper half of the 64-bit
        /// register, the same rule the accumulator family follows.
        /// </summary>
        [Theory]
        [InlineData("ESI", "RSI", Register.RSI)]
        [InlineData("EDI", "RDI", Register.RDI)]
        [InlineData("EBP", "RBP", Register.RBP)]
        public void DwordWriteToIndexRegisters64_ZeroExtends(string dword, string qword, Register register)
        {
            var agent = MovTestHarness.Run64(Asm.Text(
                "MOV RBX, 0xFFFFFFFFFFFFFFFF",
                $"MOV {qword}, RBX",           // fill the whole register
                "MOV EBX, 0xCAFEB00B",
                $"MOV {dword}, EBX"));         // narrow write must clear the top half

            Assert.Equal(0x00000000CAFEB00BUL, agent.ReadR64Register(register));
        }

        /// <summary>
        /// The <c>int</c> overload of <c>WriteExtendedRegister</c> gained the same three
        /// registers.  MOV never reaches it -- the kernel uses it to report syscall results --
        /// so it is exercised directly here.
        /// </summary>
        [Theory]
        [InlineData(Register.ESI)]
        [InlineData(Register.EDI)]
        [InlineData(Register.EBP)]
        public void SignedOverloadSupportsIndexRegisters(Register register)
        {
            var registers = new ulong[19];

            Agent.WriteExtendedRegister(registers, register, 1234);

            Assert.Equal(1234U, Agent.ReadExtendedRegister(registers, register));
        }

        #endregion

        #region Segment registers

        [Theory]
        [InlineData("CS", Register.CS)]
        [InlineData("DS", Register.DS)]
        [InlineData("SS", Register.SS)]
        [InlineData("ES", Register.ES)]
        [InlineData("FS", Register.FS)]
        [InlineData("GS", Register.GS)]
        public void SegmentRegisters_RoundTrip(string name, Register register)
        {
            var agent = MovTestHarness.Run32(Asm.Text(
                "MOV AX, 0x1234",
                $"MOV {name}, AX"));

            Assert.Equal((ushort)0x1234, agent.ReadRegister(register));
        }

        [Fact]
        public void SegmentRegister_ToGeneralRegister()
        {
            var agent = MovTestHarness.Run32(Asm.Text(
                "MOV AX, 0x4321",
                "MOV DS, AX",
                "MOV BX, DS"));

            Assert.Equal((ushort)0x4321, agent.ReadRegister(Register.BX));
        }

        #endregion

        #region Degenerate cases

        [Fact]
        public void SelfMove32_IsANoOp()
        {
            var agent = MovTestHarness.Run32(Asm.Text(
                "MOV EAX, 0xDEADBEEF",
                "MOV EAX, EAX"));

            Assert.Equal(0xDEADBEEFU, agent.ReadExtendedRegister(Register.EAX));
        }

        [Fact]
        public void SelfMove64_IsANoOp()
        {
            var agent = MovTestHarness.Run64(Asm.Text(
                "MOV RAX, 0xDEADBEEFCAFEB00B",
                "MOV RAX, RAX"));

            Assert.Equal(0xDEADBEEFCAFEB00BUL, agent.ReadR64Register(Register.RAX));
        }

        /// <summary>
        /// AH and AL alias the same 16 bits of the accumulator; moving between them must not
        /// disturb the rest of the register.
        /// </summary>
        [Fact]
        public void HalfRegisterAliasing_WithinOneRegister()
        {
            var agent = MovTestHarness.Run32(Asm.Text(
                "MOV EAX, 0x11223344",
                "MOV AH, AL"));

            Assert.Equal(0x11224444U, agent.ReadExtendedRegister(Register.EAX));
        }

        [Fact]
        public void SourceRegisterIsUnchanged()
        {
            var agent = Run32("MOV EAX, EBX");
            Assert.Equal(SrcSeed32, agent.ReadExtendedRegister(Register.EBX));
        }

        #endregion
    }
}
