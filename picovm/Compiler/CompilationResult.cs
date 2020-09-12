using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace picovm.Compiler
{
    public sealed class CompilationResult
    {
        public readonly uint? TextSegmentSize;
        public readonly uint? DataSegmentSize;
        public readonly uint? BssSegmentSize;
        public readonly uint? EntryPoint;
        public readonly uint? TextSegmentBase;
        public readonly uint? DataSegmentBase;
        public readonly ImmutableArray<byte>? TextSegment;
        public readonly Dictionary<string, uint>? TextLabelsOffsets;
        public readonly ImmutableList<BytecodeTextSymbol> TextSymbolReferenceOffsets;
        public readonly ImmutableArray<byte>? DataSegment;
        public readonly Dictionary<string, BytecodeDataSymbol>? DataSymbolOffsets;
        public readonly ImmutableList<BytecodeBssSymbol> BssSymbols;
        public readonly ImmutableList<CompilationError> Errors;
        public bool Success => Errors == null || Errors.Count == 0;

        public CompilationResult(
            uint textSegmentSize,
            uint dataSegmentSize,
            uint bssSegmentSize,
            uint entryPoint,
            uint textSegmentBase,
            uint? dataSegmentBase,
            byte[] textSegment,
            IEnumerable<KeyValuePair<string, uint>>? textLabelsOffsets,
            IEnumerable<BytecodeTextSymbol>? textSymbolReferenceOffsets,
            ImmutableArray<byte>? dataSegment,
            IEnumerable<KeyValuePair<string, BytecodeDataSymbol>>? dataSymbolOffsets,
            IEnumerable<BytecodeBssSymbol>? bssSymbols,
            IEnumerable<CompilationError> errors)
        {
            this.TextSegmentSize = textSegmentSize;
            this.DataSegmentSize = dataSegmentSize;
            this.BssSegmentSize = bssSegmentSize;
            this.EntryPoint = entryPoint;
            this.TextSegmentBase = textSegmentBase;
            this.DataSegmentBase = dataSegmentBase;
            this.TextSegment = ImmutableArray<byte>.Empty.AddRange(textSegment);
            this.TextLabelsOffsets = textLabelsOffsets?.ToDictionary(k => k.Key, v => v.Value);
            this.TextSymbolReferenceOffsets = ImmutableList<BytecodeTextSymbol>.Empty.AddRange(textSymbolReferenceOffsets);
            this.DataSegment = dataSegment;
            this.DataSymbolOffsets = dataSymbolOffsets?.ToDictionary(k => k.Key, v => v.Value);
            this.BssSymbols = bssSymbols == null ? ImmutableList<BytecodeBssSymbol>.Empty : ImmutableList<BytecodeBssSymbol>.Empty.AddRange(bssSymbols);
            this.Errors = ImmutableList<CompilationError>.Empty.AddRange(errors);
        }

        public CompilationResult(IEnumerable<CompilationError> errors)
        {
            this.TextSymbolReferenceOffsets = ImmutableList<BytecodeTextSymbol>.Empty;
            this.BssSymbols = ImmutableList<BytecodeBssSymbol>.Empty;
            this.Errors = ImmutableList<CompilationError>.Empty.AddRange(errors);
        }

        public static CompilationResult Error(string message, string? sourceFile = null, ushort? lineNumber = null, ushort? column = null)
        {
            return new CompilationResult(new[] { new CompilationError(message, sourceFile, lineNumber, column) });
        }
    }
}