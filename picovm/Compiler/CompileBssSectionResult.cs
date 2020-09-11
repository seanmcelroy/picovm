using System.Collections.Generic;
using System.Collections.Immutable;

namespace picovm.Compiler
{
    public sealed class CompileBssSectionResult
    {
        public ImmutableList<BytecodeBssSymbol> Symbols { get; private set; }

        public CompileBssSectionResult(IEnumerable<BytecodeBssSymbol> symbols) =>
            this.Symbols = ImmutableList<BytecodeBssSymbol>.Empty.AddRange(symbols);
    }
}
