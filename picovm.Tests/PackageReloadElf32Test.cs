using System;
using System.IO;
using picovm.Assembler;
using picovm.Packager.Elf.Elf32;
using Xunit;

namespace picovm.Tests
{
    public class PackageReloadElf32Test
    {
        [Fact]
        public void StructSizes()
        {
            Assert.Equal(52 - 4, System.Runtime.InteropServices.Marshal.SizeOf<Header32>());
            Assert.Equal(32, System.Runtime.InteropServices.Marshal.SizeOf<ProgramHeader32>());
            Assert.Equal(40, System.Runtime.InteropServices.Marshal.SizeOf<SectionHeader32>());
        }

        [Fact]
        public void ReadKeyboardAsm32()
        {
            // http://www.sco.com/developers/devspecs/gabi41.pdf
            var compiler = new BytecodeCompiler<UInt32>();
            var sourceFileName = "./../../../../picovm/asm-src/read-keyboard32.asm";
            Assert.True(File.Exists(Path.Combine(Environment.CurrentDirectory, sourceFileName)), $"Cannot find file {sourceFileName} for test, current directory: {Environment.CurrentDirectory}");
            var compilationInterface = compiler.Compile(sourceFileName);
            Assert.Empty(compilationInterface.Errors);
            Assert.IsType<CompilationResult32>(compilationInterface);
            var compilation = (CompilationResult32)compilationInterface;

            var packager = new PackagerElf32(compilation);

            // Test ELF file header write/read/compare fidelity
            var header = packager.GenerateElfFileHeader();
            {
                var ms = new MemoryStream();
                header.Write(ms, 1, 1);

                ms.Seek(0, SeekOrigin.Begin);
                var header2 = new Header32();
                header2.Read(ms);

                Assert.Equal(header.EI_CLASS, header2.EI_CLASS);
                Assert.Equal(header.EI_DATA, header2.EI_DATA);
                Assert.Equal(header.EI_VERSION, header2.EI_VERSION);
                Assert.Equal(header.E_TYPE, header2.E_TYPE);
                Assert.Equal(header.E_MACHINE, header2.E_MACHINE);
                Assert.Equal(header.E_VERSION, header2.E_VERSION);
                Assert.Equal(header.E_ENTRY, header2.E_ENTRY);
                Assert.Equal(header.E_PHOFF, header2.E_PHOFF);
                Assert.Equal(header.E_SHOFF, header2.E_SHOFF);
                Assert.Equal(header.E_FLAGS, header2.E_FLAGS);
                Assert.Equal(header.E_EHSIZE, header2.E_EHSIZE);
                Assert.Equal(header.E_PHENTSIZE, header2.E_PHENTSIZE);
                Assert.Equal(header.E_PHNUM, header2.E_PHNUM);
                Assert.Equal(header.E_SHENTSIZE, header2.E_SHENTSIZE);
                Assert.Equal(header.E_SHNUM, header2.E_SHNUM);
                Assert.Equal(header.E_SHSTRNDX, header2.E_SHSTRNDX);
            }

            // Test program header write/read/compare fidelity
            {
                var ms = new MemoryStream();
                var ph = packager.GenerateProgramHeader32();
                ph.Write(ms);

                ms.Seek(0, SeekOrigin.Begin);
                var ph2 = new ProgramHeader32();
                ph2.Read(ms);

                Assert.Equal(ph.P_TYPE, ph2.P_TYPE);
                Assert.Equal(ph.P_OFFSET, ph2.P_OFFSET);
                Assert.Equal(ph.P_VADDR, ph2.P_VADDR);
                Assert.Equal(ph.P_PADDR, ph2.P_PADDR);
                Assert.Equal(ph.P_FILESZ, ph2.P_FILESZ);
                Assert.Equal(ph.P_MEMSZ, ph2.P_MEMSZ);
                Assert.Equal(ph.P_FLAGS, ph2.P_FLAGS);
                Assert.Equal(ph.P_ALIGN, ph2.P_ALIGN);
            }

            // Test full package/load/compare fidelity
            {
                var ms = new MemoryStream();
                packager.Write(ms);

                ms.Seek(0, SeekOrigin.Begin);

                var loader = new LoaderElf32(ms);
                var compilation2 = loader.LoadImage();
                Assert.NotNull(compilation2);
                Assert.Equal(compilation.EntryPoint.Value, compilation2.EntryPoint);
                Assert.Equal(AssemblerPackageOutputType.Elf32, Packager.Inspector.DetectPackageOutputType(ms));
            }
        }

    }
}
