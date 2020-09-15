using System.IO;
using picovm.Assembler;

namespace picovm.Packager
{
    public sealed class Inspector
    {
        public static AssemblerPackageOutputType DetectPackageOutputType(string filePath)
        {
            AssemblerPackageOutputType ret;
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                ret = DetectPackageOutputType(fs);
            }
            return ret;
        }

        public static AssemblerPackageOutputType DetectPackageOutputType(Stream stream)
        {
            // TODO: do this for real.
            return AssemblerPackageOutputType.Elf32;
        }

        public InspectionResult Inspect(Stream stream)
        {
            return new InspectionResult();
        }
    }
}