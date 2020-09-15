using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace picovm.Assembler
{
    public sealed class CompilationResult32 : CompilationResultBase, ICompilationResult
    {
        public readonly UInt32? EntryPoint;
        public readonly UInt32? TextSegmentBase;
        public readonly UInt32? DataSegmentBase;
        public readonly Dictionary<string, UInt32>? TextLabelsOffsets;
        public readonly ImmutableList<BytecodeTextSymbol32> TextSymbolReferenceOffsets;
        public readonly Dictionary<string, BytecodeDataSymbol32>? DataSymbolOffsets;

        ValueType? ICompilationResult.EntryPoint => this.EntryPoint;

        public CompilationResult32(
            uint textSegmentSize,
            uint dataSegmentSize,
            uint bssSegmentSize,
            UInt32 entryPoint,
            UInt32 textSegmentBase,
            UInt32? dataSegmentBase,
            byte[] textSegment,
            IEnumerable<KeyValuePair<string, UInt32>> textLabelsOffsets,
            IEnumerable<BytecodeTextSymbol32> textSymbolReferenceOffsets,
            ImmutableArray<byte> dataSegment,
            IEnumerable<KeyValuePair<string, BytecodeDataSymbol32>> dataSymbolOffsets,
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