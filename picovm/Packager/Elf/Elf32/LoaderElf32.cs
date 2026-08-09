using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using picovm.VM;

namespace picovm.Packager.Elf.Elf32
{
    public sealed class LoaderElf32 : ILoader
    {
        private readonly Stream stream;

        public LoaderElf32(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);

            this.stream = stream;
        }

        public LoaderResult32 LoadImage()
        {
            if (!stream.CanRead)
                throw new ArgumentException("Stream is not available for reading", nameof(stream));
            if (!stream.CanSeek)
                throw new ArgumentException("Stream is not available for seeking", nameof(stream));

            var elfFileHeader = new Header32();
            elfFileHeader.Read(stream);

            stream.Seek((long)elfFileHeader.E_PHOFF, SeekOrigin.Begin);
            var programHeader = new ProgramHeader32();
            programHeader.Read(stream);

            // The headers are written 16-byte aligned, so the image begins past their
            // padded sizes -- not past E_EHSIZE/E_PHENTSIZE, which report the unpadded
            // lengths.  Sizing the image off the unpadded values over-reads the segment
            // by the padding and drags in bytes belonging to the section name table.
            UInt32 imageOffset =
                elfFileHeader.E_EHSIZE
                + (UInt32)elfFileHeader.E_EHSIZE.CalculateRoundUpTo16Pad()
                + (UInt32)(elfFileHeader.E_PHNUM * (elfFileHeader.E_PHENTSIZE + elfFileHeader.E_PHENTSIZE.CalculateRoundUpTo16Pad()));
            var image = new byte[programHeader.P_FILESZ - imageOffset];
            stream.Seek(imageOffset, SeekOrigin.Begin);
            stream.ReadExactly(image);

            return new LoaderResult32(elfFileHeader.E_ENTRY - imageOffset, image,
                metadata: [elfFileHeader, programHeader]);
        }

        public ImmutableList<object> LoadMetadata()
        {
            var metadata = new List<object>();

            if (!stream.CanRead)
                throw new ArgumentException("Stream is not available for reading", nameof(stream));
            if (!stream.CanSeek)
                throw new ArgumentException("Stream is not available for seeking", nameof(stream));

            var elfFileHeader = new Header32();
            elfFileHeader.Read(stream);
            metadata.Add(elfFileHeader);

            stream.Seek((long)elfFileHeader.E_PHOFF, SeekOrigin.Begin);
            var programHeader = new ProgramHeader32();
            programHeader.Read(stream);
            metadata.Add(programHeader);

            return metadata.ToImmutableList();
        }

        ILoaderResult ILoader.LoadImage() => LoadImage();
    }
}