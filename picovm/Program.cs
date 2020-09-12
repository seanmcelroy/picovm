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
            Console.WriteLine(" Usage: picovm <COMMAND> INPUTS... <src.asm> <a.elf>");
            Console.WriteLine("\r\n\tCommands:");
            Console.WriteLine("\r\n\t\tcompile OUTPUT TYPE FORMAT INPUT");
            Console.WriteLine("\t\t\tOUTPUT - resulting .elf output file");
            Console.WriteLine("\t\t\tTYPE   - elf64 is the only supported package type");
            Console.WriteLine("\t\t\tFORMAT - pico is the only supported code/text format");
            Console.WriteLine("\t\t\tINPUT  - source .asm assembly file");
            Console.WriteLine("\r\n\t\trun EXECUTABLE");
            Console.WriteLine("\t\t\tEXECUTABLE - file to run in the virtual machine");
            Console.WriteLine("\r\n\t\tcomprun OUTPUT TYPE FORMAT INPUT");
            Console.WriteLine("\t\t\t(same as compile + run together)");

            if (args.Length == 0)
            {
                Console.Error.WriteLine("No command specified.");
                return -1;
            }

            var command = args[0];
            if (string.Compare(command, "compile", StringComparison.CurrentCultureIgnoreCase) == 0)
            {
                if (args.Length != 5)
                {
                    Console.Error.WriteLine("Incorrect options for compile command.");
                    return -2;
                }

                var output = args[1];
                var type = args[2];
                var format = args[3];
                var input = args[4];

                var compilation = Compile(output, type, format, input);
            }
            else if (string.Compare(command, "run", StringComparison.CurrentCultureIgnoreCase) == 0)
            {
                if (args.Length != 2)
                {
                    Console.Error.WriteLine("Incorrect options for run command.");
                    return -2;
                }

                var exec = args[1];
                var loaded = Load(exec);
                var result = Execute(loaded);
            }
            else if (string.Compare(command, "comprun", StringComparison.CurrentCultureIgnoreCase) == 0)
            {
                if (args.Length != 5)
                {
                    Console.Error.WriteLine("Incorrect options for compile command.");
                    return -2;
                }

                var output = args[1];
                var type = args[2];
                var format = args[3];
                var input = args[4];

                var compilation = Compile(output, type, format, input);
                var loaded = Load(output);
                var result = Execute(loaded);
            }
            else
            {
                Console.Error.WriteLine($"Unknown option {command}.");
                return -3;
            }

            return 0;
        }

        static CompilationResult Compile(string output, string type, string format, string input)
        {
            if (!System.IO.File.Exists(input))
                return CompilationResult.Error($"Source input file {input} not found.");

            if (System.IO.File.Exists(output))
            {
                Console.Error.WriteLine($"Executable output file {output} already exists.");
                // DEBUG
                System.IO.File.Delete(output);
                //return -4;
            }

            // Compile.
            CompilationResult compilation;
            {
                Console.Out.WriteLine($"Compiling source file: {input}");
                var compiler = new BytecodeCompiler();
                compilation = compiler.Compile(input);

                if (compilation.Errors.Count > 0)
                {
                    Console.Error.WriteLine($"Compilation failed with {compilation.Errors.Count} errors.");
                    return compilation;
                }
            }

            // Package.
            var packageFormat = CompilerOutputType.Elf64;
            Console.Out.WriteLine($"Packaging bytecode as: {Enum.GetName(typeof(CompilerOutputType), packageFormat)}");
            IPackager packager;

            using (var fs = new FileStream(output, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.SequentialScan))
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

            return compilation;
        }
        static LoaderResult Load(string input)
        {
            if (!System.IO.File.Exists(input))
                return LoaderResult.Error($"Source input file {input} not found.");

            // Loader Stage 1 - read file
            LoaderResult loaded;
            using (var fs = new FileStream(input, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var loader = new LoaderElf64(fs);
                loaded = loader.Load();
            }

            return loaded;
        }
        static ExecutionResult Execute(LoaderResult loaded)
        {
            // Loader Stage 2 - setup file
            var image = loaded.Image.ToArray();

            Console.Out.WriteLine("Emulating Linux 32-bit kernel syscall interface");
            var kernel = new Linux32Kernel();
            var agent = new Agent(kernel, image, loaded.EntryPoint);
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
                    return new ExecutionResult(0);
                default:
                    Console.WriteLine("\r\n\r\nProgram errored out.");
                    return new ExecutionResult(ret ?? int.MinValue);
            }
        }
    }
}
