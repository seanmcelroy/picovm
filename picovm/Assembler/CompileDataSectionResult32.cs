using System.Collections.Generic;
using System.Collections.Immutable;

namespace picovm.Assembler
{
    public sealed class CompileDataSectionResult32 : ICompileDataSectionResult
    {
        public ImmutableArray<byte> Bytecode { get; private set; }
        public ImmutableDictionary<string, BytecodeDataSymbol32> SymbolOffsets { get; private set; }

        ImmutableDictionary<string, IBytecodeDataSymbol> ICompileDataSectionResult.SymbolOffsets => this.SymbolOffsets.ToImmutableDictionary(k => k.Key, v => (IBytecodeDataSymbol)v.Value);

        public CompileDataSectionResult32(byte[] bytecode, IEnumerable<KeyValuePair<string, BytecodeDataSymbol32>> symbolOffsets)
        {
            this.Bytecode = ImmutableArray.Create<byte>(bytecode);
            this.SymbolOffsets = ImmutableDictionary<string, BytecodeDataSymbol32>.Empty.AddRange(symbolOffsets);
        }
    }
}
