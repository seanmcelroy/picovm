using System.Collections.Generic;

namespace picovm.Compiler
{
    public sealed class CompilationResult
    {
        public uint textSegmentSize;
        public uint dataSegmentSize;
        public uint bssSegmentSize;
        public uint entryPoint;
        public uint textSegmentBase;
        public uint dataSegmentBase;

        public byte[] textSegment;

        public Dictionary<string, uint> textLabelsOffsets;
        public List<BytecodeTextSymbol> textSymbolReferenceOffsets;

        public byte[] dataSegment;

        public Dictionary<string, BytecodeDataSymbol> dataSymbolOffsets;

        public List<BytecodeBssSymbol> bssSymbols;

        public readonly List<CompilationError> errors = new List<CompilationError>();

        public bool Success => errors == null || errors.Count == 0;
    }
}