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
        public bool Success => Errors == null || Errors.Count == 0;

        public LoaderResult32(
            UInt32 entryPoint,
            IEnumerable<byte>? image,
            IEnumerable<LoaderError>? errors = null)
        {
            this.EntryPoint = entryPoint;
            this.Image = image == null ? ImmutableArray<byte>.Empty : ImmutableArray<byte>.Empty.AddRange(image);
            this.Errors = errors == null ? ImmutableList<LoaderError>.Empty : ImmutableList<LoaderError>.Empty.AddRange(errors);
        }

        public LoaderResult32(IEnumerable<LoaderError> errors)
        {
            this.Image = ImmutableArray<byte>.Empty;
            this.Errors = ImmutableList<LoaderError>.Empty.AddRange(errors);
        }

        public static LoaderResult32 Error(string message, string? sourceFile = null, ushort? lineNumber = null, ushort? column = null)
        {
            return new LoaderResult32(new[] { new LoaderError(message, sourceFile, lineNumber, column) });
        }
    }
}