using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace picovm.Assembler
{
    public sealed class CompileDataSectionResult<TAddrSize>(byte[] bytecode, IEnumerable<KeyValuePair<string, BytecodeDataSymbol<TAddrSize>>> symbolOffsets)
        where TAddrSize : struct, IComparable, IComparable<TAddrSize>, IConvertible, IEquatable<TAddrSize>, IFormattable 

    {
        public ImmutableArray<byte> Bytecode { get; private set; } = ImmutableArray.Create<byte>(bytecode);
        public ImmutableDictionary<string, BytecodeDataSymbol<TAddrSize>> SymbolOffsets { get; private set; } = symbolOffsets.ToImmutableDictionary(StringComparer.InvariantCultureIgnoreCase);
    }
}
