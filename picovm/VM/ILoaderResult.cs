using System;
using System.Collections.Immutable;
using System.Numerics;

namespace picovm.VM
{
    public interface ILoaderResult
    {
        ImmutableArray<byte> Image { get; }
        ImmutableList<LoaderError> Errors { get; }
        bool Success { get; }
    }

    public interface ILoaderResult<TAddrSize> : ILoaderResult
         where TAddrSize : struct, INumber<TAddrSize>
    {
        TAddrSize EntryPoint { get; }
    }
}