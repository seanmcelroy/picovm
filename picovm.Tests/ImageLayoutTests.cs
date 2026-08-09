using System;
using System.IO;
using System.Linq;
using picovm.Assembler;
using picovm.Packager.Elf.Elf32;
using picovm.Packager.Elf.Elf64;
using picovm.Tests.Support;
using Xunit;

namespace picovm.Tests
{
    /// <summary>
    /// Guards the assumption every data-segment test rests on: that the flat image
    /// <see cref="MovTestHarness"/> builds is the same image the real ELF packager and loader
    /// produce.  Without this the whole MOV suite could be testing a memory layout that only
    /// exists in the test harness.
    /// </summary>
    public class ImageLayoutTests
    {
        private static readonly string[] Program =
            Asm.WithData(
                ["counter db 0, 0, 0, 0, 0, 0, 0, 0"],
                "MOV ECX, counter",
                "MOV EAX, [ECX]");

        private static readonly string[] Program64 =
            Asm.WithData(
                ["counter db 0, 0, 0, 0, 0, 0, 0, 0"],
                "MOV RCX, counter",
                "MOV RAX, [RCX]");

        [Fact]
        public void HarnessImage_PlacesDataImmediatelyAfterText()
        {
            var compilation = MovTestHarness.Compile32(Program);
            var image = MovTestHarness.BuildImage(compilation);

            Assert.Equal(compilation.TextSegmentSize + compilation.DataSegmentSize, (uint)image.Length);
            Assert.Equal(compilation.TextSegment!.Value, image.Take((int)compilation.TextSegmentSize!.Value));
            Assert.Equal(compilation.DataSegment!.Value, image.Skip((int)compilation.TextSegmentSize!.Value));
        }

        [Fact]
        public void HarnessImage_PutsDataSymbolWhereTheCompilerRelocatedIt()
        {
            var compilation = MovTestHarness.Compile32(
                Asm.WithData(["marker db 0x41, 0x42, 0x43, 0x44"], "MOV ECX, marker"));
            var image = MovTestHarness.BuildImage(compilation);

            var address = MovTestHarness.DataSymbolAddress(compilation, "marker");

            // The relocated address must land on the actual bytes, not on padding.
            Assert.Equal(new byte[] { 0x41, 0x42, 0x43, 0x44 }, image.Skip((int)address).Take(4));
        }

        [Fact]
        public void HarnessImage_MatchesElf32LoaderImage()
        {
            var compilation = MovTestHarness.Compile32(Program);
            var harnessImage = MovTestHarness.BuildImage(compilation);

            var ms = new MemoryStream();
            new PackagerElf32(compilation).Write(ms);
            ms.Seek(0, SeekOrigin.Begin);
            var loaded = new LoaderElf32(ms).LoadImage();

            AssertImagesAgree(harnessImage, [.. loaded.Image]);
            Assert.Equal(compilation.EntryPoint!.Value, loaded.EntryPoint);
        }

        [Fact]
        public void HarnessImage_MatchesElf64LoaderImage()
        {
            var compilation = MovTestHarness.Compile64(Program64);
            var harnessImage = MovTestHarness.BuildImage(compilation);

            var ms = new MemoryStream();
            new PackagerElf64(compilation).Write(ms);
            ms.Seek(0, SeekOrigin.Begin);
            var loaded = new LoaderElf64(ms).LoadImage();

            AssertImagesAgree(harnessImage, [.. loaded.Image]);
            Assert.Equal(compilation.EntryPoint!.Value, loaded.EntryPoint);
        }

        /// <summary>
        /// The loader's image carries the packager's trailing 16-byte alignment padding after
        /// <c>.rodata</c>.  That padding is addressable-but-zero memory, so it is allowed to
        /// differ in length from the harness image as long as every meaningful byte agrees.
        /// </summary>
        private static void AssertImagesAgree(byte[] harnessImage, byte[] loaderImage)
        {
            Assert.True(loaderImage.Length >= harnessImage.Length,
                $"Loader image ({loaderImage.Length} bytes) is shorter than the harness image ({harnessImage.Length} bytes).");
            Assert.Equal(harnessImage, loaderImage.Take(harnessImage.Length));
            Assert.All(loaderImage.Skip(harnessImage.Length), b => Assert.Equal(0, b));
        }
    }
}
