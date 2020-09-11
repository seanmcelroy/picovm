using System;
using System.ComponentModel;
using System.IO;
using picovm.Compiler;
using picovm.Packager;

namespace picovm.Packager.Elf64
{
    public sealed class LoaderElf64 : ILoader
    {
        private readonly Stream stream;
        public LoaderElf64(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead)
                throw new ArgumentException("Stream is not available for reading", nameof(stream));

            this.stream = stream;
        }

        public CompilationResult Load()
        {
            if (!stream.CanRead)
                throw new ArgumentException("Stream is not available for reading", nameof(stream));

            var elfFileHeader = new Header64();
            elfFileHeader.Read(stream);

            stream.Seek((long)elfFileHeader.E_PHOFF, SeekOrigin.Begin);
            var programHeader = new ProgramHeader64();
            programHeader.Read(stream);


            return null;
        }
    }
}