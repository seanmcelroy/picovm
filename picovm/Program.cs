using System;
using picovm.Compiler;
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
            Console.WriteLine(" Usage: picovm <src.asm> <a.out>");

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

                if (compilation.errors.Count > 0)
                {
                    Console.Error.WriteLine($"Compilation failed with {compilation.errors.Count} errors.");
                    return -6;
                }
            }

            // Package.
            {
                var packageFormat = CompilerOutputType.AOut32;
                Console.Out.WriteLine($"Packaging bytecode as: {Enum.GetName(typeof(CompilerOutputType), packageFormat)}");
                var fileName = args[1];
                switch (packageFormat)
                {
                    case CompilerOutputType.AOut32:
                        var packager = new PackagerAOut32(compilation);
                        packager.WriteFile(fileName);
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown packaging format: {packageFormat}");
                }
            }

            // Loader
            var image = new byte[compilation.textSegmentSize + compilation.dataSegmentSize + compilation.bssSegmentSize];
            Array.Copy(compilation.textSegment, 0, image, compilation.textSegmentBase, compilation.textSegmentSize);
            if (compilation.dataSegmentSize > 0)
                Array.Copy(compilation.dataSegment, 0, image, compilation.dataSegmentBase, compilation.dataSegmentSize);

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
