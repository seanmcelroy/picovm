using System.Collections.Generic;
using System.Collections.Immutable;

namespace picovm.Assembler
{
    public sealed class CompileDataSectionResult64 : ICompileDataSectionResult
    {
        public ImmutableArray<byte> Bytecode { get; private set; }
        public ImmutableDictionary<string, BytecodeDataSymbol64> SymbolOffsets { get; private set; }

        ImmutableDictionary<string, IBytecodeDataSymbol> ICompileDataSectionResult.SymbolOffsets => this.SymbolOffsets.ToImmutableDictionary(k => k.Key, v => (IBytecodeDataSymbol)v.Value);

        public CompileDataSectionResult64(byte[] bytecode, IEnumerable<KeyValuePair<string, BytecodeDataSymbol64>> symbolOffsets)
        {
            this.Bytecode = ImmutableArray.Create<byte>(bytecode);
            this.SymbolOffsets = ImmutableDictionary<string, BytecodeDataSymbol64>.Empty.AddRange(symbolOffsets);
        }
    }
}
