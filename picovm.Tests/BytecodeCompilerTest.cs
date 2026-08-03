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
            Assert.True(File.Exists(Path.Combine(Environment.CurrentDirectory, sourceFileName)), $"Cannot find file {sourceFileName} for test, current directory: {Environment.CurrentDirectory}");
            var compilation = compiler.Compile(Path.Combine(Environment.CurrentDirectory, sourceFileName));
            Assert.Empty(compilation.Errors);
        }

        [Fact]
        public void CompileHelloWorldLinux32Asm()
        {
            var compiler = new BytecodeCompiler<UInt32>();
            var sourceFileName = "./../../../../picovm/asm-src/hello-world-linux32.asm";
            Assert.True(File.Exists(Path.Combine(Environment.CurrentDirectory, sourceFileName)), $"Cannot find file {sourceFileName} for test, current directory: {Environment.CurrentDirectory}");
            var compilation = compiler.Compile(Path.Combine(Environment.CurrentDirectory, sourceFileName));
            Assert.Empty(compilation.Errors);
        }

        [Fact]
        public void CompileHelloWorldLinux64Asm()
        {
            var compiler = new BytecodeCompiler<UInt64>();
            var sourceFileName = "./../../../../picovm/asm-src/hello-world-linux64.asm";
            Assert.True(File.Exists(Path.Combine(Environment.CurrentDirectory, sourceFileName)), $"Cannot find file {sourceFileName} for test, current directory: {Environment.CurrentDirectory}");
            var compilation = compiler.Compile(Path.Combine(Environment.CurrentDirectory, sourceFileName));
            Assert.Empty(compilation.Errors);
        }

        [Fact]
        public void CompileLogicalInstructionsAsm()
        {
            var compiler = new BytecodeCompiler<UInt32>();
            var sourceFileName = "./../../../../picovm/asm-src/logical-instructions.asm";
            Assert.True(File.Exists(Path.Combine(Environment.CurrentDirectory, sourceFileName)), $"Cannot find file {sourceFileName} for test, current directory: {Environment.CurrentDirectory}");
            var compilation = compiler.Compile(sourceFileName);
            Assert.Empty(compilation.Errors);
        }

        [Fact]
        public void CompileReadKeyboardAsm()
        {
            var compiler = new BytecodeCompiler<UInt32>();
            var sourceFileName = "./../../../../picovm/asm-src/read-keyboard32.asm";
            Assert.True(File.Exists(Path.Combine(Environment.CurrentDirectory, sourceFileName)), $"Cannot find file {sourceFileName} for test, current directory: {Environment.CurrentDirectory}");
            var compilation = compiler.Compile(sourceFileName);
            Assert.Empty(compilation.Errors);
        }

    }
}
