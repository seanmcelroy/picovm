using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using picovm.Assembler;
using picovm.Packager.Elf;

namespace picovm.Packager.Elf.Elf64
{
    public sealed class PackagerElf64 : IPackager
    {
        private readonly CompilationResult64 compilationResult;

        private bool generateSectionHeaderTable { get; set; }

        public PackagerElf64(CompilationResult64 compilationResult, bool generateSectionHeaderTable = true)
        {
            if (compilationResult.EntryPoint == null)
                throw new ArgumentException("Compilation result is missing an entry point", nameof(compilationResult));
            if (compilationResult.TextSegmentSize == null)
                throw new ArgumentException("Compilation result is missing a text segment size", nameof(compilationResult));
            if (compilationResult.DataSegmentSize == null)
                throw new ArgumentException("Compilation result is missing a data segment size", nameof(compilationResult));
            this.compilationResult = compilationResult;
            this.generateSectionHeaderTable = generateSectionHeaderTable;
        }

        public Header64 GenerateElfFileHeader() => new Header64
        {
            EI_CLASS = HeaderIdentityClass.ELFCLASS64,
            EI_DATA = HeaderIdentityData.ELFDATA2LSB,
            EI_VERSION = HeaderIdentityVersion.EI_CURRENT,
            E_TYPE = HeaderType.ET_EXEC,
            E_MACHINE = HeaderMachine.EM_ARM, // EM_ARM = 0x28 TODO: What should this be?
            E_VERSION = HeaderVersion.EV_CURRENT,
            E_ENTRY = (ushort)(this.compilationResult.EntryPoint!.Value + 0x60), // ELF header + Program Table Header = 0x60
            E_PHOFF = 0x40, // We always start the program header at 64 bytes, b/c the header will vary 52 vs 64 bytes in length if it's 32-bit or 64-bit.
            E_SHOFF = 0,
            E_FLAGS = 0,
            E_EHSIZE = 64,
            E_PHENTSIZE = 56,
            E_SHENTSIZE = 64,
            E_SHSTRNDX = SpecialSectionIndexes.SHN_UNDEF
        };

        public ProgramHeader64 GenerateProgramHeader64() => new ProgramHeader64
        {
            P_TYPE = ProgramHeaderType.PT_LOAD, // TODO: always?
            P_OFFSET = 0, // TODO: always 0?
            //P_VADDR = 0x8000000, // TODO: always?
            //P_PADDR = 0x8000000, // TODO: always?
            P_FLAGS = (uint)(SegmentPermissionFlags.PF_R | SegmentPermissionFlags.PF_X),
            P_ALIGN = 0, // TODO: always?
        };

        public void Write(Stream stream)
        {
            // Mandatory ELF header

            // https://upload.wikimedia.org/wikipedia/commons/e/e4/ELF_Executable_and_Linkable_Format_diagram_by_Ange_Albertini.png

            var elfFileHeader = GenerateElfFileHeader();
            uint elfFileHeaderSize = 0x40;

            var programHeader = GenerateProgramHeader64();

            // And here... we... go.
            uint programHeaderOffset = elfFileHeaderSize; // Always start at 64 bytes in.
            var (msProgramHeader, programHeaderSizeReal, programHeaderSizePad) = programHeader.ToMemoryStream();
            uint programHeaderSize = programHeaderSizeReal + (uint)programHeaderSizePad;

            // .text
            uint textOffset = programHeaderOffset + programHeaderSize;
            uint textSizeReal = (uint)(compilationResult.TextSegment?.Length ?? 0);
            int textSizePad = textSizeReal.CalculateRoundUpTo16Pad();
            uint textSize = textSizeReal + (uint)textSizePad;

            // .rodata
            uint rodataOffset = textOffset + textSize;
            uint rodataSizeReal = (uint)(compilationResult.DataSegment?.Length ?? 0);
            int rodataSizePad = rodataSizeReal.CalculateRoundUpTo16Pad();
            uint rodataSize = rodataSizeReal + (uint)rodataSizePad;

            var msData = new MemoryStream();
            if (compilationResult.DataSegment != null)
            {
                var bwData = new BinaryWriter(msData);

                // Write out section header string table, align to 16 bytes
                bwData.Write(compilationResult.DataSegment.Value.AsSpan());
                bwData.Flush();
                bwData.Write(Enumerable.Repeat((byte)0x00, rodataSizePad).ToArray());
                bwData.Flush();
            }

            // Section names
            uint sectionNamesOffset = rodataOffset + rodataSize;
            uint sectionNamesSizeReal;
            int sectionNamesSizePad;

            // .shrtrtab and section header table
            var msSectionNames = new MemoryStream();
            var sections = new List<SectionHeader64>();
            {
                var bwSectionNames = new BinaryWriter(msSectionNames);
                bwSectionNames.Write('\0');
                bwSectionNames.Write(System.Text.Encoding.ASCII.GetBytes(".shrtrtab\0"));

                // Required Index 0
                sections.Add(new SectionHeader64
                {
                    SH_NAME = 0x0,
                    SH_TYPE = SectionHeaderType.SHT_NULL,
                    SH_FLAGS = 0,
                    SH_ADDR = 0,
                    SH_OFFSET = 0,
                    SH_SIZE = 0,
                    SH_LINK = SpecialSectionIndexes.SHN_UNDEF,
                    SH_INFO = 0,
                    SH_ADDRALIGN = 0,
                    SH_ENTSIZE = 0
                });

                // Code
                sections.Add(new SectionHeader64
                {
                    SH_NAME = (uint)msSectionNames.Position,
                    SH_TYPE = SectionHeaderType.SHT_PROGBITS,
                    SH_FLAGS = (uint)(SectionHeaderFlags.SHF_ALLOC | SectionHeaderFlags.SHF_EXECINSTR),
                    SH_ADDR = programHeader.P_VADDR + textOffset,
                    SH_OFFSET = textOffset,
                    SH_SIZE = textSizeReal
                });
                bwSectionNames.Write(System.Text.Encoding.ASCII.GetBytes(".text\0"));

                // Data
                sections.Add(new SectionHeader64
                {
                    SH_NAME = (uint)msSectionNames.Position,
                    SH_TYPE = SectionHeaderType.SHT_PROGBITS,
                    SH_FLAGS = (uint)SectionHeaderFlags.SHF_ALLOC,
                    SH_ADDR = programHeader.P_VADDR + rodataOffset,
                    SH_OFFSET = rodataOffset,
                    SH_SIZE = rodataSizeReal
                });
                bwSectionNames.Write(System.Text.Encoding.ASCII.GetBytes(".rodata\0"));
                bwSectionNames.Flush();

                sectionNamesSizeReal = (uint)msSectionNames.Position;
                sections.Add(new SectionHeader64
                {
                    SH_NAME = (uint)0x01, // We wrote this out first, after an initial \0, so it's always 0x01 in the string table for the section header
                    SH_TYPE = SectionHeaderType.SHT_STRTAB,
                    SH_FLAGS = 0,
                    SH_ADDR = 0,
                    SH_OFFSET = sectionNamesOffset,
                    SH_SIZE = sectionNamesSizeReal
                });

                // Write out section header string table, align to 16 bytes
                sectionNamesSizePad = sectionNamesSizeReal.CalculateRoundUpTo16Pad();
                bwSectionNames.Write(Enumerable.Repeat((byte)0x00, sectionNamesSizePad).ToArray());
                bwSectionNames.Flush();
            }
            uint sectionNamesSize = sectionNamesSizeReal + (uint)sectionNamesSizePad;


            // Section header table
            uint sectionHeaderTableOffset = sectionNamesOffset + sectionNamesSize;

            elfFileHeader.E_ENTRY = programHeader.P_VADDR + textOffset;
            elfFileHeader.E_PHOFF = programHeaderOffset;
            elfFileHeader.E_SHOFF = sectionHeaderTableOffset;
            elfFileHeader.E_SHSTRNDX = sections.Count == 0 ? SpecialSectionIndexes.SHN_UNDEF : (ushort)(sections.Count - 1);
            elfFileHeader.Write(stream, 1, (ushort)sections.Count);

            programHeader.P_FILESZ = sectionNamesOffset;
            programHeader.P_MEMSZ = sectionNamesOffset;
            (msProgramHeader, programHeaderSizeReal, programHeaderSizePad) = programHeader.ToMemoryStream();

            stream.Write(msProgramHeader.ToArray());
            if (compilationResult.TextSegment != null)
            {
                stream.Write(compilationResult.TextSegment.Value.AsSpan());
                stream.Write(Enumerable.Repeat((byte)0x00, textSizePad).ToArray());
            }
            if (compilationResult.DataSegment != null)
                stream.Write(msData.ToArray());
            stream.Write(msSectionNames.ToArray());

            // Write out section header table
            foreach (var section in sections)
                section.Write(stream, elfFileHeader.EI_CLASS);
        }
    }
}