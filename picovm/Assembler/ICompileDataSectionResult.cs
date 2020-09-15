using System.Collections.Immutable;

namespace picovm.Assembler
{
    public interface ICompileDataSectionResult
    {
        public ImmutableArray<byte> Bytecode { get; }
        public ImmutableDictionary<string, IBytecodeDataSymbol> SymbolOffsets { get; }
    }
}
