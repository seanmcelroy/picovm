using System;
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
            if (!stream.CanRead)
                throw new ArgumentException("Stream is not available for reading", nameof(stream));
            if (!stream.CanSeek)
                throw new ArgumentException("Stream is not available for seeking", nameof(stream));

            if (Elf.Elf32.Header32.IsFileType(stream))
                return AssemblerPackageOutputType.Elf32;
            if (Elf.Elf64.Header64.IsFileType(stream))
                return AssemblerPackageOutputType.Elf64;

            return AssemblerPackageOutputType.Unknown;
        }

        public static InspectionResult InspectAsElf64(string filePath)
        {
            InspectionResult ret;
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                ret = InspectAsElf64(fs);
            }
            return ret;
        }

        public static InspectionResult InspectAsElf64(Stream stream)
        {
            var loader = new Elf.Elf64.LoaderElf64(stream);
            var metadata = loader.LoadMetadata();
            return new InspectionResult(metadata);
        }

        public InspectionResult Inspect(Stream stream)
        {
            return new InspectionResult();
        }
    }
}