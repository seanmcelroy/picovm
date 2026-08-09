using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace picovm.Assembler
{
    public sealed class CompileTextSectionResult<TAddrSize>(byte[] bytecode, IEnumerable<KeyValuePair<string, TAddrSize>> labelOffsets, IEnumerable<BytecodeTextSymbol<TAddrSize>> symbolReferenceOffsets)
        where TAddrSize : struct, IComparable, IComparable<TAddrSize>, IConvertible, IEquatable<TAddrSize>, IFormattable
    {
        public ImmutableArray<byte> Bytecode { get; private set; } = ImmutableArray.Create<byte>(bytecode);
        public ImmutableDictionary<string, TAddrSize> LabelsOffsets { get; private set; } = labelOffsets.ToImmutableDictionary();
        public ImmutableList<BytecodeTextSymbol<TAddrSize>> SymbolReferenceOffsets { get; private set; } = [.. symbolReferenceOffsets];
    }
}
