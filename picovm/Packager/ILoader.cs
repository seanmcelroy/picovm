using System.IO;
using picovm.Compiler;

namespace picovm.Packager
{
    public interface ILoader
    {
        CompilationResult Load();
    }
}