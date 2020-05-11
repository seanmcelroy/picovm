using Xunit;
using picovm.Compiler;

namespace picovm.Tests
{
    public class CompilerBssAllocationDirectiveTest
    {
        [Fact]
        public void Parse_Bss_MisformattedLabel_Space_Before_Colon()
        {
            var bad = CompilerBssAllocationDirective.ParseLine("mean : resq 1");
            Assert.Equal("mean", bad.Label);
            Assert.Equal("resq", bad.Mnemonic);
            Assert.Equal((ushort)1, bad.Size);
        }
    }
}
