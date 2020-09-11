using System;
using System.IO;
using picovm.Packager;

namespace picovm.Compiler
{
    public sealed class PackagerAOut32 : IPackager
    {
        private readonly CompilationResult compilationResult;
        public PackagerAOut32(CompilationResult compilationResult)
        {
            if (compilationResult.EntryPoint == null)
                throw new ArgumentException("Compilation result is missing an entry point", nameof(compilationResult));
            if (compilationResult.TextSegmentSize == null)
                throw new ArgumentException("Compilation result is missing a text segment size", nameof(compilationResult));
            if (compilationResult.DataSegmentSize == null)
                throw new ArgumentException("Compilation result is missing a data segment size", nameof(compilationResult));
            if (compilationResult.BssSegmentSize == null)
                throw new ArgumentException("Compilation result is missing a BSS segment size", nameof(compilationResult));
            if (compilationResult.EntryPoint == null)
                throw new ArgumentException("Compilation result is missing an entry point", nameof(compilationResult));

            this.compilationResult = compilationResult;
        }

        public void Write(Stream stream)
        {
            // Mandatory exec header
            GenerateAOut32ExecHeader(stream);
        }

        private void GenerateAOut32ExecHeader(Stream fileStream)
        {
            // https://wiki.osdev.org/A.out

            // a_midmag 0-3
            fileStream.Write(BitConverter.GetBytes((uint)0));
            // a_text 4-7 Contains the size of the text segment in bytes. 
            fileStream.Write(BitConverter.GetBytes((uint)this.compilationResult.TextSegmentSize!.Value));
            // a_data 8-11 Contains the size of the data segment in bytes. 
            fileStream.Write(BitConverter.GetBytes((uint)this.compilationResult.DataSegmentSize!.Value));
            // a_bss 12-15 Contains the number of bytes in the `bss segment'. The kernel loads the program so that this amount of writable memory appears to follow the data segment and initially reads as zeroes. 
            fileStream.Write(BitConverter.GetBytes((uint)this.compilationResult.BssSegmentSize!.Value));
            // a_syms 16-19 Contains the size in bytes of the symbol table section. 
            fileStream.Write(BitConverter.GetBytes((uint)0)); // TODO.  Right now we don't output debugging symbols
            // a_entry 20-23 Contains the address in memory of the entry point of the program after the kernel has loaded it; the kernel starts the execution of the program from the machine instruction at this address. 
            fileStream.Write(BitConverter.GetBytes((uint)this.compilationResult.EntryPoint!.Value));
            // a_trsize 24-27 Contains the size in bytes of the text relocation table.
            fileStream.Write(BitConverter.GetBytes((uint)0)); // TODO?
            // a_drsize 28-31 Contains the size in bytes of the data relocation table. 
            fileStream.Write(BitConverter.GetBytes((uint)0)); // TODO?
        }
    }
}