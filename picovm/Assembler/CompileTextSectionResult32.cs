using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace picovm.Assembler
{
    public sealed class CompileTextSectionResult32(byte[] bytecode, IEnumerable<KeyValuePair<string, UInt32>> labelOffsets, IEnumerable<BytecodeTextSymbol32> symbolReferenceOffsets) : ICompileTextSectionResult
    {
        public ImmutableArray<byte> Bytecode { get; private set; } = ImmutableArray.Create<byte>(bytecode);
        public ImmutableDictionary<string, UInt32> LabelsOffsets { get; private set; } = labelOffsets.ToImmutableDictionary();
        public ImmutableList<BytecodeTextSymbol32> SymbolReferenceOffsets { get; private set; } = symbolReferenceOffsets.ToImmutableList();

        ImmutableDictionary<string, ValueType> ICompileTextSectionResult.LabelsOffsets => this.LabelsOffsets.ToImmutableDictionary(k => k.Key, v => (ValueType)v.Value);

        ImmutableList<IBytecodeTextSymbol> ICompileTextSectionResult.SymbolReferenceOffsets => this.SymbolReferenceOffsets.Cast<IBytecodeTextSymbol>().ToImmutableList();
    }
}
