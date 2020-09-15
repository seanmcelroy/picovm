using System.Collections.Immutable;

namespace picovm.VM
{
    public interface ILoaderResult
    {
        ImmutableArray<byte> Image { get; }
        ImmutableList<LoaderError> Errors { get; }
        bool Success { get; }
    }

    public interface ILoaderResult<TAddrSize> : ILoaderResult
    {
        TAddrSize EntryPoint { get; }
    }
}