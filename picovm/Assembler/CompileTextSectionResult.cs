using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Numerics;

namespace picovm.Assembler
{
    public sealed class CompileTextSectionResult<TAddrSize>(byte[] bytecode, IEnumerable<KeyValuePair<string, TAddrSize>> labelOffsets, IEnumerable<BytecodeTextSymbol<TAddrSize>> symbolReferenceOffsets)
        where TAddrSize : struct, INumber<TAddrSize>
    {
        public ImmutableArray<byte> Bytecode { get; private set; } = ImmutableArray.Create<byte>(bytecode);
        public ImmutableDictionary<string, TAddrSize> LabelsOffsets { get; private set; } = labelOffsets.ToImmutableDictionary(StringComparer.InvariantCultureIgnoreCase);
        public ImmutableList<BytecodeTextSymbol<TAddrSize>> SymbolReferenceOffsets { get; private set; } = [.. symbolReferenceOffsets];
    }
}
