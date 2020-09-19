using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using picovm.VM;

namespace picovm.Packager.Elf.Elf64
{
    public sealed class LoaderElf64 : ILoader
    {
        private readonly Stream stream;

        public LoaderElf64(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            this.stream = stream;
        }

        public LoaderResult64 LoadImage()
        {
            if (!stream.CanRead)
                throw new ArgumentException("Stream is not available for reading", nameof(stream));
            if (!stream.CanSeek)
                throw new ArgumentException("Stream is not available for seeking", nameof(stream));

            var elfFileHeader = new Header64();
            elfFileHeader.Read(stream);

            stream.Seek((long)elfFileHeader.E_PHOFF, SeekOrigin.Begin);
            var programHeader = new ProgramHeader64();
            programHeader.Read(stream);

            var image = new byte[(int)programHeader.P_FILESZ - elfFileHeader.E_EHSIZE - (elfFileHeader.E_PHNUM * elfFileHeader.E_PHENTSIZE)];
            UInt64 imageOffset =
                elfFileHeader.E_EHSIZE
                + (UInt64)elfFileHeader.E_EHSIZE.CalculateRoundUpTo16Pad()
                + (UInt64)(elfFileHeader.E_PHNUM * (elfFileHeader.E_PHENTSIZE + elfFileHeader.E_PHENTSIZE.CalculateRoundUpTo16Pad()));
            stream.Seek((long)imageOffset, SeekOrigin.Begin);
            stream.Read(image, 0, image.Length);

            return new LoaderResult64(elfFileHeader.E_ENTRY - (ulong)imageOffset, image,
                metadata: new object[] { elfFileHeader, programHeader });
        }

        public ImmutableList<object> LoadMetadata()
        {
            var metadata = new List<object>();

            if (!stream.CanRead)
                throw new ArgumentException("Stream is not available for reading", nameof(stream));
            if (!stream.CanSeek)
                throw new ArgumentException("Stream is not available for seeking", nameof(stream));

            var elfFileHeader = new Header64();
            elfFileHeader.Read(stream);
            metadata.Add(elfFileHeader);

            if (elfFileHeader.E_PHNUM > 0)
            {
                for (var i = 0L; i < elfFileHeader.E_PHNUM; i++)
                {
                    var phOffset = (long)elfFileHeader.E_PHOFF + (i * elfFileHeader.E_PHENTSIZE);
                    stream.Seek(phOffset, SeekOrigin.Begin);
                    var programHeader = new ProgramHeader64();
                    programHeader.Read(stream);
                    metadata.Add(programHeader);
                }
            }

            if (elfFileHeader.E_SHNUM > 0)
            {
                for (var i = 0L; i < elfFileHeader.E_SHNUM; i++)
                {
                    var shOffset = (long)elfFileHeader.E_SHOFF + (i * elfFileHeader.E_SHENTSIZE);
                    stream.Seek(shOffset, SeekOrigin.Begin);
                    var sectionHeader = new SectionHeader64();
                    sectionHeader.Read(stream);
                    metadata.Add(sectionHeader);
                }
            }

            return metadata.ToImmutableList();
        }

        ILoaderResult ILoader.LoadImage() => this.LoadImage();
    }
}