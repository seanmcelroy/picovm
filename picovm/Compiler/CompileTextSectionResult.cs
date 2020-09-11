using System.Collections.Generic;
using System.Collections.Immutable;

namespace picovm.Compiler
{
    public sealed class CompileTextSectionResult
    {
        public ImmutableArray<byte> Bytecode { get; private set; }
        public ImmutableDictionary<string, uint> LabelsOffsets { get; private set; }
        public ImmutableList<BytecodeTextSymbol> SymbolReferenceOffsets { get; private set; }

        public CompileTextSectionResult(byte[] bytecode, IEnumerable<KeyValuePair<string, uint>> labelOffsets, IEnumerable<BytecodeTextSymbol> symbolReferenceOffsets)
        {
            this.Bytecode = ImmutableArray.Create<byte>(bytecode);
            this.LabelsOffsets = ImmutableDictionary<string, uint>.Empty.AddRange(labelOffsets);
            this.SymbolReferenceOffsets = ImmutableList<BytecodeTextSymbol>.Empty.AddRange(symbolReferenceOffsets);
        }
    }
}
