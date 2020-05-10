using System;
using System.Linq;

namespace agent_playground
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
                return -3;
            }

            // Compile.
            CompilationResult compilation;
            {
                Console.Out.WriteLine($"Compiling source file: {args[0]}");
                var compiler = new BytecodeCompiler();
                var fileName = (new System.IO.FileInfo(args[0])).Name;
                compilation = compiler.Compile(fileName, programText);
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
            var image = new byte[compilation.textSegmentSize + compilation.dataSegmentSize];
            Array.Copy(compilation.textSegment, 0, image, compilation.textSegmentBase, compilation.textSegmentSize);
            Array.Copy(compilation.dataSegment, 0, image, compilation.dataSegmentBase, compilation.dataSegmentSize);

            var agent = new Agent(image);
            int? ret;
            do
            {
                ret = agent.Tick();
                //agent.Dump();
            } while (ret == null);
            switch (ret)
            {
                case Agent.E_INVALID:
                    throw new Exception($"ERROR: Unknown bytecode!");
                case 0:
                    Console.WriteLine("Program terminated.");
                    break;
            }

            return 0;
        }
    }
}
