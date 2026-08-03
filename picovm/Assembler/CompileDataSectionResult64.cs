using System.Collections.Generic;
using System.Collections.Immutable;

namespace picovm.Assembler
{
    public sealed class CompileDataSectionResult64(byte[] bytecode, IEnumerable<KeyValuePair<string, BytecodeDataSymbol64>> symbolOffsets) : ICompileDataSectionResult
    {
        public ImmutableArray<byte> Bytecode { get; private set; } = ImmutableArray.Create<byte>(bytecode);
        public ImmutableDictionary<string, BytecodeDataSymbol64> SymbolOffsets { get; private set; } = symbolOffsets.ToImmutableDictionary();

        ImmutableDictionary<string, IBytecodeDataSymbol> ICompileDataSectionResult.SymbolOffsets => this.SymbolOffsets.ToImmutableDictionary(k => k.Key, v => (IBytecodeDataSymbol)v.Value);
    }
}
