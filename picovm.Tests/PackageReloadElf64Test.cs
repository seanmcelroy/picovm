using picovm.Assembler;
using picovm.Packager.Elf.Elf64;
using System;
using System.IO;
using Xunit;

namespace picovm.Tests
{
    public class PackageReloadElf64Test
    {
        [Fact]
        public void StructSizes()
        {
            Xunit.Assert.Equal(64, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Header64)));
            Xunit.Assert.Equal(56, System.Runtime.InteropServices.Marshal.SizeOf(typeof(ProgramHeader64)));
            Xunit.Assert.Equal(64, System.Runtime.InteropServices.Marshal.SizeOf(typeof(SectionHeader64)));
        }

        [Fact]
        public void ReadKeyboardAsm64()
        {
            // http://www.sco.com/developers/devspecs/gabi41.pdf
            var compiler = new BytecodeCompiler<UInt64>();
            var sourceFileName = "./../../../../picovm/asm-src/read-keyboard64.asm";
            Xunit.Assert.True(File.Exists(Path.Combine(System.Environment.CurrentDirectory, sourceFileName)), $"Cannot find file {sourceFileName} for test, current directory: {System.Environment.CurrentDirectory}");
            var compilationInterface = compiler.Compile(sourceFileName);
            Xunit.Assert.Equal(0, compilationInterface.Errors.Count);
            Xunit.Assert.IsType<CompilationResult64>(compilationInterface);
            var compilation = (CompilationResult64)compilationInterface;

            var packager = new PackagerElf64(compilation);

            // Test ELF file header write/read/compare fidelity
            var header = packager.GenerateElfFileHeader();
            {
                var ms = new MemoryStream();
                header.Write(ms, 1, 1);

                ms.Seek(0, SeekOrigin.Begin);
                var header2 = new Header64();
                header2.Read(ms);

                Xunit.Assert.Equal(header.EI_CLASS, header2.EI_CLASS);
                Xunit.Assert.Equal(header.EI_DATA, header2.EI_DATA);
                Xunit.Assert.Equal(header.EI_VERSION, header2.EI_VERSION);
                Xunit.Assert.Equal(header.E_TYPE, header2.E_TYPE);
                Xunit.Assert.Equal(header.E_MACHINE, header2.E_MACHINE);
                Xunit.Assert.Equal(header.E_VERSION, header2.E_VERSION);
                Xunit.Assert.Equal(header.E_ENTRY, header2.E_ENTRY);
                Xunit.Assert.Equal(header.E_PHOFF, header2.E_PHOFF);
                Xunit.Assert.Equal(header.E_SHOFF, header2.E_SHOFF);
                Xunit.Assert.Equal(header.E_FLAGS, header2.E_FLAGS);
                Xunit.Assert.Equal(header.E_EHSIZE, header2.E_EHSIZE);
                Xunit.Assert.Equal(header.E_PHENTSIZE, header2.E_PHENTSIZE);
                Xunit.Assert.Equal(header.E_PHNUM, header2.E_PHNUM);
                Xunit.Assert.Equal(header.E_SHENTSIZE, header2.E_SHENTSIZE);
                Xunit.Assert.Equal(header.E_SHNUM, header2.E_SHNUM);
                Xunit.Assert.Equal(header.E_SHSTRNDX, header2.E_SHSTRNDX);
            }

            // Test program header write/read/compare fidelity
            {
                var ms = new MemoryStream();
                var ph = packager.GenerateProgramHeader64();
                ph.Write(ms);

                ms.Seek(0, SeekOrigin.Begin);
                var ph2 = new ProgramHeader64();
                ph2.Read(ms);

                Xunit.Assert.Equal(ph.P_TYPE, ph2.P_TYPE);
                Xunit.Assert.Equal(ph.P_OFFSET, ph2.P_OFFSET);
                Xunit.Assert.Equal(ph.P_VADDR, ph2.P_VADDR);
                Xunit.Assert.Equal(ph.P_PADDR, ph2.P_PADDR);
                Xunit.Assert.Equal(ph.P_FILESZ, ph2.P_FILESZ);
                Xunit.Assert.Equal(ph.P_MEMSZ, ph2.P_MEMSZ);
                Xunit.Assert.Equal(ph.P_FLAGS, ph2.P_FLAGS);
                Xunit.Assert.Equal(ph.P_ALIGN, ph2.P_ALIGN);
            }

            // Test full package/load/compare fidelity
            {
                var ms = new MemoryStream();
                packager.Write(ms);

                ms.Seek(0, SeekOrigin.Begin);

                var loader = new LoaderElf64(ms);
                var compilation2 = loader.LoadImage();
                Xunit.Assert.NotNull(compilation2);
                Xunit.Assert.Equal(compilation.EntryPoint.Value, compilation2.EntryPoint);
                Xunit.Assert.Equal(AssemblerPackageOutputType.Elf64, Packager.Inspector.DetectPackageOutputType(ms));
            }
        }
    }
}
