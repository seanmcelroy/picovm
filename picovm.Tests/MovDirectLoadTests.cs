using System;
using picovm.Tests.Support;
using picovm.VM;
using Xunit;

namespace picovm.Tests
{
    /// <summary>
    /// Coverage for <c>MOV_DIRECT_LOAD</c> (<c>MOV EAX, [counter]</c>) -- a load from an
    /// absolute address resolved at compile time into a register.
    /// </summary>
    /// <remarks>
    /// The destination register's width tells the VM how many bytes to read.  Sister opcodes:
    /// <c>MOV_DIRECT_STORE</c> mirrors this direction (register &#8594; address);
    /// <c>MOV_DIRECT_IMMEDIATE</c> carries an explicit size byte because neither of its
    /// operands is a register that could imply one.
    /// </remarks>
    public class MovDirectLoadTests
    {
        /// <summary>
        /// Distinguishable little-endian bytes: a wrong-width load reveals itself in the value.
        /// </summary>
        private const string Counter = "counter db 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88";

        #region Loads at each width

        [Theory]
        [InlineData("AL", 0x00000011U)]
        [InlineData("AX", 0x00002211U)]
        [InlineData("EAX", 0x44332211U)]
        public void Load32(string reg, uint expected)
        {
            var agent = MovTestHarness.Run32(
                Asm.WithData([Counter], $"MOV {reg}, [counter]"));

            Assert.Equal(expected, Read32(agent, reg));
        }

        [Theory]
        [InlineData("AL", 0x0000000000000011UL)]
        [InlineData("AX", 0x0000000000002211UL)]
        [InlineData("EAX", 0x0000000044332211UL)]
        [InlineData("RAX", 0x8877665544332211UL)]
        public void Load64(string reg, ulong expected)
        {
            var agent = MovTestHarness.Run64(
                Asm.WithData([Counter], $"MOV {reg}, [counter]"));

            Assert.Equal(expected, Read64(agent, reg));
        }

        #endregion

        #region Size hints

        /// <summary>
        /// A hint that agrees with the destination register is redundant but harmless.
        /// </summary>
        [Theory]
        [InlineData("BYTE", "AL")]
        [InlineData("WORD", "AX")]
        [InlineData("DWORD", "EAX")]
        public void AgreeingSizeHint_IsAccepted(string hint, string reg)
        {
            var agent = MovTestHarness.Run32(
                Asm.WithData([Counter], $"MOV {hint} {reg}, [counter]"));

            // The counter's first byte is 0x11, which fits in AL regardless of load width.
            Assert.Equal((byte)0x11, agent.ReadHalfRegister(Register.AL));
        }

        /// <summary>
        /// LOAD takes its width from the destination register, so a disagreeing hint would be
        /// silently ignored -- the VM would still load the register's width.  Reject at compile
        /// time so callers cannot lie about what the instruction does.
        /// </summary>
        [Theory]
        [InlineData("MOV BYTE EAX, [counter]")]
        [InlineData("MOV WORD EAX, [counter]")]
        [InlineData("MOV DWORD AL, [counter]")]
        public void DisagreeingSizeHint32_Throws(string instruction)
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                MovTestHarness.TryCompile32(Asm.WithData([Counter], instruction)));

            Assert.Contains("disagrees with destination register", ex.Message);
        }

        [Theory]
        [InlineData("MOV DWORD RAX, [counter]")]
        [InlineData("MOV BYTE RAX, [counter]")]
        public void DisagreeingSizeHint64_Throws(string instruction)
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                MovTestHarness.TryCompile64(Asm.WithData([Counter], instruction)));

            Assert.Contains("disagrees with destination register", ex.Message);
        }

        /// <summary>
        /// Contrast with <see cref="MovDirectImmediateTests"/>: IMMEDIATE requires an explicit
        /// hint because it has no register to imply one.  LOAD does not.
        /// </summary>
        [Fact]
        public void UnhintedLoad_IsAccepted()
        {
            var agent = MovTestHarness.Run32(Asm.WithData([Counter], "MOV EAX, [counter]"));
            Assert.Equal(0x44332211U, agent.ReadExtendedRegister(Register.EAX));
        }

        #endregion

        #region Round trip

        /// <summary>
        /// Direct store into a symbol then direct load out of it: if either half computes the
        /// address wrong the value will not survive the round trip.  Distinct from the
        /// STORE&#8594;INDIRECT_LOAD trip in <see cref="MovDirectImmediateTests"/> because it
        /// exercises both direct halves.
        /// </summary>
        [Theory]
        [InlineData("BYTE", "0x41", "AL", 0x00000041U)]
        [InlineData("WORD", "0xBEEF", "AX", 0x0000BEEFU)]
        [InlineData("DWORD", "0xDEADBEEF", "EAX", 0xDEADBEEFU)]
        public void DirectStoreThenDirectLoad_RoundTrips32(string hint, string literal, string reg, uint expected)
        {
            var agent = MovTestHarness.Run32(
                Asm.WithData(["scratch db 0, 0, 0, 0, 0, 0, 0, 0"],
                    $"MOV {hint} [scratch], {literal}",
                    $"MOV {reg}, [scratch]"));

            Assert.Equal(expected, Read32(agent, reg));
        }

        [Theory]
        [InlineData("BYTE", "0x41", "AL", 0x0000000000000041UL)]
        [InlineData("WORD", "0xBEEF", "AX", 0x000000000000BEEFUL)]
        [InlineData("DWORD", "0xDEADBEEF", "EAX", 0x00000000DEADBEEFUL)]
        [InlineData("QWORD", "0x1122334455667788", "RAX", 0x1122334455667788UL)]
        public void DirectStoreThenDirectLoad_RoundTrips64(string hint, string literal, string reg, ulong expected)
        {
            var agent = MovTestHarness.Run64(
                Asm.WithData(["scratch db 0, 0, 0, 0, 0, 0, 0, 0"],
                    $"MOV {hint} [scratch], {literal}",
                    $"MOV {reg}, [scratch]"));

            Assert.Equal(expected, Read64(agent, reg));
        }

        #endregion

        #region Address resolution

        /// <summary>
        /// Two adjacent symbols: a load from one must not pick up bytes from the other.  If
        /// the compiler baked in the wrong address or the VM read the wrong width, at least
        /// one of these two loads would land in the neighbour.
        /// </summary>
        [Fact]
        public void Load_ReadsFromSymbolAddressNotNeighbour()
        {
            var agent = MovTestHarness.Run32(
                Asm.WithData(
                    ["first  db 0xEF, 0xBE, 0xAD, 0xDE",
                     "second db 0x11, 0x22, 0x33, 0x44"],
                    "MOV EAX, [first]",
                    "MOV EBX, [second]"));

            Assert.Equal(0xDEADBEEFU, agent.ReadExtendedRegister(Register.EAX));
            Assert.Equal(0x44332211U, agent.ReadExtendedRegister(Register.EBX));
        }

        #endregion

        #region Helpers

        private static uint Read32(Agent agent, string reg) => reg switch
        {
            "AL" => agent.ReadHalfRegister(Register.AL),
            "AX" => agent.ReadRegister(Register.AX),
            "EAX" => agent.ReadExtendedRegister(Register.EAX),
            _ => throw new InvalidOperationException($"No 32-bit reader for {reg}")
        };

        private static ulong Read64(Agent64 agent, string reg) => reg switch
        {
            "AL" => agent.ReadHalfRegister(Register.AL),
            "AX" => agent.ReadRegister(Register.AX),
            "EAX" => agent.ReadExtendedRegister(Register.EAX),
            "RAX" => agent.ReadR64Register(Register.RAX),
            _ => throw new InvalidOperationException($"No 64-bit reader for {reg}")
        };

        #endregion
    }
}
