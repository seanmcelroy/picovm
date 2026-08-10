using System;
using picovm.Assembler;
using picovm.VM;
using Xunit;

namespace picovm.Tests
{
    /// <summary>
    /// Multi-instruction integration tests for the 64-bit agent.
    /// </summary>
    /// <remarks>
    /// Focused MOV coverage lives in the <c>Mov*Tests</c> suites; what remains here exercises
    /// MOV together with the logic instructions, which those suites do not cover.
    /// </remarks>
    public class Agent64Test
    {
        private static readonly Linux64Kernel kernel = new();

        [Fact]
        public void MOV_Bonanza64()
        {
            var programText = new string[] {
                "section	.text",
                "global _start",
                "_start:",
                "mov  rax, 0x1111222233334444 ;           rax = 0x1111222233334444",
                "mov  eax, 0x55556666         ; actual:   rax = 0x0000000055556666",
                "mov  rax, 0x1111222233334444 ;           rax = 0x1111222233334444",
                "mov  ax, 0x7777              ;           rax = 0x1111222233337777 (works!)",
                "mov  rax, 0x1111222233334444 ;           rax = 0x1111222233334444",
                "xor  eax, eax                ; actual:   rax = 0x0000000000000000",
                "                             ; again, it wiped whole register"
            };

            var compiler = new BytecodeCompiler<UInt64>();
            var compiled = compiler.Compile(programText, "UNIT_TEST");
            Assert.NotEmpty(compiled.TextSegment);

            var agent = new Agent64(kernel, [.. compiled.TextSegment.Value], 0);
            var ret = agent.Tick();
            Assert.False(ret.Done);
            Assert.Equal(0x1111222233334444UL, agent.ReadR64Register(Register.RAX));

            ret = agent.Tick();
            Assert.False(ret.Done);
            Assert.Equal(0x0000000055556666UL, agent.ReadR64Register(Register.RAX));

            ret = agent.Tick();
            Assert.False(ret.Done);
            Assert.Equal(0x1111222233334444UL, agent.ReadR64Register(Register.RAX));

            ret = agent.Tick();
            Assert.False(ret.Done);
            Assert.Equal(0x1111222233337777UL, agent.ReadR64Register(Register.RAX));

            ret = agent.Tick();
            Assert.False(ret.Done);
            Assert.Equal(0x1111222233334444UL, agent.ReadR64Register(Register.RAX));

            ret = agent.Tick();
            Assert.False(ret.Done);
            Assert.Equal(0x0000000000000000UL, agent.ReadR64Register(Register.RAX));
        }

    }
}
