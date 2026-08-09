using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace picovm.VM
{
    public sealed class LoaderResult64 : ILoaderResult
    {
        public UInt64 EntryPoint;
        public ImmutableArray<byte> Image { get; private set; }
        public ImmutableList<LoaderError> Errors { get; private set; }
        public ImmutableList<object> Metadata { get; private set; }
        public bool Success => Errors == null || Errors.Count == 0;

        public LoaderResult64(
            UInt64 entryPoint,
            IEnumerable<byte>? image,
            IEnumerable<LoaderError>? errors = null,
            IEnumerable<object>? metadata = null)
        {
            EntryPoint = entryPoint;
            Image = image == null ? [] : [.. image];
            Errors = errors == null ? [] : [.. errors];
            Metadata = metadata == null ? [] : [.. metadata];
        }

        public LoaderResult64(IEnumerable<LoaderError> errors)
        {
            Image = [];
            Errors = [.. errors];
            Metadata = [];
        }

        public static LoaderResult64 Error(string message, string? sourceFile = null, ushort? lineNumber = null, ushort? column = null) => new LoaderResult64(new[] { new LoaderError(message, sourceFile, lineNumber, column) });
    }
}