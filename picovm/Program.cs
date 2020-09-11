using System;
using System.IO;
using System.Linq;
using picovm.Compiler;
using picovm.Packager;
using picovm.Packager.Elf64;
using picovm.VM;

namespace picovm
{
    class Program
    {
        static int Main(string[] args)
        {
            Console.WriteLine("PicoVM - A tiny toy virtual machine");
            Console.WriteLine(" (c) 2020 Sean McElroy");
            Console.WriteLine(" Released under the MIT License; all rights reserved.");
            Console.WriteLine();
            Console.WriteLine(" Usage: picovm <src.asm> <a.elf>");

            if (args.Length == 0)
            {
                Console.Error.WriteLine("No source input file provided.");
                return -1;
            }

            if (!System.IO.File.Exists(args[0]))
            {
                Console.Error.WriteLine($"Source input file {args[0]} not found.");
                return -2;
            }

            if (args.Length == 1)
            {
                Console.Error.WriteLine("No destination output file provided.");
                return -3;
            }

            if (System.IO.File.Exists(args[1]))
            {
                Console.Error.WriteLine($"Executable output file {args[0]} already exists.");
                // DEBUG
                System.IO.File.Delete(args[1]);
                //return -4;
            }

            string[] programText;
            try
            {
                programText = System.IO.File.ReadAllLines(args[0]);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error while attempting to read input file: {ex.Message}");
                return -5;
            }

            // Compile.
            CompilationResult compilation;
            {
                Console.Out.WriteLine($"Compiling source file: {args[0]}");
                var compiler = new BytecodeCompiler();
                var fileName = (new System.IO.FileInfo(args[0])).Name;
                compilation = compiler.Compile(fileName, programText);

                if (compilation.Errors.Count > 0)
                {
                    Console.Error.WriteLine($"Compilation failed with {compilation.Errors.Count} errors.");
                    return -6;
                }
            }

            // Package.
            {
                var packageFormat = CompilerOutputType.Elf64;
                Console.Out.WriteLine($"Packaging bytecode as: {Enum.GetName(typeof(CompilerOutputType), packageFormat)}");
                IPackager packager;

                using (var fs = new FileStream(args[1], FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.SequentialScan))
                {
                    switch (packageFormat)
                    {
                        case CompilerOutputType.AOut32:
                            packager = new PackagerAOut32(compilation);
                            packager.Write(fs);
                            break;
                        case CompilerOutputType.Elf64:
                            packager = new PackagerElf64(compilation);
                            packager.Write(fs);
                            break;
                        default:
                            throw new InvalidOperationException($"Unknown packaging format: {packageFormat}");
                    }

                    fs.Flush();
                    fs.Close();
                }

            }

            // Loader
            var image = new byte[compilation.TextSegmentSize!.Value + compilation.DataSegmentSize!.Value + compilation.BssSegmentSize!.Value];
            Array.Copy(compilation.TextSegment!, 0, image, compilation.TextSegmentBase!.Value, compilation.TextSegmentSize.Value);
            if (compilation.DataSegmentSize > 0 && compilation.DataSegment != null)
                Array.Copy(compilation.DataSegment.Value.ToArray(), 0, image, (int)compilation.DataSegmentBase!.Value, (int)compilation.DataSegmentSize.Value);

            Console.Out.WriteLine("Emulating Linux 32-bit kernel syscall interface");
            var kernel = new Linux32Kernel();
            var agent = new Agent(kernel, image);
            int? ret;
            do
            {
                ret = agent.Tick();
                //agent.Dump();
            } while (ret == null);
            switch (ret)
            {
                case -666:
                    throw new Exception($"ERROR: Unknown bytecode!");
                case 0:
                    Console.WriteLine("\r\n\r\nProgram terminated.");
                    break;
            }

            return 0;
        }
    }
}
