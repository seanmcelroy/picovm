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
            Xunit.Assert.Equal(52 - 4, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Header32)));
            Xunit.Assert.Equal(32, System.Runtime.InteropServices.Marshal.SizeOf(typeof(ProgramHeader32)));
            Xunit.Assert.Equal(40, System.Runtime.InteropServices.Marshal.SizeOf(typeof(SectionHeader32)));
        }

        [Fact]
        public void ReadKeyboardAsm32()
        {
            // http://www.sco.com/developers/devspecs/gabi41.pdf
            var compiler = new BytecodeCompiler<UInt32>();
            var sourceFileName = "./../../../../picovm/asm-src/read-keyboard32.asm";
            Xunit.Assert.True(File.Exists(Path.Combine(System.Environment.CurrentDirectory, sourceFileName)), $"Cannot find file {sourceFileName} for test, current directory: {System.Environment.CurrentDirectory}");
            var compilationInterface = compiler.Compile(sourceFileName);
            Xunit.Assert.Equal(0, compilationInterface.Errors.Count);
            Xunit.Assert.IsType<CompilationResult32>(compilationInterface);
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
                var ph = packager.GenerateProgramHeader32();
                ph.Write(ms);

                ms.Seek(0, SeekOrigin.Begin);
                var ph2 = new ProgramHeader32();
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

                var loader = new LoaderElf32(ms);
                var compilation2 = loader.LoadImage();
                Xunit.Assert.NotNull(compilation2);
                Xunit.Assert.Equal(compilation.EntryPoint.Value, compilation2.EntryPoint);
                Xunit.Assert.Equal(AssemblerPackageOutputType.Elf32, Packager.Inspector.DetectPackageOutputType(ms));
            }
        }

    }
}
