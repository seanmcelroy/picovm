using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace picovm.Assembler
{
    public sealed class CompileTextSectionResult64 : ICompileTextSectionResult
    {
        public ImmutableArray<byte> Bytecode { get; private set; }
        public ImmutableDictionary<string, UInt64> LabelsOffsets { get; private set; }
        public ImmutableList<BytecodeTextSymbol64> SymbolReferenceOffsets { get; private set; }

        ImmutableDictionary<string, ValueType> ICompileTextSectionResult.LabelsOffsets => this.LabelsOffsets.ToImmutableDictionary(k => k.Key, v => (ValueType)v.Value);

        ImmutableList<IBytecodeTextSymbol> ICompileTextSectionResult.SymbolReferenceOffsets => this.SymbolReferenceOffsets.Cast<IBytecodeTextSymbol>().ToImmutableList();

        public CompileTextSectionResult64(byte[] bytecode, IEnumerable<KeyValuePair<string, UInt64>> labelOffsets, IEnumerable<BytecodeTextSymbol64> symbolReferenceOffsets)
        {
            this.Bytecode = ImmutableArray.Create<byte>(bytecode);
            this.LabelsOffsets = labelOffsets.ToImmutableDictionary();
            this.SymbolReferenceOffsets = symbolReferenceOffsets.ToImmutableList();
        }
    }
}
