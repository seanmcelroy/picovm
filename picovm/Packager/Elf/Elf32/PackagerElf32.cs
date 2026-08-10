using System;
using System.Collections.Generic;
using System.IO;
using picovm.Assembler;

namespace picovm.Packager.Elf.Elf32
{
    public sealed class PackagerElf32 : IPackager
    {
        private readonly CompilationResult<UInt32> compilationResult;

        public PackagerElf32(CompilationResult<UInt32> compilationResult)
        {
            if (compilationResult.EntryPoint == null)
                throw new ArgumentException("Compilation result is missing an entry point", nameof(compilationResult));
            if (compilationResult.TextSegmentSize == null)
                throw new ArgumentException("Compilation result is missing a text segment size", nameof(compilationResult));
            if (compilationResult.DataSegmentSize == null)
                throw new ArgumentException("Compilation result is missing a data segment size", nameof(compilationResult));
            this.compilationResult = compilationResult;
        }

        public Header32 GenerateElfFileHeader() => new()
        {
            EI_CLASS = HeaderIdentityClass.ELFCLASS32,
            EI_DATA = HeaderIdentityData.ELFDATA2LSB,
            EI_VERSION = HeaderIdentityVersion.EI_CURRENT,
            E_TYPE = HeaderType.ET_EXEC,
            E_MACHINE = HeaderMachine.EM_ARM, // EM_ARM = 0x28 TODO: What should this be?
            E_VERSION = HeaderVersion.EV_CURRENT,
            E_ENTRY = compilationResult.EntryPoint!.Value + 0x60, // ELF header + Program Table Header = 0x60
            E_PHOFF = 0x40, // We always start the program header at 64 bytes, b/c the header will vary 52 vs 64 bytes in length if it's 32-bit or 64-bit.
            E_SHOFF = 0,
            E_FLAGS = 0,
            E_EHSIZE = 52,
            E_PHENTSIZE = 32,
            E_SHENTSIZE = 40,
            E_SHSTRNDX = SpecialSectionIndexes.SHN_UNDEF
        };

        public static ProgramHeader32 GenerateProgramHeader32() => new()
        {
            P_TYPE = ProgramHeaderType.PT_LOAD, // TODO: always?
            //P_VADDR = 0x8000000, // TODO: always?
            //P_PADDR = 0x8000000, // TODO: always?
            P_FLAGS = (uint)(SegmentPermissionFlags.PF_R | SegmentPermissionFlags.PF_X),
            P_ALIGN = 16, // matches the 16-byte padding used throughout the packager
            // P_OFFSET, P_FILESZ, P_MEMSZ are assigned in Write() once the layout is known.
        };

        public void Write(Stream stream)
        {
            // Mandatory ELF header

            // https://upload.wikimedia.org/wikipedia/commons/e/e4/ELF_Executable_and_Linkable_Format_diagram_by_Ange_Albertini.png

            var elfFileHeader = GenerateElfFileHeader();
            uint elfFileHeaderSize = 0x40;

            var programHeader = GenerateProgramHeader32();

            // And here... we... go.
            uint programHeaderOffset = elfFileHeaderSize; // Always start at 64 bytes in.
            var (msProgramHeader, programHeaderSizeReal, programHeaderSizePad) = programHeader.ToMemoryStream();
            uint programHeaderSize = programHeaderSizeReal + (uint)programHeaderSizePad;

            // .text
            uint textOffset = programHeaderOffset + programHeaderSize;
            uint textSizeReal = (uint)(compilationResult.TextSegment?.Length ?? 0);
            // No alignment padding between .text and .rodata.  The compiler has already
            // baked data symbol addresses into the text segment using
            // dataSegmentBase = textSegmentBase + textSegmentSize (see BytecodeCompiler.Compile),
            // so padding here would move the data without moving the relocations that
            // point at it, leaving every data symbol aimed at the padding instead.
            int textSizePad = 0;
            uint textSize = textSizeReal;

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
                bwData.BaseStream.WriteZeros(rodataSizePad);
                bwData.Flush();
            }

            // Section names
            uint sectionNamesOffset = rodataOffset + rodataSize;
            uint sectionNamesSizeReal;
            int sectionNamesSizePad;

            // .shrtrtab and section header table
            var msSectionNames = new MemoryStream();
            var sections = new List<SectionHeader32>();
            {
                var bwSectionNames = new BinaryWriter(msSectionNames);
                bwSectionNames.Write('\0');
                bwSectionNames.Write(System.Text.Encoding.ASCII.GetBytes(".shstrtab\0"));

                // Required Index 0
                sections.Add(new SectionHeader32
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
                sections.Add(new SectionHeader32
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
                sections.Add(new SectionHeader32
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
                sections.Add(new SectionHeader32
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
                bwSectionNames.BaseStream.WriteZeros(sectionNamesSizePad);
                bwSectionNames.Flush();
            }
            uint sectionNamesSize = sectionNamesSizeReal + (uint)sectionNamesSizePad;


            // Section header table
            uint sectionHeaderTableOffset = sectionNamesOffset + sectionNamesSize;

            // E_ENTRY is a virtual address, not a file offset.
            // EntryPoint is text-segment-relative (see BytecodeCompiler.cs:185).
            elfFileHeader.E_ENTRY = programHeader.P_VADDR + (compilationResult.EntryPoint ?? 0);
            elfFileHeader.E_PHOFF = programHeaderOffset;
            elfFileHeader.E_SHOFF = sectionHeaderTableOffset;
            elfFileHeader.E_SHSTRNDX = sections.Count == 0 ? SpecialSectionIndexes.SHN_UNDEF : (ushort)(sections.Count - 1);
            elfFileHeader.Write(stream, 1, (ushort)sections.Count);

            // PT_LOAD covers just the payload (.text + .rodata), starting at its true file offset.
            programHeader.P_OFFSET = textOffset;
            programHeader.P_FILESZ = textSize + rodataSize;
            programHeader.P_MEMSZ  = programHeader.P_FILESZ;
            (msProgramHeader, programHeaderSizeReal, programHeaderSizePad) = programHeader.ToMemoryStream();

            stream.Write(msProgramHeader.ToArray());
            if (compilationResult.TextSegment != null)
            {
                stream.Write(compilationResult.TextSegment.Value.AsSpan());
                stream.WriteZeros(textSizePad);
            }
            if (compilationResult.DataSegment != null)
                stream.Write(msData.ToArray());
            stream.Write(msSectionNames.ToArray());

            // Write out section header table
            foreach (var section in sections)
                section.Write(stream, elfFileHeader.EI_CLASS);

            stream.Flush();
        }
    }
}