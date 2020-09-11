using System.Collections.Generic;
using System.Collections.Immutable;

namespace picovm.Compiler
{
    public sealed class CompileDataSectionResult
    {
        public ImmutableArray<byte> Bytecode { get; private set; }
        public ImmutableDictionary<string, BytecodeDataSymbol> SymbolOffsets { get; private set; }

        public CompileDataSectionResult(byte[] bytecode, IEnumerable<KeyValuePair<string, BytecodeDataSymbol>> symbolOffsets)
        {
            this.Bytecode = ImmutableArray.Create<byte>(bytecode);
            this.SymbolOffsets = ImmutableDictionary<string, BytecodeDataSymbol>.Empty.AddRange(symbolOffsets);
        }
    }
}
