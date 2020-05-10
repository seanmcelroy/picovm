using System.Linq;
using Xunit;
using agent_playground;

namespace agent_playground.Tests
{
    public class CompilerDataAllocationDirectiveTest
    {
        [Fact]
        public void Parse_Data_DB()
        {
            var dad = CompilerDataAllocationDirective.ParseLine("msg db 'Hello, world!', 0xa");
            Assert.Equal("msg", dad.Label);
            Assert.Equal("db", dad.Mnemonic);
            Assert.Equal(2, dad.Operands.Length);
            Assert.Equal("'Hello, world!'", dad.Operands[0]);
            Assert.Equal("0xa", dad.Operands[1]);
        }

        [Fact]
        public void Parse_Data_EQU()
        {
            var dad = CompilerDataAllocationDirective.ParseLine("len equ $ - msg");
            Assert.Equal("len", dad.Label);
            Assert.Equal("equ", dad.Mnemonic);
            Assert.Equal(3, dad.Operands.Length);
            Assert.Equal("$", dad.Operands[0]);
            Assert.Equal("-", dad.Operands[1]);
            Assert.Equal("msg", dad.Operands[2]);
        }

        [Fact]
        public void ConvertInfixToReversePolishNotation_Simple_Number_Subtraction()
        {
            // 3-1 => 3 1 -
            var rpn = CompilerDataAllocationDirective.ConvertInfixToReversePolishNotation(new string[] { "3", "-", "1" }, 0).ToArray();
            Assert.Equal(3, rpn.Length);
            Assert.Equal((byte)3, rpn[0]);
            Assert.Equal((byte)1, rpn[1]);
            Assert.Equal("-", rpn[2]);
        }

        [Fact]
        public void ConvertInfixToReversePolishNotation_Simple_Symbolic_Subtraction()
        {
            // A-B => A B -
            var rpn = CompilerDataAllocationDirective.ConvertInfixToReversePolishNotation(new string[] { "A", "-", "B" }, 0).ToArray();
            Assert.Equal(3, rpn.Length);
            Assert.Equal("A", rpn[0]);
            Assert.Equal("B", rpn[1]);
            Assert.Equal("-", rpn[2]);
        }

        [Fact]
        public void ConvertInfixToReversePolishNotation_Complex_Symbolic_Multiple()
        {
            // A ^ 2 + 3 * A * B + B ^ 4 => A 2 ^ 3 A * B * + B 4 ^ +
            var rpn = CompilerDataAllocationDirective.ConvertInfixToReversePolishNotation("A ^ 2 + 3 * A * B + B ^ 4".Split(' '), 0).ToArray();
            Assert.Equal(13, rpn.Length);
            Assert.Equal("A", rpn[0]);
            Assert.Equal((byte)2, rpn[1]);
            Assert.Equal("^", rpn[2]);
            Assert.Equal((byte)3, rpn[3]);
            Assert.Equal("A", rpn[4]);
            Assert.Equal("*", rpn[5]);
            Assert.Equal("B", rpn[6]);
            Assert.Equal("*", rpn[7]);
            Assert.Equal("+", rpn[8]);
            Assert.Equal("B", rpn[9]);
            Assert.Equal((byte)4, rpn[10]);
            Assert.Equal("^", rpn[11]);
            Assert.Equal("+", rpn[12]);
        }
    }
}
