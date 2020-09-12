using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace picovm.VM
{
    public sealed class LoaderResult
    {
        public UInt64 EntryPoint;
        public ImmutableArray<byte> Image;
        public readonly ImmutableList<LoaderError> Errors;
        public bool Success => Errors == null || Errors.Count == 0;

        public LoaderResult(
            UInt64 entryPoint,
            IEnumerable<byte>? image,
            IEnumerable<LoaderError>? errors = null)
        {
            this.EntryPoint = entryPoint;
            this.Image = image == null ? ImmutableArray<byte>.Empty : ImmutableArray<byte>.Empty.AddRange(image);
            this.Errors = errors == null ? ImmutableList<LoaderError>.Empty : ImmutableList<LoaderError>.Empty.AddRange(errors);
        }

        public LoaderResult(IEnumerable<LoaderError> errors)
        {
            this.Image = ImmutableArray<byte>.Empty;
            this.Errors = ImmutableList<LoaderError>.Empty.AddRange(errors);
        }

        public static LoaderResult Error(string message, string? sourceFile = null, ushort? lineNumber = null, ushort? column = null)
        {
            return new LoaderResult(new[] { new LoaderError(message, sourceFile, lineNumber, column) });
        }
    }
}