using System;
using System.IO;
using picovm.VM;

namespace picovm.Packager.Elf64
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

        public LoaderResult Load()
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
            var imageOffset =
                elfFileHeader.E_EHSIZE
                + elfFileHeader.E_EHSIZE.CalculateRoundUpTo16Pad()
                + (elfFileHeader.E_PHNUM * (elfFileHeader.E_PHENTSIZE + elfFileHeader.E_PHENTSIZE.CalculateRoundUpTo16Pad()));
            stream.Seek(imageOffset, SeekOrigin.Begin);
            stream.Read(image, 0, image.Length);

            return new LoaderResult(elfFileHeader.E_ENTRY - (ulong)imageOffset, image);
        }
    }
}