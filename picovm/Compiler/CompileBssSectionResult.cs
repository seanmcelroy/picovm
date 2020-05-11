using System.Collections.Generic;

namespace picovm.Compiler
{
    public sealed class CompileBssSectionResult
    {
        public List<BytecodeBssSymbol> Symbols { get; private set; }

        public CompileBssSectionResult(List<BytecodeBssSymbol> symbols)
        {
            this.Symbols = symbols;
        }
    }
}
