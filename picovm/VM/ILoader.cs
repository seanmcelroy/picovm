using picovm.VM;

namespace picovm.VM
{
    public interface ILoader
    {
        ILoaderResult Load();
    }
}