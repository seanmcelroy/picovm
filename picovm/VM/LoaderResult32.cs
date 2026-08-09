using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace picovm.VM
{
    public sealed class LoaderResult32 : ILoaderResult<UInt32>
    {
        public UInt32 EntryPoint { get; private set; }
        public ImmutableArray<byte> Image { get; private set; }
        public ImmutableList<LoaderError> Errors { get; private set; }
        public ImmutableList<object> Metadata { get; private set; }
        public bool Success => Errors == null || Errors.Count == 0;

        public LoaderResult32(
            UInt32 entryPoint,
            IEnumerable<byte>? image,
            IEnumerable<LoaderError>? errors = null,
            IEnumerable<object>? metadata = null)
        {
            EntryPoint = entryPoint;
            Image = image == null ? [] : [.. image];
            Errors = errors == null ? [] : [.. errors];
            Metadata = metadata == null ? [] : [.. metadata];
        }

        public LoaderResult32(IEnumerable<LoaderError> errors)
        {
            Image = [];
            Errors = [.. errors];
            Metadata = [];
        }

        public static LoaderResult32 Error(string message, string? sourceFile = null, ushort? lineNumber = null, ushort? column = null) => new LoaderResult32(new[] { new LoaderError(message, sourceFile, lineNumber, column) });
    }
}