using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using picovm.Assembler;
using picovm.Commands;
using picovm.Packager;
using picovm.Packager.Elf;
using picovm.Packager.Elf.Elf32;
using picovm.Packager.Elf.Elf64;
using picovm.Packager.PE;
using picovm.VM;
using Spectre.Console.Cli;

namespace picovm
{
    class Program
    {
        static int Main(string[] args)
        {
            Console.WriteLine("PicoVM - A tiny toy virtual machine");
            Console.WriteLine(" (c) 2026 Sean McElroy");
            Console.WriteLine(" Released under the MIT License; all rights reserved.\r\n");

            var app = new CommandApp();
            app.Configure(config =>
            {
                config.SetApplicationName("picovm");

                config.AddCommand<AsmCommand>("asm")
                    .WithDescription("Assembles a source file into a binary output.");

                config.AddCommand<InspectCommand>("inspect")
                    .WithDescription("Reads the metadata about an executable.");

                config.AddCommand<RunCommand>("run")
                    .WithDescription("Runs a binary executable file in the virtual machine.");

                config.AddCommand<AsmRunCommand>("asmrun")
                    .WithDescription("Combines asm and run into a single command.");
            });

            return app.Run(args);
        }

        internal static ICompilationResult Assemble(string output, string type, string format, string input, bool noClobber)
        {
            var outputType = Enum.Parse<AssemblerPackageOutputType>(type, true);
            return Assemble(output, outputType, format, input, noClobber);
        }

        internal static ICompilationResult Assemble(string output, AssemblerPackageOutputType outputType, string format, string input, bool noClobber)
        {
            if (!File.Exists(input))
                return CompilationResultBase.Error($"Source input file {input} not found.");

            if (File.Exists(output) && noClobber)
                return CompilationResultBase.Error($"Executable output file {output} already exists (use without --no-clobber to overwrite).");

            // Compile.
            ICompilationResult compilation;
            {
                Console.Out.WriteLine($"Compiling source file: {input}");
                IBytecodeCompiler? compiler;
                switch (outputType)
                {
                    case AssemblerPackageOutputType.Elf32:
                        Console.Out.WriteLine($"Packaging bytecode as: {Enum.GetName(outputType)}");
                        compiler = new BytecodeCompiler<UInt32>();
                        break;
                    case AssemblerPackageOutputType.Elf64:
                        Console.Out.WriteLine($"Packaging bytecode as: {Enum.GetName(outputType)}");
                        compiler = new BytecodeCompiler<UInt64>();
                        break;
                    default:
                        Console.Error.WriteLine($"Unsupported assembler output type {outputType}.");
                        Environment.Exit(-5);
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
                        packager = new PackagerAOut32((CompilationResult<UInt32>)compilation);
                        packager.Write(fs);
                        break;
                    case AssemblerPackageOutputType.Elf32:
                        packager = new PackagerElf32((CompilationResult<UInt32>)compilation);
                        packager.Write(fs);
                        break;
                    case AssemblerPackageOutputType.Elf64:
                        packager = new PackagerElf64((CompilationResult<UInt64>)compilation);
                        packager.Write(fs);
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown packaging format: {outputType}");
                }
            }

            return compilation;
        }

        internal static ILoaderResult Load(string input, string type)
        {
            var inputType = Enum.Parse<AssemblerPackageOutputType>(type, true);
            return Load(input, inputType);
        }

        internal static ILoaderResult Load(string input, AssemblerPackageOutputType inputType)
        {
            if (!File.Exists(input))
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
        internal static ExecutionResult Execute(ILoaderResult loaded)
        {
            // Loader Stage 2 - setup file
            var image = loaded.Image.ToArray();

            IKernel kernel;
            Agent agent;

            if (loaded is LoaderResult32 loader32)
            {
                Console.Out.WriteLine("Emulating Linux 32-bit kernel syscall interface");
                kernel = new Linux32Kernel();
                agent = new Agent(kernel, image, loader32.EntryPoint);
            }
            else if (loaded is LoaderResult64 loader64)
            {
                Console.Out.WriteLine("Emulating Linux 64-bit kernel syscall interface");
                kernel = new Linux64Kernel();
                agent = new Agent64(kernel, image, loader64.EntryPoint);
            }
            else
                throw new InvalidOperationException();

            TickResult ret;
            do
            {
                ret = agent.Tick();
            } while (!ret.Done);

            foreach (var error in ret.Errors ?? [])
            {
                Console.Error.WriteLine($"ERROR: {error}");
            }
    
            if (ret.ErrorCode == TickErrorCode.Ok)
            {
                Console.WriteLine("\r\n\r\nProgram terminated normally.");
            } else
            {
                agent.Dump();
                Console.WriteLine($"\r\n\r\nProgram aborted with error code {ret.ErrorCode}.");
            }

            return new ExecutionResult((int)ret.ErrorCode, ret.Errors ?? []);
        }

        internal static void PrintInspection(string target)
        {
            if (!File.Exists(target))
                throw new FileNotFoundException($"Source input file {target} not found.", target);

            var type = Inspector.DetectPackageOutputType(target);
            switch (type)
            {
                case AssemblerPackageOutputType.Elf32:
                    PrintInspectionElf32(target);
                    break;
                case AssemblerPackageOutputType.Elf64:
                    PrintInspectionElf64(target);
                    break;
                case AssemblerPackageOutputType.PE:
                    PrintInspectionPE(target);
                    break;
                default:
                    Console.Error.WriteLine($"Inspection of binary file type {type} is not yet supproted");
                    throw new NotImplementedException($"Inspection of binary file type {type} is not yet supproted");
            }
        }

        static void PrintInspectionElf32(string filePath)
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            PrintInspectionElf32(fs);
        }

        static void PrintInspectionElf64(string filePath)
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            PrintInspectionElf64(fs);
        }

         static void PrintInspectionElf32(Stream stream)
        {
            var metadata = Inspector.InspectAsElf32(stream).Metadata;
            var header = metadata.OfType<Header32>().FirstOrDefault();

            if (header.Equals(default(Header32)))
                Console.Out.WriteLine("No Elf32 header found.");

            Console.Out.WriteLine("ELF Header:");
            stream.Seek(0, SeekOrigin.Begin);
            Span<byte> first16 = stackalloc byte[16];
            stream.ReadExactly(first16);
            var first16String = Convert.ToHexStringLower(first16);
            Console.Out.WriteLine($"  Magic:                             {first16String}");
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
            Console.Out.WriteLine($"  Section header string table index: {header.E_SHSTRNDX}");

            Console.Out.WriteLine($"\r\nSection Headers:");
            Console.Out.WriteLine($"  [Nr] Name              Type             Address           Offset");
            Console.Out.WriteLine($"       Size              EntSize          Flags  Link  Info  Align");
            var sectionHeaders = metadata.OfType<SectionHeader32>().ToArray();
            var sectionHeaderNames = new Dictionary<UInt32, string>();
            long? sectionHeaderNameTableOffset = header.E_SHSTRNDX == SpecialSectionIndexes.SHN_UNDEF ? default(long?) : (long)sectionHeaders[header.E_SHSTRNDX].SH_OFFSET;
            var i = 0;
            foreach (var sh in sectionHeaders)
            {
                string name = "<MISSING TABLE>";
                if (sectionHeaderNameTableOffset != null)
                {
                    stream.Seek((long)sectionHeaderNameTableOffset + sh.SH_NAME, SeekOrigin.Begin);
                    name = stream.ReadNulTerminatedString();
                    sectionHeaderNames.Add(sh.SH_NAME, name);
                }

                var flagString = PackagerUtility.GetEnumFlagsShortName<SectionHeaderFlags>((ulong)sh.SH_FLAGS); // Widen to ulong to prevent unboxing errors downstream

                Console.Out.WriteLine($"  [{i,2}] {name.LeftAlignToSize(17)} {PackagerUtility.GetEnumAttributeValue<SectionHeaderType, ShortNameAttribute>(sh.SH_TYPE, sn => sn.DisplayName).LeftAlignToSize(16)} {sh.SH_ADDR.RightAlignHexToSize()}  {sh.SH_OFFSET.RightAlignHexToSize()}");
                Console.Out.WriteLine($"       {sh.SH_SIZE.RightAlignHexToSize()}  {sh.SH_ENTSIZE.RightAlignHexToSize()}  {flagString.LeftAlignToSize(4)}  {sh.SH_LINK.RightAlignDecToSize(4, ' ')}  {sh.SH_INFO.RightAlignDecToSize(4, ' ')}  {sh.SH_ADDRALIGN.RightAlignDecToSize(4, ' ')}");
                i++;
            }
            Console.Out.WriteLine("Key to Flags:");
            Console.Out.WriteLine("  W (write), A (alloc), X (execute), M (merge), S (strings), I (info),");
            Console.Out.WriteLine("  L (link order), O (extra OS processing required), G (group), T (TLS),");
            Console.Out.WriteLine("  C (compressed), o (OS specific), E (exclude), p (processor specific)");

            Console.Out.WriteLine("\r\nProgram Headers:");
            Console.Out.WriteLine("  Type           Offset             VirtAddr           PhysAddr");
            Console.Out.WriteLine("                 FileSiz            MemSiz              Flags  Align");
            var programHeaders = metadata.OfType<ProgramHeader64>().ToArray();
            foreach (var ph in programHeaders)
            {
                var flagString = new StringBuilder();
                flagString.Append(((SegmentPermissionFlags)ph.P_FLAGS).HasFlag(SegmentPermissionFlags.PF_R) ? 'R' : ' ');
                flagString.Append(((SegmentPermissionFlags)ph.P_FLAGS).HasFlag(SegmentPermissionFlags.PF_W) ? 'W' : ' ');
                flagString.Append(((SegmentPermissionFlags)ph.P_FLAGS).HasFlag(SegmentPermissionFlags.PF_X) ? 'E' : ' ');

                Console.Out.WriteLine($"  {PackagerUtility.GetEnumAttributeValue<ProgramHeaderType, ShortNameAttribute>(ph.P_TYPE, s => s.DisplayName).LeftAlignToSize(13)}  0x{ph.P_OFFSET.RightAlignHexToSize()} 0x{ph.P_VADDR.RightAlignHexToSize()} 0x{ph.P_PADDR.RightAlignHexToSize()}");
                Console.Out.WriteLine($"                 0x{ph.P_FILESZ.RightAlignHexToSize()} 0x{ph.P_MEMSZ.RightAlignHexToSize()}  {flagString.LeftAlignToSize(6)} 0x{ph.P_ALIGN:x}");
            }

            Console.Out.WriteLine("\r\nSection to Segment mapping:");
            Console.Out.WriteLine(" Segment Sections...");
            i = 0;
            foreach (var ph in programHeaders)
            {
                var shdrs = sectionHeaders
                    .Where(sh =>
                        (sh.SH_ADDR > 0 || sh.SH_ENTSIZE > 0) &&
                        sh.SH_ADDR >= ph.P_VADDR &&
                        sh.SH_ADDR + sh.SH_SIZE <= ph.P_VADDR + ph.P_MEMSZ)
                    .Select(sh => sectionHeaderNames[sh.SH_NAME]).ToArray();
                var shdrNames = shdrs.Length == 0 ? string.Empty : shdrs.Aggregate((c, n) => $"{c} {n}");
                Console.Out.WriteLine($"  {i:00}     {shdrNames}");
                i++;
            }

        }

        static void PrintInspectionElf64(Stream stream)
        {
            var metadata = Inspector.InspectAsElf64(stream).Metadata;
            var header = metadata.OfType<Header64>().FirstOrDefault();

            if (header.Equals(default(Header64)))
                Console.Out.WriteLine("No Elf64 header found.");

            Console.Out.WriteLine("ELF Header:");
            stream.Seek(0, SeekOrigin.Begin);
            Span<byte> first16 = stackalloc byte[16];
            stream.ReadExactly(first16);
            var first16String = Convert.ToHexStringLower(first16);
            Console.Out.WriteLine($"  Magic:                             {first16String}");
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
            Console.Out.WriteLine($"  Section header string table index: {header.E_SHSTRNDX}");

            Console.Out.WriteLine($"\r\nSection Headers:");
            Console.Out.WriteLine($"  [Nr] Name              Type             Address           Offset");
            Console.Out.WriteLine($"       Size              EntSize          Flags  Link  Info  Align");
            var sectionHeaders = metadata.OfType<SectionHeader64>().ToArray();
            var sectionHeaderNames = new Dictionary<UInt32, string>();
            long? sectionHeaderNameTableOffset = header.E_SHSTRNDX == SpecialSectionIndexes.SHN_UNDEF ? default(long?) : (long)sectionHeaders[header.E_SHSTRNDX].SH_OFFSET;
            var i = 0;
            foreach (var sh in sectionHeaders)
            {
                string name = "<MISSING TABLE>";
                if (sectionHeaderNameTableOffset != null)
                {
                    stream.Seek((long)sectionHeaderNameTableOffset + sh.SH_NAME, SeekOrigin.Begin);
                    name = stream.ReadNulTerminatedString();
                    sectionHeaderNames.Add(sh.SH_NAME, name);
                }

                var flagString = PackagerUtility.GetEnumFlagsShortName<SectionHeaderFlags>(sh.SH_FLAGS);

                Console.Out.WriteLine($"  [{i.ToString().PadLeft(2)}] {name.LeftAlignToSize(17)} {PackagerUtility.GetEnumAttributeValue<SectionHeaderType, ShortNameAttribute>(sh.SH_TYPE, sn => sn.DisplayName).LeftAlignToSize(16)} {sh.SH_ADDR.RightAlignHexToSize()}  {sh.SH_OFFSET.RightAlignHexToSize()}");
                Console.Out.WriteLine($"       {sh.SH_SIZE.RightAlignHexToSize()}  {sh.SH_ENTSIZE.RightAlignHexToSize()}  {flagString.LeftAlignToSize(4)}  {sh.SH_LINK.RightAlignDecToSize(4, ' ')}  {sh.SH_INFO.RightAlignDecToSize(4, ' ')}  {sh.SH_ADDRALIGN.RightAlignDecToSize(4, ' ')}");
                i++;
            }
            Console.Out.WriteLine("Key to Flags:");
            Console.Out.WriteLine("  W (write), A (alloc), X (execute), M (merge), S (strings), I (info),");
            Console.Out.WriteLine("  L (link order), O (extra OS processing required), G (group), T (TLS),");
            Console.Out.WriteLine("  C (compressed), o (OS specific), E (exclude), p (processor specific)");

            Console.Out.WriteLine("\r\nProgram Headers:");
            Console.Out.WriteLine("  Type           Offset             VirtAddr           PhysAddr");
            Console.Out.WriteLine("                 FileSiz            MemSiz              Flags  Align");
            var programHeaders = metadata.OfType<ProgramHeader64>().ToArray();
            foreach (var ph in programHeaders)
            {
                var flagString = new StringBuilder();
                flagString.Append(((SegmentPermissionFlags)ph.P_FLAGS).HasFlag(SegmentPermissionFlags.PF_R) ? 'R' : ' ');
                flagString.Append(((SegmentPermissionFlags)ph.P_FLAGS).HasFlag(SegmentPermissionFlags.PF_W) ? 'W' : ' ');
                flagString.Append(((SegmentPermissionFlags)ph.P_FLAGS).HasFlag(SegmentPermissionFlags.PF_X) ? 'E' : ' ');

                Console.Out.WriteLine($"  {PackagerUtility.GetEnumAttributeValue<ProgramHeaderType, ShortNameAttribute>(ph.P_TYPE, s => s.DisplayName).LeftAlignToSize(13)}  0x{ph.P_OFFSET.RightAlignHexToSize()} 0x{ph.P_VADDR.RightAlignHexToSize()} 0x{ph.P_PADDR.RightAlignHexToSize()}");
                Console.Out.WriteLine($"                 0x{ph.P_FILESZ.RightAlignHexToSize()} 0x{ph.P_MEMSZ.RightAlignHexToSize()}  {flagString.LeftAlignToSize(6)} 0x{ph.P_ALIGN:x}");
            }

            Console.Out.WriteLine("\r\nSection to Segment mapping:");
            Console.Out.WriteLine(" Segment Sections...");
            i = 0;
            foreach (var ph in programHeaders)
            {
                var shdrs = sectionHeaders
                    .Where(sh =>
                        (sh.SH_ADDR > 0 || sh.SH_ENTSIZE > 0) &&
                        sh.SH_ADDR >= ph.P_VADDR &&
                        sh.SH_ADDR + sh.SH_SIZE <= ph.P_VADDR + ph.P_MEMSZ)
                    .Select(sh => sectionHeaderNames[sh.SH_NAME]).ToArray();
                var shdrNames = shdrs.Length == 0 ? string.Empty : shdrs.Aggregate((c, n) => $"{c} {n}");
                Console.Out.WriteLine($"  {i:00}     {shdrNames}");
                i++;
            }

        }

        static void PrintInspectionPE(string filePath)
        {
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                PrintInspectionPE(fs);
            }
        }

        static void PrintInspectionPE(Stream stream)
        {
            var metadata = Inspector.InspectAsPE(stream).Metadata;
            var msDosStubHeader = metadata.OfType<MsDosStubHeader>().FirstOrDefault();

            if (msDosStubHeader.Equals(default(MsDosStubHeader)))
                Console.Out.WriteLine("No MSDOS stub header found.");

            Console.Out.WriteLine("MSDOS Header:");
            stream.Seek(0, SeekOrigin.Begin);
            Span<byte> first2 = stackalloc byte[2];
            stream.ReadExactly(first2);
            Console.Out.WriteLine($"  Magic:                             {Convert.ToHexStringLower(first2)}");
            Console.Out.WriteLine($"  PE Header Offset:                  0x{msDosStubHeader.e_lfanew:x}");
            if (msDosStubHeader.e_lfanew % 8 != 0)
                Console.Error.WriteLine("WARN: PE header should be aligned to an 8-byte boundary.");

            {
                var peHeader = metadata.OfType<PEHeader>().FirstOrDefault();
                if (peHeader.Equals(default(PEHeader)))
                    Console.Out.WriteLine("No PE header found.");
                else
                {
                    stream.Seek(msDosStubHeader.e_lfanew, SeekOrigin.Begin);
                    stream.ReadExactly(first2);
                    Console.Out.WriteLine("\r\nPE Header:");
                    Console.Out.WriteLine($"  Magic:                             {Convert.ToHexStringLower(first2)}");
                    Console.Out.WriteLine($"  Machine:                           {PackagerUtility.GetEnumDescription<MachineType>(peHeader.mMachine)}");
                    Console.Out.WriteLine($"  Number of sections:                {peHeader.mNumberOfSections}");
                    var ts = DateTime.UnixEpoch.AddSeconds(peHeader.mTimeDateStamp).ToUniversalTime();
                    Console.Out.WriteLine($"  Timestamp:                         {ts.ToLongDateString()} {ts.ToLongTimeString()} UTC");
                    Console.Out.WriteLine($"  Pointer to symbol table:           0x{peHeader.mPointerToSymbolTable:x}");
                    Console.Out.WriteLine($"  Number of symbols:                 {peHeader.mNumberOfSymbols}");
                    Console.Out.WriteLine($"  Size of optional PE header:        {peHeader.mSizeOfOptionalHeader} (bytes)");
                    Console.Out.WriteLine($"  Characteristics:                   {PackagerUtility.GetEnumFlagsShortName<PECharacteristics>(peHeader.mCharacteristics, ", ")}");
                }
            }

            {
                var pe32 = metadata.OfType<PEHeaderOption32>().FirstOrDefault();
                if (!pe32.Equals(default(PEHeaderOption32)))
                {
                    Console.Out.WriteLine("\r\nPE Optional 32-bit Header:");
                    Console.Out.WriteLine($"  Linker version:                    {pe32.mMajorLinkerVersion}.{pe32.mMinorLinkerVersion}");
                    Console.Out.WriteLine($"  Size of code:                      {pe32.mSizeOfCode} (bytes)");
                    Console.Out.WriteLine($"  Size of initialized data:          {pe32.mSizeOfInitializedData} (bytes)");
                    Console.Out.WriteLine($"  Size of uninitialized data:        {pe32.mSizeOfUninitializedData} (bytes)");
                    Console.Out.WriteLine($"  Entry point:                       0x{pe32.mAddressOfEntryPoint:x}");
                    Console.Out.WriteLine($"  Base address of code section:      0x{pe32.mBaseOfCode:x}");
                    Console.Out.WriteLine($"  Base address of data section:      0x{pe32.mBaseOfData:x}");
                    Console.Out.WriteLine($"  Base address of image:             0x{pe32.mImageBase:x}");
                    Console.Out.WriteLine($"  Section alignment:                 {pe32.mSectionAlignment}");
                    if (pe32.mSectionAlignment < pe32.mFileAlignment)
                        Console.Error.WriteLine($"ERROR: Section alignment value must be greater than or equal to the file alignment!");
                    Console.Out.WriteLine($"  File alignment:                    {pe32.mFileAlignment}{(pe32.mFileAlignment != 512 ? " (Default is 512)" : string.Empty)}");
                    if (pe32.mFileAlignment < 512 || pe32.mFileAlignment > 65536 || (pe32.mFileAlignment & (pe32.mFileAlignment - 1)) != 0)
                        Console.Error.WriteLine($"WARN: The value should be a power of 2 between 512 and 64K (inclusive).");
                    Console.Out.WriteLine($"  Operating system version:          {pe32.mMajorOperatingSystemVersion}.{pe32.mMinorOperatingSystemVersion} ({GetWindowsVersion(pe32.mMajorOperatingSystemVersion, pe32.mMinorOperatingSystemVersion)})");
                    Console.Out.WriteLine($"  Image version:                     {pe32.mMajorImageVersion}.{pe32.mMinorImageVersion}");
                    Console.Out.WriteLine($"  Subsystem version:                 {pe32.mMajorSubsystemVersion}.{pe32.mMinorSubsystemVersion}");
                    if (pe32.mWin32VersionValue != 0)
                    {
                        Console.Out.WriteLine($"  Win32 version:                     {pe32.mWin32VersionValue}");
                        Console.Error.WriteLine($"ERROR: This member is reserved and must be 0!");
                    }
                    Console.Out.WriteLine($"  Size of image:                     {pe32.mSizeOfImage} (bytes)");
                    if (pe32.mSizeOfImage % pe32.mSectionAlignment != 0)
                        Console.Error.WriteLine($"ERROR: Size of image must be a multiple of the section alignment!");
                    Console.Out.WriteLine($"  Size of headers:                   {pe32.mSizeOfHeaders} (bytes)");
                    Console.Out.WriteLine($"  Checksum:                          0x{pe32.mCheckSum:x}");
                    Console.Out.WriteLine($"  Subsystem:                         {PackagerUtility.GetEnumDescription<Subsystem>(pe32.mSubsystem)} (0x{pe32.mSubsystem:x})");
                    Console.Out.WriteLine($"  DLL characteristics:               {PackagerUtility.GetEnumFlagsShortName<DllCharacteristics>(pe32.mDllCharacteristics, ", ")}");
                    Console.Out.WriteLine($"  Size of stack reserve:             {pe32.mSizeOfStackReserve} (bytes)");
                    Console.Out.WriteLine($"  Size of stack commit:              {pe32.mSizeOfStackCommit} (bytes)");
                    Console.Out.WriteLine($"  Size of heap reserve:              {pe32.mSizeOfHeapReserve} (bytes)");
                    Console.Out.WriteLine($"  Size of heap commit:               {pe32.mSizeOfHeapCommit} (bytes)");
                    Console.Out.WriteLine($"  Loader flags (obsolete):           0x{pe32.mLoaderFlags:x}");
                    Console.Out.WriteLine($"  Number of directory entries:       {pe32.mNumberOfRvaAndSizes}");
                }
            }

            {
                var pe64 = metadata.OfType<PEHeaderOption64>().FirstOrDefault();
                if (!pe64.Equals(default(PEHeaderOption64)))
                {
                    Console.Out.WriteLine("\r\nPE Optional 64-bit Header:");
                    Console.Out.WriteLine($"  Linker major version:              {pe64.mMajorLinkerVersion}");
                    Console.Out.WriteLine($"  Linker minor version:              {pe64.mMinorLinkerVersion}");
                    Console.Out.WriteLine($"  Size of code:                      {pe64.mSizeOfCode} (bytes)");
                    Console.Out.WriteLine($"  Size of initialized data:          {pe64.mSizeOfInitializedData} (bytes)");
                    Console.Out.WriteLine($"  Size of uninitialized data:        {pe64.mSizeOfUninitializedData} (bytes)");
                    Console.Out.WriteLine($"  Entry point:                       0x{pe64.mAddressOfEntryPoint:x}");
                    Console.Out.WriteLine($"  Base address of code section:      0x{pe64.mBaseOfCode:x}");
                    Console.Out.WriteLine($"  Base address of image:             0x{pe64.mImageBase:x}");
                    Console.Out.WriteLine($"  Section alignment:                 {pe64.mSectionAlignment}");
                    if (pe64.mSectionAlignment < pe64.mFileAlignment)
                        Console.Error.WriteLine($"ERROR: Section alignment value must be greater than or equal to the file alignment!");
                    Console.Out.WriteLine($"  File alignment:                    {pe64.mFileAlignment}{(pe64.mFileAlignment != 512 ? " (Default is 512)" : string.Empty)}");
                    if (pe64.mFileAlignment < 512 || pe64.mFileAlignment > 65536 || (pe64.mFileAlignment & (pe64.mFileAlignment - 1)) != 0)
                        Console.Error.WriteLine($"WARN: The value should be a power of 2 between 512 and 64K (inclusive).");
                    Console.Out.WriteLine($"  Operating system version:          {pe64.mMajorOperatingSystemVersion}.{pe64.mMinorOperatingSystemVersion} ({GetWindowsVersion(pe64.mMajorOperatingSystemVersion, pe64.mMinorOperatingSystemVersion)})");
                    Console.Out.WriteLine($"  Image version:                     {pe64.mMajorImageVersion}.{pe64.mMinorImageVersion}");
                    Console.Out.WriteLine($"  Subsystem version:                 {pe64.mMajorSubsystemVersion}.{pe64.mMinorSubsystemVersion}");
                    if (pe64.mWin32VersionValue != 0)
                    {
                        Console.Out.WriteLine($"  Win32 version:                     {pe64.mWin32VersionValue}");
                        Console.Error.WriteLine($"ERROR: This member is reserved and must be 0!");
                    }
                    Console.Out.WriteLine($"  Size of image:                     {pe64.mSizeOfImage} (bytes)");
                    if (pe64.mSizeOfImage % pe64.mSectionAlignment != 0)
                        Console.Error.WriteLine($"ERROR: Size of image must be a multiple of the section alignment!");
                    Console.Out.WriteLine($"  Size of headers:                   {pe64.mSizeOfHeaders} (bytes)");
                    Console.Out.WriteLine($"  Checksum:                          0x{pe64.mCheckSum:x}");
                    Console.Out.WriteLine($"  Subsystem:                         {PackagerUtility.GetEnumDescription<Subsystem>(pe64.mSubsystem)} (0x{pe64.mSubsystem:x})");
                    Console.Out.WriteLine($"  DLL characteristics:               {PackagerUtility.GetEnumFlagsShortName<DllCharacteristics>(pe64.mDllCharacteristics, ", ")}");
                    Console.Out.WriteLine($"  Size of stack reserve:             {pe64.mSizeOfStackReserve} (bytes)");
                    Console.Out.WriteLine($"  Size of stack commit:              {pe64.mSizeOfStackCommit} (bytes)");
                    Console.Out.WriteLine($"  Size of heap reserve:              {pe64.mSizeOfHeapReserve} (bytes)");
                    Console.Out.WriteLine($"  Size of heap commit:               {pe64.mSizeOfHeapCommit} (bytes)");
                    Console.Out.WriteLine($"  Loader flags (obsolete):           0x{pe64.mLoaderFlags:x}");
                    Console.Out.WriteLine($"  Number of directory entries:       {pe64.mNumberOfRvaAndSizes}");
                }
            }

            {
                var sht = metadata.OfType<SectionHeaderTable>().FirstOrDefault();
                if (sht == null)
                {
                    Console.Out.WriteLine($"\r\nSection Headers: NONE");
                }
                else
                {
                    Console.Out.WriteLine($"  [Nr] Name      VirtualAddress  VirtualSize");
                    Console.Out.WriteLine($"       Characteristics");
                    var i = 0;
                    foreach (var sh in sht)
                    {
                        var name = Encoding.ASCII.GetString(BitConverter.GetBytes(sh.Name).TakeWhile(b => b != 0x00).ToArray());
                        Console.Out.WriteLine($"  [{i,2}] {name.LeftAlignToSize(8)}  {sh.VirtualAddress.RightAlignHexToSize().LeftAlignToSize(14)}  {sh.VirtualSize.RightAlignHexToSize()}");
                        var flagString = PackagerUtility.GetEnumFlagsShortName<SectionHeaderCharacteristics>(sh.Characteristics, ", ");
                        Console.Out.WriteLine($"       {flagString}");

                        i++;
                    }
                }
            }

            {
                var dd = metadata.OfType<PEDataDictionary>().FirstOrDefault();
                if (dd == null)
                {
                    Console.Out.WriteLine("\r\nPE Data Dictionaries: NONE");
                }
                else if (!dd.Equals(default(PEDataDictionary)))
                {
                    Console.Out.WriteLine("\r\nPE Data Dictionaries:");
                    var i = 0;
                    foreach (var dde in dd)
                    {
                        if (dde.RelativeVirtualAddress > 0)
                            Console.Out.WriteLine($"  {Enum.GetName(typeof(PEDataDictionaryIndex), i)?.LeftAlignToSize(30) ?? string.Empty} at 0x{dde.RelativeVirtualAddress:x}");
                        i++;
                    }
                }
            }

            var verbosity = 1;
            {
                var idt = metadata.OfType<PEImportDirectoryTable>().FirstOrDefault();
                var ilts = metadata.OfType<PEImportLookupTable>().ToArray();

                if (idt == default(PEImportDirectoryTable))
                    Console.Error.WriteLine("WARN: Missing Import Directory Table..");
                else
                {
                    if (ilts.Length == 0)
                        Console.Error.WriteLine("ERROR: Missing Import Lookup Table!");
                    if (ilts.Length != idt.Count)
                        Console.Error.WriteLine($"ERROR: Import Lookup Table count ({ilts.Length}) did not match directory table entry count ({idt.Count})!");

                    Console.Out.WriteLine("\r\nImport Directory Table:");
                    var i = 0;
                    foreach (var ide in idt)
                    {
                        Console.Out.WriteLine($"  {ide.Name}");
                        if (verbosity > 1)
                        {
                            var missingNames = 0;
                            foreach (var ile in ilts[i])
                            {
                                if (!string.IsNullOrEmpty(ile.Value))
                                    Console.Out.WriteLine($"    {ile.Value}");
                                else
                                    missingNames++;
                            }

                            if (missingNames > 0)
                                Console.Out.WriteLine($"   ...and {missingNames} unresolvable ordinal imports");
                        }
                        i++;
                    }
                }
            }

        }

        private static string GetWindowsVersion(UInt16 major, UInt16 minor)
        {
            if (major == 10 && minor == 0)
                return "Windows 10/Server 2019/Server 2016";
            if (major == 6 && minor == 3)
                return "Windows 8.1/Server 2012 R2";
            if (major == 6 && minor == 2)
                return "Windows 8/Server 2012";
            if (major == 6 && minor == 1)
                return "Windows 7/Server 2008 R2";
            if (major == 6 && minor == 0)
                return "Windows Vista/Server 2008";
            if (major == 5 && minor == 2)
                return "Windows XP 64-Bit Edition/Server 2003/Server 2003 R2";
            if (major == 5 && minor == 1)
                return "Windows XP";
            if (major == 5 && minor == 0)
                return "Windows 2000/NT 5.0";
            if (major == 4 && minor == 90)
                return "Windows Me";
            if (major == 4 && minor == 10)
                return "Windows 98";
            if (major == 4 && minor == 0)
                return "Windows 95/NT 4.0";
            if (major == 3 && minor == 51)
                return "Windows NT 3.51";
            if (major == 3 && minor == 5)
                return "Windows NT 3.5";
            if (major == 3 && minor == 2)
                return "Windows 3.2";
            if (major == 3 && minor == 11)
                return "Windows for Workgroups 3.11";
            if (major == 3 && minor == 1)
                return "Windows 3.1/Windows NT 3.1";
            if (major == 3 && minor == 0)
                return "Windows 3.0";
            if (major == 2 && minor == 11)
                return "Windows 2.11 (1989)";
            if (major == 2 && minor == 10)
                return "Windows 2.10 (1988)";
            if (major == 2 && minor == 3)
                return "Windows 2.03 (1987)";
            if (major == 1 && minor == 4)
                return "Windows 1.04 (1987)";
            if (major == 1 && minor == 3)
                return "Windows 1.03 (1986)";
            if (major == 1 && minor == 2)
                return "Windows 1.02 (1986)";
            if (major == 1 && minor == 0)
                return "Windows 1.0 (1985)";

            return "Unrecognized OS version";
        }

    }
}
