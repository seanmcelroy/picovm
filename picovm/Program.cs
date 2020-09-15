using System;
using System.IO;
using System.Linq;
using picovm.Assembler;
using picovm.Packager;
using picovm.Packager.Elf;
using picovm.Packager.Elf.Elf32;
using picovm.Packager.Elf.Elf64;
using picovm.VM;

namespace picovm
{
    class Program
    {
        static int Main(string[] args)
        {
            Console.WriteLine("PicoVM - A tiny toy virtual machine");
            Console.WriteLine(" (c) 2020 Sean McElroy");
            Console.WriteLine(" Released under the MIT License; all rights reserved.\r\n");

            if (args.Length == 0)
            {
                Help();
                Console.Error.WriteLine("No command specified.");
                return -1;
            }

            var command = args[0];
            if (string.Compare(command, "asm", StringComparison.CurrentCultureIgnoreCase) == 0)
            {
                if (args.Length != 5)
                {
                    Help();
                    Console.Error.WriteLine("Incorrect options for asm command.");
                    return -2;
                }

                var output = args[1];
                var type = args[2];
                var format = args[3];
                var input = args[4];

                var compilation = Assemble(output, type, format, input);
            }
            else if (string.Compare(command, "inspect", StringComparison.CurrentCultureIgnoreCase) == 0)
            {
                if (args.Length != 2)
                {
                    Help();
                    Console.Error.WriteLine("Incorrect options for inspect command.");
                    return -2;
                }

                var exec = args[1];
                PrintInspection(exec);
                return 0;
            }
            else if (string.Compare(command, "run", StringComparison.CurrentCultureIgnoreCase) == 0)
            {
                if (args.Length != 2)
                {
                    Help();
                    Console.Error.WriteLine("Incorrect options for run command.");
                    return -2;
                }

                var exec = args[1];
                var type = Inspector.DetectPackageOutputType(exec);
                var loaded = Load(exec, type);
                var result = Execute(loaded);
            }
            else if (string.Compare(command, "asmrun", StringComparison.CurrentCultureIgnoreCase) == 0)
            {
                if (args.Length != 5)
                {
                    Help();
                    Console.Error.WriteLine("Incorrect options for asmrun command.");
                    return -2;
                }

                var output = args[1];
                var type = args[2];
                var format = args[3];
                var input = args[4];

                var compilation = Assemble(output, type, format, input);
                var loaded = Load(output, type);
                var result = Execute(loaded);
            }
            else if (string.Compare(command, "help", StringComparison.CurrentCultureIgnoreCase) == 0)
            {
                Help();
            }
            else
            {
                Help();
                Console.Error.WriteLine($"Unknown option {command}.");
                return -3;
            }

            return 0;
        }

        static void Help()
        {
            Console.WriteLine(" Usage: picovm <COMMAND> INPUTS... <src.asm> <a.elf>");
            Console.WriteLine("\r\n\tCommands:");
            Console.WriteLine("\t\tasm OUTPUT TYPE FORMAT INPUT");
            Console.WriteLine("\t\t\tAssembles a file into a binary output");
            Console.WriteLine("\t\t\t OUTPUT - resulting .elf output file");
            Console.WriteLine("\t\t\t TYPE   - elf32 and elf64 is the only supported package types");
            Console.WriteLine("\t\t\t FORMAT - pico is the only supported code/text format");
            Console.WriteLine("\t\t\t INPUT  - source .asm assembly file");
            Console.WriteLine("\r\n\t\tinspect EXECUTABLE");
            Console.WriteLine("\t\t\tReads the metadata about an executable");
            Console.WriteLine("\r\n\t\trun EXECUTABLE");
            Console.WriteLine("\t\t\tRuns a binary executable file");
            Console.WriteLine("\t\t\t EXECUTABLE - file to run in the virtual machine");
            Console.WriteLine("\r\n\t\tasmrun OUTPUT TYPE FORMAT INPUT");
            Console.WriteLine("\t\t\tCombines asm and run into a single command");
        }

        static ICompilationResult Assemble(string output, string type, string format, string input)
        {
            var outputType = Enum.Parse<AssemblerPackageOutputType>(type, true);
            return Assemble(output, outputType, format, input);
        }

        static ICompilationResult Assemble(string output, AssemblerPackageOutputType outputType, string format, string input)
        {
            if (!System.IO.File.Exists(input))
                return CompilationResultBase.Error($"Source input file {input} not found.");

            if (System.IO.File.Exists(output))
            {
                Console.Error.WriteLine($"Executable output file {output} already exists.");
                // DEBUG
                System.IO.File.Delete(output);
                //return -4;
            }

            // Compile.
            ICompilationResult compilation;
            {
                Console.Out.WriteLine($"Compiling source file: {input}");
                IBytecodeCompiler? compiler = null;
                switch (outputType)
                {
                    case AssemblerPackageOutputType.Elf32:
                        Console.Out.WriteLine($"Packaging bytecode as: {Enum.GetName(typeof(AssemblerPackageOutputType), outputType)}");
                        compiler = new BytecodeCompiler<UInt32>();
                        break;
                    case AssemblerPackageOutputType.Elf64:
                        Console.Out.WriteLine($"Packaging bytecode as: {Enum.GetName(typeof(AssemblerPackageOutputType), outputType)}");
                        compiler = new BytecodeCompiler<UInt64>();
                        break;
                    default:
                        Console.Error.WriteLine($"Unsupported assembler output type {outputType}.");
                        System.Environment.Exit(-5);
                        return null;
                }

                compilation = compiler.Compile(input);

                if (compilation.Errors.Count > 0)
                {
                    Console.Error.WriteLine($"Compilation failed with {compilation.Errors.Count} errors.");
                    return compilation;
                }
            }

            // Package.
            IPackager packager;

            using (var fs = new FileStream(output, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.SequentialScan))
            {
                switch (outputType)
                {
                    case AssemblerPackageOutputType.AOut32:
                        packager = new PackagerAOut32((CompilationResult32)compilation);
                        packager.Write(fs);
                        break;
                    case AssemblerPackageOutputType.Elf32:
                        packager = new PackagerElf32((CompilationResult32)compilation);
                        packager.Write(fs);
                        break;
                    case AssemblerPackageOutputType.Elf64:
                        packager = new PackagerElf64((CompilationResult64)compilation);
                        packager.Write(fs);
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown packaging format: {outputType}");
                }

                fs.Flush();
                fs.Close();
            }

            return compilation;
        }

        static ILoaderResult Load(string input, string type)
        {
            var inputType = Enum.Parse<AssemblerPackageOutputType>(type, true);
            return Load(input, inputType);
        }

        static ILoaderResult Load(string input, AssemblerPackageOutputType inputType)
        {
            if (!System.IO.File.Exists(input))
                throw new FileNotFoundException($"Source input file {input} not found.", input);

            // Loader Stage 1 - read file
            ILoaderResult loaded;
            using (var fs = new FileStream(input, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                switch (inputType)
                {
                    case AssemblerPackageOutputType.Elf32:
                        loaded = new LoaderElf32(fs).LoadImage();
                        break;
                    case AssemblerPackageOutputType.Elf64:
                        loaded = new LoaderElf64(fs).LoadImage();
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown packaging format: {inputType}");
                }
            }

            return loaded;
        }
        static ExecutionResult Execute(ILoaderResult loaded)
        {
            // Loader Stage 2 - setup file
            var image = loaded.Image.ToArray();

            IKernel kernel;
            Agent agent;

            if (loaded.GetType() == typeof(LoaderResult32))
            {
                Console.Out.WriteLine("Emulating Linux 32-bit kernel syscall interface");
                kernel = new Linux32Kernel();
                agent = new Agent(kernel, image, ((LoaderResult32)loaded).EntryPoint);
            }
            else if (loaded.GetType() == typeof(LoaderResult64))
            {
                Console.Out.WriteLine("Emulating Linux 64-bit kernel syscall interface");
                kernel = new Linux64Kernel();
                agent = new Agent64(kernel, image, ((LoaderResult64)loaded).EntryPoint);
            }
            else
                throw new InvalidOperationException();

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

        static void PrintInspection(string target)
        {
            if (!System.IO.File.Exists(target))
                throw new FileNotFoundException($"Source input file {target} not found.", target);

            var type = Inspector.DetectPackageOutputType(target);
            switch (type)
            {
                case AssemblerPackageOutputType.Elf64:
                    PrintInspectionElf64(target);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        static void PrintInspectionElf64(string filePath)
        {
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                PrintInspectionElf64(fs);
            }
        }

        static void PrintInspectionElf64(Stream stream)
        {
            var metadata = Inspector.InspectAsElf64(stream).Metadata;
            var header = metadata.OfType<Packager.Elf.Elf64.Header64>().FirstOrDefault();

            if (header.Equals(default(Packager.Elf.Elf64.Header64)))
                Console.Out.WriteLine("No Elf64 header found.");


            Console.Out.WriteLine("ELF Header:");
            stream.Seek(0, SeekOrigin.Begin);
            var first16 = new byte[16];
            stream.Read(first16, 0, 16);
            var first16String = first16.Select(b => $"{b:x2}").Aggregate((c, n) => $"{c} {n}");
            Console.Out.WriteLine($"  Magic:   {first16String}");
            Console.Out.WriteLine($"  Class:                             {PackagerUtility.GetEnumDescription(header.EI_CLASS)}");
            Console.Out.WriteLine($"  Data:                              {PackagerUtility.GetEnumDescription(header.EI_DATA)}");
            Console.Out.WriteLine($"  Version:                           {PackagerUtility.GetEnumDescription(header.EI_VERSION)}");
            Console.Out.WriteLine($"  OS/ABI:                            {PackagerUtility.GetEnumDescription(header.EI_OSABI)}");
            Console.Out.WriteLine($"  ABI Version:                       {header.EI_ABIVERSION}");
            Console.Out.WriteLine($"  Type:                              {PackagerUtility.GetEnumDescription(header.E_TYPE)}");
            Console.Out.WriteLine($"  Machine:                           {PackagerUtility.GetEnumDescription(header.E_MACHINE)}");
            Console.Out.WriteLine($"  Version:                           {PackagerUtility.GetEnumDescription(header.E_VERSION)}");
            Console.Out.WriteLine($"  Entry point address:               0x{header.E_ENTRY:x}");
            Console.Out.WriteLine($"  Start of program headers:          {header.E_PHOFF} (bytes into file)");
            Console.Out.WriteLine($"  Start of section headers:          {header.E_SHOFF} (bytes into file)");
            Console.Out.WriteLine($"  Flags:                             0x{header.E_FLAGS:x}");
            Console.Out.WriteLine($"  Size of this header:               {header.E_EHSIZE} (bytes)");
            Console.Out.WriteLine($"  Size of program headers:           {header.E_PHENTSIZE} (bytes)");
            Console.Out.WriteLine($"  Number of program headers:         {header.E_PHNUM}");
            Console.Out.WriteLine($"  Size of section headers:           {header.E_SHENTSIZE} (bytes)");
            Console.Out.WriteLine($"  Number of section headers:         {header.E_SHNUM}");
            Console.Out.WriteLine($"  Section header string table index: {header.E_SHSTRIDX}");
        }
    }
}
