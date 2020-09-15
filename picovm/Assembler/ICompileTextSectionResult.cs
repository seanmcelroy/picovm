using System;
using System.Collections.Immutable;

namespace picovm.Assembler
{
    public interface ICompileTextSectionResult
    {
        ImmutableArray<byte> Bytecode { get; }
        ImmutableDictionary<string, ValueType> LabelsOffsets { get; }
        ImmutableList<IBytecodeTextSymbol> SymbolReferenceOffsets { get; }
    }
}
