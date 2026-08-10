using System;
using System.Collections.Immutable;

namespace picovm.VM
{
    public interface ILoader<TAddrSize>
         where TAddrSize : struct, IComparable, IComparable<TAddrSize>, IConvertible, IEquatable<TAddrSize>, IFormattable
    {
        ILoaderResult<TAddrSize> LoadImage();
        ImmutableList<object> LoadMetadata();
    }
}