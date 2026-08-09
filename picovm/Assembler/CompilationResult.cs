using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace picovm.Assembler
{
    public sealed class CompilationResult<TAddrSize>(
        uint textSegmentSize,
        uint dataSegmentSize,
        uint bssSegmentSize,
        TAddrSize entryPoint,
        TAddrSize textSegmentBase,
        TAddrSize? dataSegmentBase,
        byte[] textSegment,
        IEnumerable<KeyValuePair<string, TAddrSize>> textLabelsOffsets,
        IEnumerable<BytecodeTextSymbol<TAddrSize>> textSymbolReferenceOffsets,
        ImmutableArray<byte> dataSegment,
        IEnumerable<KeyValuePair<string, BytecodeDataSymbol<TAddrSize>>> dataSymbolOffsets,
        IEnumerable<BytecodeBssSymbol> bssSymbols,
        IEnumerable<CompilationError> errors) : CompilationResultBase(textSegmentSize,
            dataSegmentSize,
            bssSegmentSize,
            textSegment,
            dataSegment,
            bssSymbols,
            errors), ICompilationResult
        where TAddrSize : struct, IComparable, IComparable<TAddrSize>, IConvertible, IEquatable<TAddrSize>, IFormattable
    {
        public readonly TAddrSize? EntryPoint = entryPoint;
        public readonly TAddrSize? TextSegmentBase = textSegmentBase;
        public readonly TAddrSize? DataSegmentBase = dataSegmentBase;
        public readonly Dictionary<string, TAddrSize>? TextLabelsOffsets = textLabelsOffsets?.ToDictionary(k => k.Key, v => v.Value);
        public readonly ImmutableList<BytecodeTextSymbol<TAddrSize>> TextSymbolReferenceOffsets = [.. textSymbolReferenceOffsets];
        public readonly Dictionary<string, BytecodeDataSymbol<TAddrSize>>? DataSymbolOffsets = dataSymbolOffsets?.ToDictionary(k => k.Key, v => v.Value);

        ValueType? ICompilationResult.EntryPoint => EntryPoint;
    }
}