using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace picovm.Assembler
{
    public sealed class CompilationResult64 : CompilationResultBase, ICompilationResult
    {
        public readonly UInt64? EntryPoint;
        public readonly UInt64? TextSegmentBase;
        public readonly UInt64? DataSegmentBase;
        public readonly Dictionary<string, UInt64>? TextLabelsOffsets;
        public readonly ImmutableList<BytecodeTextSymbol64> TextSymbolReferenceOffsets;
        public readonly Dictionary<string, BytecodeDataSymbol64>? DataSymbolOffsets;

        ValueType? ICompilationResult.EntryPoint => this.EntryPoint;

        public CompilationResult64(
            uint textSegmentSize,
            uint dataSegmentSize,
            uint bssSegmentSize,
            UInt64 entryPoint,
            UInt64 textSegmentBase,
            UInt64? dataSegmentBase,
            byte[] textSegment,
            IEnumerable<KeyValuePair<string, UInt64>> textLabelsOffsets,
            IEnumerable<BytecodeTextSymbol64> textSymbolReferenceOffsets,
            ImmutableArray<byte> dataSegment,
            IEnumerable<KeyValuePair<string, BytecodeDataSymbol64>> dataSymbolOffsets,
            IEnumerable<BytecodeBssSymbol> bssSymbols,
            IEnumerable<CompilationError> errors) : base(textSegmentSize,
                dataSegmentSize,
                bssSegmentSize,
                textSegment,
                dataSegment,
                bssSymbols,
                errors)
        {
            this.EntryPoint = entryPoint;
            this.TextSegmentBase = textSegmentBase;
            this.DataSegmentBase = dataSegmentBase;
            this.TextLabelsOffsets = textLabelsOffsets?.ToDictionary(k => k.Key, v => v.Value);
            this.TextSymbolReferenceOffsets = textSymbolReferenceOffsets.ToImmutableList();
            this.DataSymbolOffsets = dataSymbolOffsets?.ToDictionary(k => k.Key, v => v.Value);
        }
    }
}