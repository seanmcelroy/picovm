using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using picovm.VM;

namespace picovm.Packager.Elf.Elf64
{
    public sealed class LoaderElf64 : ILoader<UInt64>
    {
        private readonly Stream stream;

        public LoaderElf64(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);

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
            if (elfFileHeader.EI_CLASS != HeaderIdentityClass.ELFCLASS64)
                throw new BadImageFormatException("Not an ELFCLASS64 file");
            if (elfFileHeader.E_TYPE != HeaderType.ET_EXEC)
                throw new BadImageFormatException("Not an executable file");

            // If there are multiple PT_LOAD segments (rodata split from text, etc.), the "first" one isn't necessarily the one holding the entry point.
            ProgramHeader64? loadSegment = null;
            for (var i = 0; i < elfFileHeader.E_PHNUM; i++)
            {
                stream.Seek((long)elfFileHeader.E_PHOFF + i * elfFileHeader.E_PHENTSIZE, SeekOrigin.Begin);
                var ph = new ProgramHeader64();
                ph.Read(stream);
                if (ph.P_TYPE == ProgramHeaderType.PT_LOAD
                    && elfFileHeader.E_ENTRY >= ph.P_VADDR
                    && elfFileHeader.E_ENTRY < ph.P_VADDR + ph.P_MEMSZ)
                {
                    loadSegment = ph;
                    break;
                }
            }

            if (loadSegment is null)
                throw new InvalidDataException("ELF file has no PT_LOAD segment");

            var programHeader = loadSegment.Value;
            if (programHeader.P_ALIGN > 1)
            {
                if ((programHeader.P_ALIGN & (programHeader.P_ALIGN - 1)) != 0)
                    throw new BadImageFormatException($"P_ALIGN ({programHeader.P_ALIGN}) is not a power of two");
                if (programHeader.P_VADDR % programHeader.P_ALIGN != programHeader.P_OFFSET % programHeader.P_ALIGN)
                    throw new BadImageFormatException($"P_VADDR/P_OFFSET not congruent modulo P_ALIGN ({programHeader.P_ALIGN})");
            }

            var image = new byte[programHeader.P_MEMSZ];
            stream.Seek((long)programHeader.P_OFFSET, SeekOrigin.Begin);
            stream.ReadExactly(image.AsSpan(0, (int)programHeader.P_FILESZ));

            var entryOffset = elfFileHeader.E_ENTRY - programHeader.P_VADDR;
            return new LoaderResult64(entryOffset, image, metadata: [elfFileHeader, programHeader]);
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
            if (elfFileHeader.EI_CLASS != HeaderIdentityClass.ELFCLASS64)
                throw new BadImageFormatException("Not an ELFCLASS64 file");

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

            return [.. metadata];
        }

        ILoaderResult<UInt64> ILoader<UInt64>.LoadImage() => LoadImage();
    }
}