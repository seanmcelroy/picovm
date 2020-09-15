using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace picovm.Assembler
{
    public sealed class CompileTextSectionResult32 : ICompileTextSectionResult
    {
        public ImmutableArray<byte> Bytecode { get; private set; }
        public ImmutableDictionary<string, UInt32> LabelsOffsets { get; private set; }
        public ImmutableList<BytecodeTextSymbol32> SymbolReferenceOffsets { get; private set; }

        ImmutableDictionary<string, ValueType> ICompileTextSectionResult.LabelsOffsets => this.LabelsOffsets.ToImmutableDictionary(k => k.Key, v => (ValueType)v.Value);

        ImmutableList<IBytecodeTextSymbol> ICompileTextSectionResult.SymbolReferenceOffsets => this.SymbolReferenceOffsets.Cast<IBytecodeTextSymbol>().ToImmutableList();

        public CompileTextSectionResult32(byte[] bytecode, IEnumerable<KeyValuePair<string, UInt32>> labelOffsets, IEnumerable<BytecodeTextSymbol32> symbolReferenceOffsets)
        {
            this.Bytecode = ImmutableArray.Create<byte>(bytecode);
            this.LabelsOffsets = ImmutableDictionary<string, UInt32>.Empty.AddRange(labelOffsets);
            this.SymbolReferenceOffsets = ImmutableList<BytecodeTextSymbol32>.Empty.AddRange(symbolReferenceOffsets);
        }
    }
}
