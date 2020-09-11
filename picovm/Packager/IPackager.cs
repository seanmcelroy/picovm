using System.IO;

namespace picovm.Packager
{
    public interface IPackager
    {
        void Write(Stream stream);
    }
}