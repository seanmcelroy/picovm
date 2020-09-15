using System.Collections.Immutable;
using picovm.VM;

namespace picovm.VM
{
    public interface ILoader
    {
        ILoaderResult LoadImage();
        ImmutableList<object> LoadMetadata();
    }
}