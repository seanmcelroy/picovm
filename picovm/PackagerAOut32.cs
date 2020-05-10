using System;
using System.IO;

namespace agent_playground
{
    public sealed class PackagerAOut32
    {
        private readonly CompilationResult compilationResult;
        public PackagerAOut32(CompilationResult compilationResult)
        {
            this.compilationResult = compilationResult;
        }


        public void WriteFile(string filename)
        {
            using (var fs = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.SequentialScan))
            {
                // Mandatory exec header
                GenerateAOut32ExecHeader(fs);

                fs.Flush();
                fs.Close();
            }
        }

        private void GenerateAOut32ExecHeader(FileStream fileStream)
        {
            // https://wiki.osdev.org/A.out

            // a_midmag 0-3
            fileStream.Write(BitConverter.GetBytes((uint)0));
            // a_text 4-7 Contains the size of the text segment in bytes. 
            fileStream.Write(BitConverter.GetBytes((uint)this.compilationResult.textSegmentSize));
            // a_data 8-11 Contains the size of the data segment in bytes. 
            fileStream.Write(BitConverter.GetBytes((uint)this.compilationResult.dataSegmentSize));
            // a_bss 12-15 Contains the number of bytes in the `bss segment'. The kernel loads the program so that this amount of writable memory appears to follow the data segment and initially reads as zeroes. 
            fileStream.Write(BitConverter.GetBytes((uint)0)); // TODO?
            // a_syms 16-19 Contains the size in bytes of the symbol table section. 
            fileStream.Write(BitConverter.GetBytes((uint)0)); // TODO.  Right now we don't output debugging symbols
            // a_entry 20-23 Contains the address in memory of the entry point of the program after the kernel has loaded it; the kernel starts the execution of the program from the machine instruction at this address. 
            fileStream.Write(BitConverter.GetBytes((uint)this.compilationResult.entryPoint));
            // a_trsize 24-27 Contains the size in bytes of the text relocation table.
            fileStream.Write(BitConverter.GetBytes((uint)0)); // TODO?
            // a_drsize 28-31 Contains the size in bytes of the data relocation table. 
            fileStream.Write(BitConverter.GetBytes((uint)0)); // TODO?
        }
    }
}