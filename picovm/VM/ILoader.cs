using System.Collections.Immutable;

namespace picovm.VM
{
    public interface ILoader
    {
        ILoaderResult LoadImage();
        ImmutableList<object> LoadMetadata();
    }
}