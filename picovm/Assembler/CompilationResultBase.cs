using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace picovm.Assembler
{
    public class CompilationResultBase : ICompilationResult
    {
        public uint? TextSegmentSize { get; private set; }
        public uint? DataSegmentSize { get; private set; }
        public uint? BssSegmentSize { get; private set; }
        public ImmutableArray<byte>? TextSegment { get; private set; }
        public ImmutableArray<byte>? DataSegment { get; private set; }
        public ImmutableList<BytecodeBssSymbol> BssSymbols { get; private set; }
        public ImmutableList<CompilationError> Errors { get; private set; }
        public bool Success => Errors == null || Errors.Count == 0;

        ValueType? ICompilationResult.EntryPoint => throw new NotImplementedException();

        protected CompilationResultBase(
            uint textSegmentSize,
            uint dataSegmentSize,
            uint bssSegmentSize,
            byte[] textSegment,
            ImmutableArray<byte> dataSegment,
            IEnumerable<BytecodeBssSymbol> bssSymbols,
            IEnumerable<CompilationError> errors)
        {
            TextSegmentSize = textSegmentSize;
            DataSegmentSize = dataSegmentSize;
            BssSegmentSize = bssSegmentSize;
            TextSegment = textSegment.ToImmutableArray();
            DataSegment = dataSegment;
            BssSymbols = [.. bssSymbols];
            Errors = [.. errors];
        }

        public CompilationResultBase(IEnumerable<CompilationError> errors)
        {
            BssSymbols = [];
            Errors = [.. errors];
        }

        public static CompilationResultBase Error(string message, string? sourceFile = null, ushort? lineNumber = null, ushort? column = null)
        {
            return new CompilationResultBase([new CompilationError(message, sourceFile, lineNumber, column)]);
        }
    }
}