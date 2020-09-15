using System;
using System.Collections.Immutable;

namespace picovm.Assembler
{
    public interface ICompilationResult
    {
        public uint? TextSegmentSize { get; }
        public uint? DataSegmentSize { get; }
        public uint? BssSegmentSize { get; }
        public ValueType? EntryPoint { get; }
        //public readonly UInt32? TextSegmentBase;
        //public readonly UInt32? DataSegmentBase;
        public ImmutableArray<byte>? TextSegment { get; }
        //public readonly Dictionary<string, UInt32>? TextLabelsOffsets;
        //public readonly ImmutableList<BytecodeTextSymbol32> TextSymbolReferenceOffsets;
        public ImmutableArray<byte>? DataSegment { get; }
        //public readonly Dictionary<string, BytecodeDataSymbol32>? DataSymbolOffsets;
        public ImmutableList<BytecodeBssSymbol> BssSymbols { get; }
        public ImmutableList<CompilationError> Errors { get; }
        public bool Success { get; }
    }
}