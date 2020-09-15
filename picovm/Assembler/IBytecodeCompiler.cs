using System.Collections.Generic;

namespace picovm.Assembler
{
    public interface IBytecodeCompiler
    {

        ICompilationResult Compile(string sourceFilename);

        ICompilationResult Compile(IEnumerable<string> programLines, string? sourceFilename = null);
    }
}