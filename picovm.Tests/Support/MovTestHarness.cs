using System;
using System.Collections.Immutable;
using System.IO;
using picovm.Assembler;
using picovm.VM;
using Xunit;

namespace picovm.Tests.Support
{
    /// <summary>
    /// Compiles assembly snippets and boots them into an agent over a realistic flat memory
    /// image, so tests can exercise instructions that touch the data segment.
    /// </summary>
    /// <remarks>
    /// The agent constructor copies whatever byte array it is handed to address zero, and
    /// tests have historically handed it only <c>TextSegment</c>.  That is not enough for any
    /// instruction that names a data symbol: the compiler bakes symbol addresses into the text
    /// using <c>dataSegmentBase = textSegmentBase + textSegmentSize</c>, so those addresses
    /// point past the end of a text-only image and resolve to uninitialised memory.  The
    /// image built here places <c>.data</c> immediately after <c>.text</c>, matching both the
    /// compiler's relocation arithmetic and what the ELF packager/loader round trip produces.
    /// </remarks>
    public static class MovTestHarness
    {
        private static readonly Linux32Kernel Kernel32 = new();
        private static readonly Linux64Kernel Kernel64 = new();

        /// <summary>
        /// Serialises compilation so the <see cref="Console.Out"/> redirection below cannot
        /// leak into a test running concurrently in another class.  Compilation of these
        /// snippets is sub-millisecond, so the contention costs nothing.
        /// </summary>
        private static readonly System.Threading.Lock CompileGate = new();

        #region Compilation

        /// <summary>
        /// Compiles for a 32-bit address space, returning errors rather than asserting.
        /// </summary>
        public static ICompilationResult TryCompile32(params string[] lines) =>
            Compile(new BytecodeCompiler<UInt32>(), lines);

        /// <summary>
        /// Compiles for a 64-bit address space, returning errors rather than asserting.
        /// </summary>
        public static ICompilationResult TryCompile64(params string[] lines) =>
            Compile(new BytecodeCompiler<UInt64>(), lines);

        /// <summary>
        /// Compiles for a 32-bit address space and asserts the compilation succeeded.
        /// </summary>
        public static CompilationResult<UInt32> Compile32(params string[] lines)
        {
            var result = TryCompile32(lines);
            Assert.Empty(result.Errors);
            return Assert.IsType<CompilationResult<UInt32>>(result);
        }

        /// <summary>
        /// Compiles for a 64-bit address space and asserts the compilation succeeded.
        /// </summary>
        public static CompilationResult<UInt64> Compile64(params string[] lines)
        {
            var result = TryCompile64(lines);
            Assert.Empty(result.Errors);
            return Assert.IsType<CompilationResult<UInt64>>(result);
        }

        private static ICompilationResult Compile(IBytecodeCompiler compiler, string[] lines)
        {
            // The compiler narrates every symbol relocation to Console.Out.  Across a few
            // hundred parameterised cases that buries the actual test output.
            lock (CompileGate)
            {
                var saved = Console.Out;
                Console.SetOut(TextWriter.Null);
                try
                {
                    return compiler.Compile(lines, "UNIT_TEST");
                }
                finally
                {
                    Console.SetOut(saved);
                }
            }
        }

        #endregion

        #region Image layout

        /// <summary>
        /// Lays out the flat memory image: <c>.text</c> at address 0, <c>.data</c> immediately
        /// after it.  <c>.bss</c> contributes no bytes because agent memory starts zeroed.
        /// </summary>
        public static byte[] BuildImage(ICompilationResult compilation) =>
        [
            .. compilation.TextSegment ?? ImmutableArray<byte>.Empty,
            .. compilation.DataSegment ?? ImmutableArray<byte>.Empty
        ];

        #endregion

        #region Booting

        /// <summary>
        /// Boots a compiled 32-bit program into an agent positioned at its entry point.
        /// </summary>
        public static Agent Load32(CompilationResult<UInt32> compilation)
        {
            Assert.NotNull(compilation.EntryPoint);
            return new Agent(Kernel32, BuildImage(compilation), compilation.EntryPoint.Value);
        }

        /// <summary>
        /// Boots a compiled 64-bit program into an agent positioned at its entry point.
        /// </summary>
        public static Agent64 Load64(CompilationResult<UInt64> compilation)
        {
            Assert.NotNull(compilation.EntryPoint);
            return new Agent64(Kernel64, BuildImage(compilation), compilation.EntryPoint.Value);
        }

        /// <summary>Compiles and boots a 32-bit program in one step.</summary>
        public static Agent Boot32(params string[] lines) => Load32(Compile32(lines));

        /// <summary>Compiles and boots a 64-bit program in one step.</summary>
        public static Agent64 Boot64(params string[] lines) => Load64(Compile64(lines));

        #endregion

        #region Execution

        /// <summary>
        /// Executes exactly <paramref name="count"/> instructions, asserting the program has
        /// not terminated early.
        /// </summary>
        public static void Step(Agent agent, int count = 1)
        {
            for (var i = 0; i < count; i++)
                Assert.False(agent.Tick().Done, $"Program terminated during step {i + 1} of {count}.");
        }

        /// <summary>
        /// Ticks until the program terminates, failing the test rather than hanging if it
        /// runs away.  A decode desynchronisation typically shows up here.
        /// </summary>
        public static int RunToEnd(Agent agent, int maxTicks = 64)
        {
            for (var i = 0; i < maxTicks; i++)
            {
                var ret = agent.Tick();
                if (ret.Done)
                    return (int)ret.ErrorCode;
            }

            Assert.Fail($"Program did not terminate within {maxTicks} ticks.");
            return -1; // Unreachable; Assert.Fail always throws.
        }

        /// <summary>Compiles, boots, and runs a 32-bit program to termination.</summary>
        public static Agent Run32(params string[] lines)
        {
            var agent = Boot32(lines);
            RunToEnd(agent);
            return agent;
        }

        /// <summary>Compiles, boots, and runs a 64-bit program to termination.</summary>
        public static Agent64 Run64(params string[] lines)
        {
            var agent = Boot64(lines);
            RunToEnd(agent);
            return agent;
        }

        #endregion

        #region Inspection

        /// <summary>
        /// Resolves the runtime address of a <c>.data</c> symbol, as the compiler baked it
        /// into the text segment.  Symbol names are upper-cased by the compiler.
        /// </summary>
        public static uint DataSymbolAddress(CompilationResult<UInt32> compilation, string name)
        {
            Assert.NotNull(compilation.DataSymbolOffsets);
            Assert.True(compilation.DataSymbolOffsets.ContainsKey(name.ToUpperInvariant()), $"No data symbol named {name}.");
            return compilation.DataSymbolOffsets[name.ToUpperInvariant()].DataSegmentOffset;
        }

        /// <inheritdoc cref="DataSymbolAddress(CompilationResult{UInt32}, string)"/>
        public static ulong DataSymbolAddress(CompilationResult<UInt64> compilation, string name)
        {
            Assert.NotNull(compilation.DataSymbolOffsets);
            Assert.True(compilation.DataSymbolOffsets.ContainsKey(name.ToUpperInvariant()), $"No data symbol named {name}.");
            return compilation.DataSymbolOffsets[name.ToUpperInvariant()].DataSegmentOffset;
        }

        #endregion
    }
}
