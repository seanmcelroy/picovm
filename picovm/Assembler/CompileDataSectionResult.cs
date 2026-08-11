using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Numerics;

namespace picovm.Assembler
{
    public sealed class CompileDataSectionResult<TAddrSize>(byte[] bytecode, IEnumerable<KeyValuePair<string, BytecodeDataSymbol<TAddrSize>>> symbolOffsets)
        where TAddrSize : struct, INumber<TAddrSize> 

    {
        public ImmutableArray<byte> Bytecode { get; private set; } = ImmutableArray.Create(bytecode);
        public ImmutableDictionary<string, BytecodeDataSymbol<TAddrSize>> SymbolOffsets { get; private set; } = symbolOffsets.ToImmutableDictionary(StringComparer.InvariantCultureIgnoreCase);
    }
}
