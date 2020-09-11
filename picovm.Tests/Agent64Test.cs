using picovm.Compiler;
using picovm.VM;
using Xunit;

namespace picovm.Tests
{
    public class Agent64Test
    {
        private static Linux32Kernel kernel = new Linux64Kernel();

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

            var compiler = new BytecodeCompiler();
            var compiled = compiler.Compile("UNIT_TEST", programText);

            var agent = new Agent(kernel, compiled.TextSegment);
            var ret = agent.Tick();
            Xunit.Assert.Null(ret);
            Xunit.Assert.Equal((ulong)0x1111222233334444, agent.ReadR64Register(Register.RAX));

            ret = agent.Tick();
            Xunit.Assert.Null(ret);
            Xunit.Assert.Equal((ulong)0x0000000055556666, agent.ReadR64Register(Register.RAX));

            ret = agent.Tick();
            Xunit.Assert.Null(ret);
            Xunit.Assert.Equal((ulong)0x1111222233334444, agent.ReadR64Register(Register.RAX));

            ret = agent.Tick();
            Xunit.Assert.Null(ret);
            Xunit.Assert.Equal((ulong)0x1111222233337777, agent.ReadR64Register(Register.RAX));

            ret = agent.Tick();
            Xunit.Assert.Null(ret);
            Xunit.Assert.Equal((ulong)0x1111222233334444, agent.ReadR64Register(Register.RAX));

            ret = agent.Tick();
            Xunit.Assert.Null(ret);
            Xunit.Assert.Equal((ulong)0x0000000000000000, agent.ReadR64Register(Register.RAX));
        }
    }
}
