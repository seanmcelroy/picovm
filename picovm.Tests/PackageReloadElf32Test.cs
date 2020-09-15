using picovm.Assembler;
using picovm.Packager.Elf.Elf32;
using System.IO;
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
    }
}
