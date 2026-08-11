using System;
using System.Collections.Immutable;
using System.Numerics;

namespace picovm.VM
{
    public interface ILoader<TAddrSize>
         where TAddrSize : struct, INumber<TAddrSize>
    {
        ILoaderResult<TAddrSize> LoadImage();
        ImmutableList<object> LoadMetadata();
    }
}