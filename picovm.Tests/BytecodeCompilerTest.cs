using picovm.Assembler;
using picovm.VM;
using System;
using System.IO;
using Xunit;

namespace picovm.Tests
{
    public class BytecodeCompilerTest
    {
        [Fact]
        public void CompileDebugAsm()
        {
            var compiler = new BytecodeCompiler<UInt32>();
            var sourceFileName = "./../../../../picovm/asm-src/debug.asm";
            Xunit.Assert.True(File.Exists(Path.Combine(System.Environment.CurrentDirectory, sourceFileName)), $"Cannot find file {sourceFileName} for test, current directory: {System.Environment.CurrentDirectory}");
            var compilation = compiler.Compile(Path.Combine(System.Environment.CurrentDirectory, sourceFileName));
            Xunit.Assert.Equal(0, compilation.Errors.Count);
        }

        [Fact]
        public void CompileHelloWorldLinux32Asm()
        {
            var compiler = new BytecodeCompiler<UInt32>();
            var sourceFileName = "./../../../../picovm/asm-src/hello-world-linux32.asm";
            Xunit.Assert.True(File.Exists(Path.Combine(System.Environment.CurrentDirectory, sourceFileName)), $"Cannot find file {sourceFileName} for test, current directory: {System.Environment.CurrentDirectory}");
            var compilation = compiler.Compile(Path.Combine(System.Environment.CurrentDirectory, sourceFileName));
            Xunit.Assert.Equal(0, compilation.Errors.Count);
        }

        [Fact]
        public void CompileHelloWorldLinux64Asm()
        {
            var compiler = new BytecodeCompiler<UInt64>();
            var sourceFileName = "./../../../../picovm/asm-src/hello-world-linux64.asm";
            Xunit.Assert.True(File.Exists(Path.Combine(System.Environment.CurrentDirectory, sourceFileName)), $"Cannot find file {sourceFileName} for test, current directory: {System.Environment.CurrentDirectory}");
            var compilation = compiler.Compile(Path.Combine(System.Environment.CurrentDirectory, sourceFileName));
            Xunit.Assert.Equal(0, compilation.Errors.Count);
        }

        [Fact]
        public void CompileLogicalInstructionsAsm()
        {
            var compiler = new BytecodeCompiler<UInt32>();
            var sourceFileName = "./../../../../picovm/asm-src/logical-instructions.asm";
            Xunit.Assert.True(File.Exists(Path.Combine(System.Environment.CurrentDirectory, sourceFileName)), $"Cannot find file {sourceFileName} for test, current directory: {System.Environment.CurrentDirectory}");
            var compilation = compiler.Compile(sourceFileName);
            Xunit.Assert.Equal(0, compilation.Errors.Count);
        }

        [Fact]
        public void CompileReadKeyboardAsm()
        {
            var compiler = new BytecodeCompiler<UInt32>();
            var sourceFileName = "./../../../../picovm/asm-src/read-keyboard32.asm";
            Xunit.Assert.True(File.Exists(Path.Combine(System.Environment.CurrentDirectory, sourceFileName)), $"Cannot find file {sourceFileName} for test, current directory: {System.Environment.CurrentDirectory}");
            var compilation = compiler.Compile(sourceFileName);
            Xunit.Assert.Equal(0, compilation.Errors.Count);
        }

    }
}
