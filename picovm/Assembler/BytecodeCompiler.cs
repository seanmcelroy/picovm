using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;

namespace picovm.Assembler
{
    public class BytecodeCompiler<TAddrSize> : IBytecodeCompiler
        where TAddrSize : struct, IComparable, IComparable<TAddrSize>, IConvertible, IEquatable<TAddrSize>, IFormattable
    {
        private readonly Dictionary<string, Bytecode> opcodes;
        private readonly Dictionary<string, Register> registers;

        public BytecodeCompiler()
        {
            // Generate opcode dictionary
            opcodes = Enum.GetValues(typeof(Bytecode)).Cast<Bytecode>().ToDictionary(k => GetEnumDescription(k), v => v);
            registers = Enum.GetValues(typeof(Register)).Cast<Register>().ToDictionary(k => GetEnumDescription(k), v => v);
        }

        public ICompilationResult Compile(string sourceFilename)
        {
            if (!System.IO.File.Exists(sourceFilename))
            {
                return CompilationResultBase.Error($"Source input file {sourceFilename} not found.", sourceFilename);
            }

            string[] programText;
            try
            {
                programText = System.IO.File.ReadAllLines(sourceFilename);
            }
            catch (Exception ex)
            {
                return CompilationResultBase.Error($"Error while attempting to read input file: {ex.Message}");
            }

            return Compile(programText, sourceFilename);
        }

        public ICompilationResult Compile(IEnumerable<string> programLines, string? sourceFilename = null)
        {
            uint? textSegmentSize = null;
            uint? dataSegmentSize = null;
            uint? bssSegmentSize = null;
            string? entryPointSymbol = null;
            ValueType entryPoint = typeof(TAddrSize) == typeof(UInt32) ? (ValueType)(UInt32)0 : (ValueType)(UInt64)0;
            ValueType textSegmentBase = typeof(TAddrSize) == typeof(UInt32) ? (ValueType)(UInt32)0 : (ValueType)(UInt64)0;
            ValueType dataSegmentBase = typeof(TAddrSize) == typeof(UInt32) ? (ValueType)(UInt32)0 : (ValueType)(UInt64)0;
            byte[]? textSegment = null;
            ImmutableDictionary<string, ValueType> textLabelsOffsets = ImmutableDictionary<string, ValueType>.Empty;
            ImmutableList<IBytecodeTextSymbol> textSymbolReferenceOffsets = ImmutableList<IBytecodeTextSymbol>.Empty;
            ImmutableArray<byte> dataSegment = ImmutableArray<byte>.Empty;
            ImmutableDictionary<string, IBytecodeDataSymbol> dataSymbolOffsets = ImmutableDictionary<string, IBytecodeDataSymbol>.Empty;
            ImmutableList<BytecodeBssSymbol> bssSymbols = ImmutableList<BytecodeBssSymbol>.Empty;
            var errors = new List<CompilationError>(10);

            // Group program lines into sections
            var sections = new Dictionary<SectionType, List<string>>();
            KeyValuePair<SectionType, List<string>> currentSection = default(KeyValuePair<SectionType, List<string>>);

            ushort lineNumber = 0;

            var macros = new List<Macro>();
            var inMacroDefinition = false;
            foreach (var programLine in programLines)
            {
                lineNumber++;

                // Knock off any comments
                var line = programLine.Split(';')[0].Trim(' ', '\t');

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // Macros
                if (line.TrimStart(' ', '\t').StartsWith("%macro"))
                {
                    inMacroDefinition = true;
                    var macroParts = line.Substring(line.IndexOf("%macro") + 6).Split(' ', '\t', StringSplitOptions.RemoveEmptyEntries);
                    macros.Add(new Macro(macroParts[0], byte.Parse(macroParts[1]), new string[0]));
                    continue;
                }
                else if (inMacroDefinition)
                {
                    if (line.TrimStart(' ', '\t').StartsWith("%endmacro"))
                    {
                        inMacroDefinition = false;
                        continue;
                    }

                    macros.Last().MacroLines.Add(line);
                    continue;
                }

                // Sections
                if (line.StartsWith("section", StringComparison.OrdinalIgnoreCase))
                {
                    // New section
                    if (line.IndexOf(".text", StringComparison.OrdinalIgnoreCase) > -1)
                    {
                        // if (dataSegmentStarted)
                        //     throw new InvalidOperationException("Currently the compiler only supports data sections after all text sections");

                        currentSection = new KeyValuePair<SectionType, List<string>>(SectionType.Text, new List<string>());
                        sections.Add(currentSection.Key, currentSection.Value);
                    }
                    else if (line.IndexOf(".data", StringComparison.OrdinalIgnoreCase) > -1)
                    {
                        //  dataSegmentStarted = true;
                        currentSection = new KeyValuePair<SectionType, List<string>>(SectionType.Data, new List<string>());
                        sections.Add(currentSection.Key, currentSection.Value);
                    }
                    else if (line.IndexOf(".bss", StringComparison.OrdinalIgnoreCase) > -1)
                    {
                        currentSection = new KeyValuePair<SectionType, List<string>>(SectionType.BSS, new List<string>());
                        sections.Add(currentSection.Key, currentSection.Value);
                    }
                    else
                    {
                        errors.Add(new CompilationError($"Unknown section type: {line}", sourceFilename, lineNumber));
                        throw new InvalidOperationException($"Unknown section type: '{line}' ({sourceFilename}:{lineNumber})");
                    }
                }
                else
                {
                    // Special handling for linker directives at this layer-above.
                    switch (currentSection.Key)
                    {
                        case SectionType.Text:
                            if (line.StartsWith("global ", StringComparison.OrdinalIgnoreCase))
                            {
                                entryPointSymbol = line.Substring(line.IndexOf("global ", StringComparison.OrdinalIgnoreCase) + "global ".Length);
                                continue;
                            }
                            break;
                    }

                    currentSection.Value.Add(line);
                }
            }

            foreach (var section in sections)
            {
                switch (section.Key)
                {
                    case SectionType.Text:
                        var bytecodeGeneration = CompileTextSectionLinesToBytecode(section.Value);
                        textSegment = bytecodeGeneration.Bytecode.ToArray();
                        textSegmentSize = (uint)bytecodeGeneration.Bytecode.Length;
                        textLabelsOffsets = bytecodeGeneration.LabelsOffsets;
                        textSymbolReferenceOffsets = bytecodeGeneration.SymbolReferenceOffsets;

                        if (entryPointSymbol == null)
                        {
                            errors.Add(new CompilationError($"No entry point specified.", sourceFilename));
                            throw new NotImplementedException($"Unable to generate compiled output for section type: {section.Key}");
                        }
                        else if (!textLabelsOffsets.ContainsKey(entryPointSymbol))
                        {
                            errors.Add(new CompilationError($"No entry point located in source file.", sourceFilename));
                            throw new NotImplementedException($"Unable to generate compiled output for section type: {section.Key}");
                        }
                        entryPoint = typeof(TAddrSize) == typeof(UInt32) ? (ValueType)(UInt32)textLabelsOffsets[entryPointSymbol] : (ValueType)(UInt64)textLabelsOffsets[entryPointSymbol];
                        break;
                    case SectionType.Data:
                        var constGeneration = CompileDataSectionLines(section.Value);
                        dataSegment = constGeneration.Bytecode;
                        dataSegmentSize = (uint)constGeneration.Bytecode.Length;
                        dataSymbolOffsets = constGeneration.SymbolOffsets;
                        break;
                    case SectionType.BSS:
                        var bssGeneration = CompileBssSectionResult.CompileBssSectionLines(section.Value);
                        bssSymbols = bssGeneration.Symbols;
                        bssSegmentSize = Convert.ToUInt32(bssGeneration.Symbols.Sum(s => s.Size()));
                        break;
                    default:
                        throw new NotImplementedException($"Unable to generate compiled output for section type: {section.Key}");
                }
            }

            // Resolve data section variables to symbols in text
            if (dataSymbolOffsets == null || dataSymbolOffsets.Count == 0)
            {
                if (textSymbolReferenceOffsets != null)
                    foreach (var missing in textSymbolReferenceOffsets)
                        errors.Add(new CompilationError($"Symbol {missing.Name} in program code is undefined; there is NO DATA SECTION!", sourceFilename));
            }
            else
            {
                if (dataSegment == null)
                {
                    errors.Add(new CompilationError($"Data segment missing, yet {dataSymbolOffsets.Count} data symbols are defined."));
                    return new CompilationResultBase(errors);
                }

                if (textSegmentSize == null)
                {
                    errors.Add(new CompilationError($"Text segment size unknown, but this is needed to resolve data section variables."));
                    return new CompilationResultBase(errors);
                }

                foreach (var missing in textSymbolReferenceOffsets
                    .Where(tsr => !dataSymbolOffsets.ContainsKey(tsr.Name)
                               && !textLabelsOffsets.ContainsKey(tsr.Name)
                               && !bssSymbols.Any(bss => string.Compare(bss.name, tsr.Name, StringComparison.InvariantCultureIgnoreCase) == 0)))
                    errors.Add(new CompilationError($"Symbol {missing.Name} in program code is undefined by the data and BSS sections", sourceFilename));
                if (errors.Count > 0)
                    return new CompilationResultBase(errors);

                foreach (var extra in dataSymbolOffsets.Where(dsr => !textSymbolReferenceOffsets.Any(tsr => string.Compare(tsr.Name, dsr.Key, StringComparison.InvariantCulture) == 0)))
                    errors.Add(new CompilationError($"Data symbol {extra.Key} is not referenced in program code", sourceFilename));

                // Rebase data symbol offsets
                if (typeof(TAddrSize) == typeof(UInt32))
                {
                    dataSegmentBase = (UInt32)textSegmentBase + textSegmentSize.Value;
                    foreach (var ds in dataSymbolOffsets)
                        ds.Value.DataSegmentOffset = (UInt32)ds.Value.DataSegmentOffset + (UInt32)dataSegmentBase;
                }
                else
                {
                    dataSegmentBase = (UInt64)textSegmentBase + textSegmentSize.Value;
                    foreach (var ds in dataSymbolOffsets)
                        ds.Value.DataSegmentOffset = (UInt64)ds.Value.DataSegmentOffset + (UInt64)dataSegmentBase;
                }

                // Perform text/label replacements
                if (textLabelsOffsets != null)
                {
                    foreach (var tsr in textSymbolReferenceOffsets.Where(tsr => textLabelsOffsets.ContainsKey(tsr.Name)))
                    {
                        ValueType labelOffsetAddress = textLabelsOffsets[tsr.Name];
                        byte[] labelOffsetAddressBytes = typeof(TAddrSize) == typeof(UInt32) ? BitConverter.GetBytes((UInt32)labelOffsetAddress) : BitConverter.GetBytes((UInt64)labelOffsetAddress);
                        if (labelOffsetAddressBytes.Length != tsr.ReferenceLength)
                            throw new InvalidOperationException($"Address size reserved for symbol {tsr.Name} is {tsr.ReferenceLength}, but needed {labelOffsetAddressBytes.Length}");

                        if (textSegment == null)
                            throw new InvalidOperationException($"Text segment is not loaded, and so symbol {tsr.Name} cannot be resolved");

                        for (var i = 0; i < 4; i++)
                        {
                            if (typeof(TAddrSize) == typeof(UInt32))
                            {
                                if (textSegment[(long)((UInt32)(tsr.TextSegmentReferenceOffset) + i)] != 0xEE)
                                    throw new InvalidOperationException($"Attempted to overwrite placeholder for {tsr.Name} which did not contain placeholder values!");
                            }
                            else
                            {
                                if (textSegment[(long)((UInt64)(tsr.TextSegmentReferenceOffset) + (UInt64)i)] != 0xEE)
                                    throw new InvalidOperationException($"Attempted to overwrite placeholder for {tsr.Name} which did not contain placeholder values!");
                            }
                        }

                        if (typeof(TAddrSize) == typeof(UInt32))
                            Array.Copy(labelOffsetAddressBytes, (long)0, textSegment, (long)Convert.ToUInt32(tsr.TextSegmentReferenceOffset), tsr.ReferenceLength);
                        else
                            Array.Copy(labelOffsetAddressBytes, (long)0, textSegment, (long)Convert.ToUInt64(tsr.TextSegmentReferenceOffset), tsr.ReferenceLength);

                        Console.Out.WriteLine($"\tLBL {tsr.Name}->{labelOffsetAddress}");
                    }
                }

                // Perform data replacements
                if (textSymbolReferenceOffsets != null && textSymbolReferenceOffsets.Count > 0)
                {
                    if (textSegment == null)
                    {
                        errors.Add(new CompilationError($"Text segment null, but this is needed to resolve and replace data section variables."));
                        return new CompilationResultBase(errors);
                    }

                    foreach (var tsr in textSymbolReferenceOffsets.Where(tsr => dataSymbolOffsets.ContainsKey(tsr.Name)))
                    {
                        var dataSymbol = dataSymbolOffsets[tsr.Name];
                        ValueType dataSymbolAddress = typeof(TAddrSize) == typeof(UInt32)
                            ? (ValueType)Convert.ToUInt32(dataSymbol.DataSegmentOffset)
                            : (ValueType)Convert.ToUInt64(dataSymbol.DataSegmentOffset);

                        if (dataSymbol.Constant)
                        {
                            // This is a value, just write it directly into the text.
                            if (typeof(TAddrSize) == typeof(UInt32))
                            {
                                if (textSegment[(long)(UInt32)tsr.TextSegmentInstructionOffset] == (byte)Bytecode.MOV_REG_MEM)
                                    textSegment[(long)(UInt32)tsr.TextSegmentInstructionOffset] = (byte)Bytecode.MOV_REG_CON;
                                else
                                    throw new InvalidOperationException($"Unable to handle constant inlining of instruction: {textSegment[(long)(UInt32)tsr.TextSegmentInstructionOffset]} for symbol {tsr.Name}");
                            }
                            else
                            {
                                if (textSegment[(long)(UInt64)tsr.TextSegmentInstructionOffset] == (byte)Bytecode.MOV_REG_MEM)
                                    textSegment[(long)(UInt64)tsr.TextSegmentInstructionOffset] = (byte)Bytecode.MOV_REG_CON;
                                else
                                    throw new InvalidOperationException($"Unable to handle constant inlining of instruction: {textSegment[(long)(UInt64)tsr.TextSegmentInstructionOffset]} for symbol {tsr.Name}");
                            }

                            switch (dataSymbol.Length)
                            {
                                case 2:
                                    {
                                        switch (tsr.ReferenceLength)
                                        {
                                            case 1:
                                                throw new InvalidOperationException("Unable to inline constant size of 2 bytes into reserved text section of 1 byte");
                                            case 2:
                                                // 2 to 2, straight array copy.
                                                if (typeof(TAddrSize) == typeof(UInt32))
                                                    Array.Copy(dataSegment.ToArray(), (long)((UInt32)dataSymbolAddress - (UInt32)dataSegmentBase), textSegment, (long)(UInt32)tsr.TextSegmentReferenceOffset, 2);
                                                else
                                                    Array.Copy(dataSegment.ToArray(), (long)((UInt64)dataSymbolAddress - (UInt64)dataSegmentBase), textSegment, (long)(UInt64)tsr.TextSegmentReferenceOffset, 2);

                                                for (var i = 0; i < 2; i++)
                                                {
                                                    if (typeof(TAddrSize) == typeof(UInt32))
                                                    {
                                                        if (textSegment[(long)((UInt32)tsr.TextSegmentReferenceOffset + i)] != 0xFF)
                                                            throw new InvalidOperationException($"Attempted to overwrite placeholder for {tsr.Name} which did not contain placeholder values!");
                                                    }
                                                    else
                                                    {
                                                        if (textSegment[(long)((UInt64)tsr.TextSegmentReferenceOffset + (UInt64)i)] != 0xFF)
                                                            throw new InvalidOperationException($"Attempted to overwrite placeholder for {tsr.Name} which did not contain placeholder values!");

                                                    }
                                                }
                                                break;
                                            case 4:
                                                // 2 to 4 upsize
                                                var dataSymbolValue = typeof(TAddrSize) == typeof(UInt32)
                                                    ? BitConverter.ToUInt16(dataSegment.ToArray(), (int)((UInt32)dataSymbolAddress - (UInt32)dataSegmentBase))
                                                    : BitConverter.ToUInt16(dataSegment.ToArray(), (int)((UInt64)dataSymbolAddress - (UInt64)dataSegmentBase));
                                                var tsrValue = Convert.ToUInt32(dataSymbolValue);
                                                // Validate we're overwriting the right place
                                                for (var i = 0; i < 4; i++)
                                                {
                                                    if (typeof(TAddrSize) == typeof(UInt32))
                                                    {
                                                        if (textSegment[(long)((UInt32)tsr.TextSegmentReferenceOffset + i)] != 0xFF)
                                                            throw new InvalidOperationException($"Attempted to overwrite placeholder for {tsr.Name} which did not contain placeholder values!");
                                                    }
                                                    else
                                                    {
                                                        if (textSegment[(long)((UInt64)tsr.TextSegmentReferenceOffset + (UInt64)i)] != 0xFF)
                                                            throw new InvalidOperationException($"Attempted to overwrite placeholder for {tsr.Name} which did not contain placeholder values!");
                                                    }
                                                }
                                                if (typeof(TAddrSize) == typeof(UInt32))
                                                    Array.Copy(BitConverter.GetBytes(tsrValue), (long)0, textSegment, (long)(UInt32)tsr.TextSegmentReferenceOffset, 4);
                                                else
                                                    Array.Copy(BitConverter.GetBytes(tsrValue), (long)0, textSegment, (long)(UInt64)tsr.TextSegmentReferenceOffset, 4);

                                                break;
                                            default:
                                                throw new NotImplementedException();
                                        }
                                        break;
                                    }
                                case 4:
                                    {
                                        switch (tsr.ReferenceLength)
                                        {
                                            case 1:
                                            case 2:
                                                throw new InvalidOperationException($"Unable to inline constant size of 4 bytes into reserved text section of {tsr.ReferenceLength} bytes");
                                            case 4:
                                                // 4 to 4, straight array copy.
                                                if (typeof(TAddrSize) == typeof(UInt32))
                                                    Array.Copy(dataSegment.ToArray(), (long)((UInt32)dataSymbolAddress - (UInt32)dataSegmentBase), textSegment, (long)(UInt32)tsr.TextSegmentReferenceOffset, 4);
                                                else
                                                    Array.Copy(dataSegment.ToArray(), (long)((UInt64)dataSymbolAddress - (UInt64)dataSegmentBase), textSegment, (long)(UInt64)tsr.TextSegmentReferenceOffset, 4);
                                                break;
                                            default:
                                                throw new NotImplementedException();
                                        }
                                        break;
                                    }
                                case 8:
                                    {
                                        switch (tsr.ReferenceLength)
                                        {
                                            case 1:
                                            case 2:
                                            case 4:
                                                throw new InvalidOperationException($"Unable to inline constant size of 8 bytes into reserved text section of {tsr.ReferenceLength} bytes");
                                            case 8:
                                                // 8 to 8, straight array copy.
                                                if (typeof(TAddrSize) == typeof(UInt32))
                                                    Array.Copy(dataSegment.ToArray(), (long)((UInt32)dataSymbolAddress - (UInt32)dataSegmentBase), textSegment, (long)(UInt32)tsr.TextSegmentReferenceOffset, 8);
                                                else
                                                    Array.Copy(dataSegment.ToArray(), (long)((UInt64)dataSymbolAddress - (UInt64)dataSegmentBase), textSegment, (long)(UInt64)tsr.TextSegmentReferenceOffset, 8);
                                                break;
                                            default:
                                                throw new NotImplementedException();
                                        }
                                        break;
                                    }
                                default:
                                    throw new NotImplementedException($"Cannot handle symbol of length: {dataSymbol.Length}");
                            }
                        }
                        else
                        {
                            // This is a reference, write it's address into the text.
                            var dataSymbolAddressBytes = (typeof(TAddrSize) == typeof(UInt32))
                                ? BitConverter.GetBytes((UInt32)dataSymbolAddress)
                                : BitConverter.GetBytes((UInt64)dataSymbolAddress);
                            if (dataSymbolAddressBytes.Length != tsr.ReferenceLength)
                                throw new InvalidOperationException($"Address size reserved for symbol {tsr.Name} is {tsr.ReferenceLength}, but needed {dataSymbolAddressBytes.Length}");
                            if (typeof(TAddrSize) == typeof(UInt32))
                                Array.Copy(dataSymbolAddressBytes, (long)0, textSegment, (long)(UInt32)tsr.TextSegmentReferenceOffset, tsr.ReferenceLength);
                            else
                                Array.Copy(dataSymbolAddressBytes, (long)0, textSegment, (long)(UInt64)tsr.TextSegmentReferenceOffset, tsr.ReferenceLength);
                            Console.Out.WriteLine($"\tDS {tsr.Name}->{dataSymbolAddress}");
                        }
                    }
                }

                // Perform BSS reference replacements
                if (bssSymbols != null)
                {
                    if (textSegment == null)
                        throw new InvalidOperationException("Text segment is null when attempting to perform BSS replacements");
                    if (textSegmentSize == null)
                        throw new InvalidOperationException("Text segment size is null when attempting to perform BSS replacements");
                    if (dataSegmentSize == null)
                        throw new InvalidOperationException("Data segment size is null when attempting to perform BSS replacements");

                    var tsrBss = textSymbolReferenceOffsets.Where(tsr => bssSymbols.Exists(bss => string.Compare(bss.name, tsr.Name, StringComparison.InvariantCultureIgnoreCase) == 0)).ToArray();
                    foreach (var tsr in tsrBss)
                    {
                        var bss = bssSymbols.Single(bss => string.Compare(bss.name, tsr.Name, StringComparison.InvariantCultureIgnoreCase) == 0);
                        var bssIndex = bssSymbols.IndexOf(bss);
                        ValueType bssOffset;
                        if (typeof(TAddrSize) == typeof(UInt32))
                        {
                            bssOffset = (UInt32)textSegmentBase + textSegmentSize.Value + dataSegmentSize.Value + (UInt32)bssSymbols.Take(bssIndex).Sum(b => b.Size());
                            for (var i = 0; i < 4; i++)
                                if (textSegment[(long)((UInt32)tsr.TextSegmentReferenceOffset + i)] != 0xFF)
                                    throw new InvalidOperationException($"Attempted to overwrite placeholder for {tsr.Name} which did not contain placeholder values!");
                            Array.Copy(BitConverter.GetBytes((UInt32)bssOffset), (long)0, textSegment, (long)(UInt32)tsr.TextSegmentReferenceOffset, 4);
                        }
                        else
                        {
                            bssOffset = (UInt64)textSegmentBase + textSegmentSize.Value + dataSegmentSize.Value + (UInt64)bssSymbols.Take(bssIndex).Sum(b => b.Size());
                            for (var i = 0; i < 4; i++)
                                if (textSegment[(long)((UInt64)tsr.TextSegmentReferenceOffset + (UInt64)i)] != 0xFF)
                                    throw new InvalidOperationException($"Attempted to overwrite placeholder for {tsr.Name} which did not contain placeholder values!");
                            Array.Copy(BitConverter.GetBytes((UInt64)bssOffset), (long)0, textSegment, (long)(UInt64)tsr.TextSegmentReferenceOffset, 4);
                        }
                        Console.Out.WriteLine($"\tBSS {bss.name}->{bssOffset}");
                    }
                }
            }

            if (textSegmentSize == null)
                throw new InvalidOperationException("Text segment size unknown at the end of compilation");
            if (entryPoint == null)
                throw new InvalidOperationException("Entry point unknown at the end of compilation");
            if (textSegment == null)
                throw new InvalidOperationException("Text segment null at the end of compilation");

            return typeof(TAddrSize) == typeof(UInt32)
                                        ? (ICompilationResult)new CompilationResult32(
                                textSegmentSize.Value,
                                dataSegmentSize ?? 0,
                                bssSegmentSize ?? 0,
                                (UInt32)entryPoint,
                                (UInt32)textSegmentBase,
                                (UInt32)dataSegmentBase,
                                textSegment,
                                textLabelsOffsets!.ToDictionary(tlo => tlo.Key, tlo => (UInt32)tlo.Value),
                                textSymbolReferenceOffsets!.Select(tsr => new BytecodeTextSymbol32(tsr.Name, (UInt32)tsr.TextSegmentInstructionOffset, (UInt32)tsr.TextSegmentReferenceOffset, tsr.ReferenceLength)),
                                dataSegment,
                                dataSymbolOffsets!.ToDictionary(ds => ds.Key, ds => new BytecodeDataSymbol32((UInt32)ds.Value.DataSegmentOffset, ds.Value.Length, ds.Value.Constant)),
                                bssSymbols!,
                                errors)
                            : (ICompilationResult)new CompilationResult64(
                                textSegmentSize.Value,
                                dataSegmentSize ?? 0,
                                bssSegmentSize ?? 0,
                                (UInt64)entryPoint,
                                (UInt64)textSegmentBase,
                                (UInt64)dataSegmentBase,
                                textSegment,
                                textLabelsOffsets!.ToDictionary(tlo => tlo.Key, tlo => (UInt64)tlo.Value),
                                textSymbolReferenceOffsets!.Select(tsr => new BytecodeTextSymbol64(tsr.Name, (UInt64)tsr.TextSegmentInstructionOffset, (UInt64)tsr.TextSegmentReferenceOffset, tsr.ReferenceLength)),
                                dataSegment,
                                dataSymbolOffsets!.ToDictionary(ds => ds.Key, ds => new BytecodeDataSymbol64((UInt64)ds.Value.DataSegmentOffset, ds.Value.Length, ds.Value.Constant)),
                                bssSymbols!,
                                errors);
        }


        private ICompileTextSectionResult CompileTextSectionLinesToBytecode(
            IEnumerable<string> programLines)
        {
            ValueType offsetBytes = typeof(TAddrSize) == typeof(UInt32) ? (ValueType)(UInt32)0 : (ValueType)(UInt64)0;
            var bytecode = new List<byte>();
            var labelsOffsets = new Dictionary<string, ValueType>();
            var symbolReferenceOffsets = new List<IBytecodeTextSymbol>();

            foreach (var programLine in programLines)
            {
                // Knock off any comments
                var line = programLine.Split(';')[0].TrimEnd();

                // Fix any missing whitespace between type operators and brackets.
                line = line.Replace("BYTE[", "BYTE [", StringComparison.InvariantCultureIgnoreCase)
                            .Replace("BYTE PTR[", "BYTE PTR [", StringComparison.InvariantCultureIgnoreCase)
                            .Replace("WORD[", "WORD [", StringComparison.InvariantCultureIgnoreCase)
                            .Replace("WORD PTR[", "WORD PTR [", StringComparison.InvariantCultureIgnoreCase)
                            .Replace("DWORD[", "DWORD [", StringComparison.InvariantCultureIgnoreCase)
                            .Replace("DWORD PTR[", "DWORD PTR [", StringComparison.InvariantCultureIgnoreCase)
                            .Replace("QWORD[", "QWORD [", StringComparison.InvariantCultureIgnoreCase)
                            .Replace("QWORD PTR[", "QWORD PTR [", StringComparison.InvariantCultureIgnoreCase);

                var lineParts = line.TrimStart(' ', '\t').Split(new char[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();

                // Ignore whitespace between the first token and the second if the second is a colon.  Poorly formatted label.
                if (lineParts.Count > 2 && lineParts[1].Length == 1 && lineParts[1][0] == ':')
                {
                    var respin = new List<string>(new string[] { lineParts.Take(2).Aggregate((c, n) => c + n) });
                    respin.AddRange(lineParts.Skip(2));
                    lineParts = respin.ToList();
                }

                // Parse label
                if (lineParts[0].EndsWith(':'))
                {
                    if (typeof(TAddrSize) == typeof(UInt32))
                        labelsOffsets.Add(lineParts[0].TrimEnd(':'), (UInt32)offsetBytes);
                    else
                        labelsOffsets.Add(lineParts[0].TrimEnd(':'), (UInt64)offsetBytes);

                    if (lineParts.Count == 1)
                        continue;

                    lineParts = lineParts.Skip(1).ToList();
                }

                // Parse out type hints
                byte? typeHintSize = null;
                if (lineParts.Count > 1)
                {
                    if (string.Compare(lineParts[1], "BYTE", StringComparison.InvariantCultureIgnoreCase) == 0)
                    {
                        typeHintSize = 1;
                        lineParts.RemoveAt(1);
                        if (lineParts.Count > 2 && string.Compare(lineParts[1], "PTR", StringComparison.InvariantCultureIgnoreCase) == 0)
                            lineParts.RemoveAt(1);
                    }
                    else if (string.Compare(lineParts[1], "WORD", StringComparison.InvariantCultureIgnoreCase) == 0)
                    {
                        typeHintSize = 2;
                        lineParts.RemoveAt(1);
                        if (lineParts.Count > 2 && string.Compare(lineParts[1], "PTR", StringComparison.InvariantCultureIgnoreCase) == 0)
                            lineParts.RemoveAt(1);
                    }
                    else if (string.Compare(lineParts[1], "DWORD", StringComparison.InvariantCultureIgnoreCase) == 0)
                    {
                        typeHintSize = 4;
                        lineParts.RemoveAt(1);
                        if (lineParts.Count > 2 && string.Compare(lineParts[1], "PTR", StringComparison.InvariantCultureIgnoreCase) == 0)
                            lineParts.RemoveAt(1);
                    }
                    else if (string.Compare(lineParts[1], "QWORD", StringComparison.InvariantCultureIgnoreCase) == 0)
                    {
                        typeHintSize = 8;
                        lineParts.RemoveAt(1);
                        if (lineParts.Count > 2 && string.Compare(lineParts[1], "PTR", StringComparison.InvariantCultureIgnoreCase) == 0)
                            lineParts.RemoveAt(1);
                    }
                }

                var instruction = lineParts[0].ToUpperInvariant();

                // "Simple" assembly
                if (instruction == "END")
                {
                    bytecode.Add((byte)Bytecode.END);
                    offsetBytes = offsetBytes.Add<TAddrSize>(1);
                }
                else if (instruction == "INT")
                {
                    var operand = lineParts[lineParts.Count - 1];
                    var operandType = AssemblerUtility.GetOperandType(operand);
                    if (operandType != ParameterType.Constant)
                        throw new Exception($"ERROR: Unable to parse INT operand, expected a constant: {line}");

                    bytecode.Add((byte)Bytecode.INT);
                    offsetBytes = offsetBytes.Add<TAddrSize>(1);
                    bytecode.Add(operand.ParseByteConstant());
                    offsetBytes = offsetBytes.Add<TAddrSize>(1);
                    continue;
                }
                else if (instruction == "SYSCALL")
                {
                    bytecode.Add((byte)Bytecode.SYSCALL);
                    offsetBytes = offsetBytes.Add<TAddrSize>(1);
                    continue;
                }
                else if (instruction == "MOV")
                {
                    var dst = lineParts[lineParts.Count - 2].ToUpperInvariant();
                    var dstType = AssemblerUtility.GetOperandType(dst);
                    var src = lineParts[lineParts.Count - 1].ToUpperInvariant();
                    var srcType = AssemblerUtility.GetOperandType(src);

                    switch (dstType)
                    {
                        case ParameterType.RegisterReference:
                            {
                                switch (srcType)
                                {
                                    case ParameterType.RegisterReference:
                                        {
                                            bytecode.Add((byte)Bytecode.MOV_REG_REG);
                                            offsetBytes = offsetBytes.Add<TAddrSize>(1);

                                            bytecode.Add((byte)registers[dst]);
                                            offsetBytes = offsetBytes.Add<TAddrSize>(1);
                                            bytecode.Add((byte)registers[src]);
                                            offsetBytes = offsetBytes.Add<TAddrSize>(1);
                                            continue;
                                        }
                                    case ParameterType.RegisterAddress:
                                        {
                                            bytecode.Add((byte)Bytecode.MOV_REG_MEM);
                                            offsetBytes = offsetBytes.Add<TAddrSize>(1);

                                            bytecode.Add((byte)registers[dst]);
                                            offsetBytes = offsetBytes.Add<TAddrSize>(1);
                                            bytecode.Add((byte)registers[src.TrimStart('[').TrimEnd(']')]);
                                            offsetBytes = offsetBytes.Add<TAddrSize>(1);
                                            continue;
                                        }
                                    case ParameterType.Variable:
                                        {
                                            ValueType instructionOffset = offsetBytes;
                                            bytecode.Add((byte)Bytecode.MOV_REG_MEM);
                                            offsetBytes = offsetBytes.Add<TAddrSize>(1);

                                            var regDst = registers[dst.ToUpperInvariant()];
                                            bytecode.Add((byte)regDst);
                                            offsetBytes = offsetBytes.Add<TAddrSize>(1);

                                            IBytecodeTextSymbol textSymbol = typeof(TAddrSize) == typeof(UInt32)
                                                ? (IBytecodeTextSymbol)new BytecodeTextSymbol32(
                                                     src,
                                                     (UInt32)instructionOffset,
                                                     (UInt32)offsetBytes,
                                                    (typeHintSize == 8 || (!typeHintSize.HasValue && regDst.Size() == 8)) ? (byte)8 :
                                                    ((typeHintSize == 4 || (!typeHintSize.HasValue && regDst.Size() == 4)) ? (byte)4 :
                                                    ((typeHintSize == 2 || (!typeHintSize.HasValue && regDst.Size() == 2)) ? (byte)2 :
                                                    ((typeHintSize == 1 || (!typeHintSize.HasValue && regDst.Size() == 1)) ? (byte)1 : (byte)0)))
                                                )
                                                : (IBytecodeTextSymbol)new BytecodeTextSymbol64(
                                                     src,
                                                     (UInt64)instructionOffset,
                                                     (UInt64)offsetBytes,
                                                    (typeHintSize == 8 || (!typeHintSize.HasValue && regDst.Size() == 8)) ? (byte)8 :
                                                    ((typeHintSize == 4 || (!typeHintSize.HasValue && regDst.Size() == 4)) ? (byte)4 :
                                                    ((typeHintSize == 2 || (!typeHintSize.HasValue && regDst.Size() == 2)) ? (byte)2 :
                                                    ((typeHintSize == 1 || (!typeHintSize.HasValue && regDst.Size() == 1)) ? (byte)1 : (byte)0)))
                                                );

                                            if (textSymbol.ReferenceLength == 0)
                                                throw new InvalidOperationException($"Unable to determine register length: {regDst}");

                                            for (var i = 0; i < textSymbol.ReferenceLength; i++)
                                                bytecode.Add((byte)0xFF); // UNRESOLVED SYMBOL FOR VARIABLE

                                            symbolReferenceOffsets.Add(textSymbol);
                                            offsetBytes = offsetBytes.Add<TAddrSize>(textSymbol.ReferenceLength);
                                            continue;
                                        }
                                    case ParameterType.Constant:
                                        {
                                            bytecode.Add((byte)Bytecode.MOV_REG_CON);
                                            offsetBytes = offsetBytes.Add<TAddrSize>(1);

                                            var dstReg = registers[dst.ToUpperInvariant()];
                                            bytecode.Add((byte)dstReg);
                                            offsetBytes = offsetBytes.Add<TAddrSize>(1);

                                            if (typeHintSize == 8 || (!typeHintSize.HasValue && dstReg.Size() == 8))
                                            {
                                                bytecode.AddRange(BitConverter.GetBytes(src.ParseUInt64Constant()));
                                                offsetBytes = offsetBytes.Add<TAddrSize>(8);
                                            }
                                            else if (typeHintSize == 4 || (!typeHintSize.HasValue && dstReg.Size() == 4))
                                            {
                                                bytecode.AddRange(BitConverter.GetBytes(src.ParseUInt32Constant()));
                                                offsetBytes = offsetBytes.Add<TAddrSize>(4);
                                            }
                                            else if (typeHintSize == 2 || (!typeHintSize.HasValue && dstReg.Size() == 2))
                                            {
                                                bytecode.AddRange(BitConverter.GetBytes(src.ParseUInt16Constant()));
                                                offsetBytes = offsetBytes.Add<TAddrSize>(2);
                                            }
                                            else if (typeHintSize == 1 || (!typeHintSize.HasValue && dstReg.Size() == 1))
                                            {
                                                bytecode.Add(src.ParseByteConstant());
                                                offsetBytes = offsetBytes.Add<TAddrSize>(1);
                                            }
                                            else
                                                throw new InvalidOperationException($"Unable to determin destination register type: {dstReg}");

                                            continue;
                                        }
                                    default:
                                        throw new Exception($"ERROR: Unable to parse MOV parameters into an opcode, unhandled src type: {line}");
                                }
                            }
                        case ParameterType.VariableAddress:
                            {
                                switch (srcType)
                                {
                                    case ParameterType.Constant:
                                        {
                                            bytecode.Add((byte)Bytecode.MOV_MEM_CON);
                                            offsetBytes = offsetBytes.Add<TAddrSize>(1);

                                            // TODO: HOW BIG?
                                            if (typeHintSize == null)
                                                throw new InvalidOperationException("I can't handle unhinted variable loads yet.  I should scan DS!");

                                            symbolReferenceOffsets.Add(
                                                (typeof(TAddrSize) == typeof(UInt32))
                                                ? (IBytecodeTextSymbol)new BytecodeTextSymbol32(
                                                     dst.Substring(1, dst.Length - 2), // Strip brackets
                                                     (UInt32)offsetBytes - 1,
                                                     (UInt32)offsetBytes,
                                                     typeHintSize ?? 4
                                                )
                                                : (IBytecodeTextSymbol)new BytecodeTextSymbol64
                                                (
                                                     dst.Substring(1, dst.Length - 2), // Strip brackets
                                                     (UInt64)offsetBytes - 1,
                                                     (UInt64)offsetBytes,
                                                     typeHintSize ?? 4
                                                ));

                                            for (var i = 0; i < typeHintSize!.Value; i++)
                                                bytecode.Add((byte)0xFF); // UNRESOLVED SYMBOL FOR VARIABLE
                                            offsetBytes = offsetBytes.Add<TAddrSize>(typeHintSize.Value);

                                            var variableSize = typeHintSize.Value;
                                            switch (variableSize)
                                            {
                                                case 8:
                                                    bytecode.AddRange(BitConverter.GetBytes(src.ParseUInt64Constant()));
                                                    break;
                                                case 4:
                                                    bytecode.AddRange(BitConverter.GetBytes(src.ParseUInt32Constant()));
                                                    break;
                                                case 2:
                                                    bytecode.AddRange(BitConverter.GetBytes(src.ParseUInt16Constant()));
                                                    break;
                                                case 1:
                                                    bytecode.Add(src.ParseByteConstant());
                                                    break;
                                                default:
                                                    throw new InvalidOperationException();
                                            }
                                            offsetBytes = offsetBytes.Add<TAddrSize>(variableSize);
                                            continue;
                                        }
                                    default:
                                        throw new NotImplementedException();
                                }

                                throw new NotImplementedException();
                            }
                        default:
                            throw new Exception($"ERROR: Unable to parse MOV parameters into an opcode, unhandled dst type: {line}");
                    }

                    throw new Exception($"ERROR: Unable to parse MOV parameters into an opcode: {line}");
                }
                else if (instruction == "POP")
                {
                    var pbc = Pop(lineParts[lineParts.Count - 1]);
                    bytecode.AddRange(pbc);
                    offsetBytes = offsetBytes.Add<TAddrSize>(pbc.Length);
                }
                else if (instruction == "PUSH")
                {
                    var operand = lineParts[lineParts.Count - 1];
                    var operandType = AssemblerUtility.GetOperandType(operand);

                    switch (operandType)
                    {
                        case ParameterType.RegisterReference:
                            {
                                bytecode.Add((byte)Bytecode.PUSH_REG);
                                offsetBytes = offsetBytes.Add<TAddrSize>(1);
                                bytecode.Add((byte)registers[operand]);
                                offsetBytes = offsetBytes.Add<TAddrSize>(1);
                                continue;
                            }
                        case ParameterType.RegisterAddress:
                            {
                                bytecode.Add((byte)Bytecode.PUSH_MEM);
                                offsetBytes = offsetBytes.Add<TAddrSize>(1);
                                bytecode.Add((byte)registers[operand.TrimStart('[').TrimEnd(']')]);
                                offsetBytes = offsetBytes.Add<TAddrSize>(1);
                                continue;
                            }
                        case ParameterType.Constant:
                            {
                                bytecode.Add((byte)Bytecode.PUSH_CON);
                                offsetBytes = offsetBytes.Add<TAddrSize>(1);
                                bytecode.AddRange(BitConverter.GetBytes(operand.ParseUInt32Constant()));
                                offsetBytes = offsetBytes.Add<TAddrSize>(4);
                                continue;
                            }
                        default:
                            throw new Exception($"ERROR: Unable to parse PUSH parameters into an opcode, unhandled operand type: {line}");
                    }

                    throw new Exception($"ERROR: Unable to parse PUSH parameters into an opcode: {line}");
                }
                else if (instruction == "ADD")
                {
                    var abc = Add(typeHintSize, lineParts[lineParts.Count - 2], lineParts[lineParts.Count - 1]);
                    bytecode.AddRange(abc);
                    offsetBytes = offsetBytes.Add<TAddrSize>(abc.Length);
                }
                else if (instruction == "AND")
                {
                    var abc = And(typeHintSize, lineParts[lineParts.Count - 2], lineParts[lineParts.Count - 1]);
                    bytecode.AddRange(abc);
                    offsetBytes = offsetBytes.Add<TAddrSize>(abc.Length);
                }
                else if (string.Compare("XOR", instruction, StringComparison.InvariantCulture) == 0)
                {
                    var abc = XOr(lineParts[lineParts.Count - 2], lineParts[lineParts.Count - 1]);
                    bytecode.AddRange(abc);
                    offsetBytes = offsetBytes.Add<TAddrSize>(abc.Length);
                }
                else if (
                    string.Compare("JZ", instruction, StringComparison.InvariantCulture) == 0 ||
                    string.Compare("JMP", instruction, StringComparison.InvariantCulture) == 0)
                {
                    var operand = lineParts[lineParts.Count - 1];

                    if (string.Compare("JZ", instruction, StringComparison.InvariantCulture) == 0)
                        bytecode.Add((byte)Bytecode.JZ);
                    else if (string.Compare("JMP", instruction, StringComparison.InvariantCulture) == 0)
                        bytecode.Add((byte)Bytecode.JMP);
                    offsetBytes = offsetBytes.Add<TAddrSize>(1);

                    var textSymbol = typeof(TAddrSize) == typeof(UInt32)
                     ? (IBytecodeTextSymbol)new BytecodeTextSymbol32(operand, (UInt32)offsetBytes - 1, (UInt32)offsetBytes, typeHintSize ?? 4)
                     : (IBytecodeTextSymbol)new BytecodeTextSymbol64(operand, (UInt64)offsetBytes - 1, (UInt64)offsetBytes, typeHintSize ?? 4);

                    for (var i = 0; i < textSymbol.ReferenceLength; i++)
                        bytecode.Add((byte)0xEE); // UNRESOLVED SYMBOL FOR LABEL

                    symbolReferenceOffsets.Add(textSymbol);
                    offsetBytes = offsetBytes.Add<TAddrSize>(textSymbol.ReferenceLength);
                }
                else
                    throw new Exception($"ERROR: Cannot compile: {line}");
            }

            if (typeof(TAddrSize) == typeof(UInt32))
                return new CompileTextSectionResult32(bytecode.ToArray(), labelsOffsets.ToDictionary(k => k.Key, v => (UInt32)v.Value), symbolReferenceOffsets.Cast<BytecodeTextSymbol32>());
            else
                return new CompileTextSectionResult64(bytecode.ToArray(), labelsOffsets.ToDictionary(k => k.Key, v => (UInt64)v.Value), symbolReferenceOffsets.Cast<BytecodeTextSymbol64>());
        }

        private ICompileDataSectionResult CompileDataSectionLines(IEnumerable<string> dataLines)
        {
            ValueType offsetBytes = typeof(TAddrSize) == typeof(UInt32) ? (ValueType)(UInt32)0 : (ValueType)(UInt64)0;

            var bytecode = new List<byte>();
            var symbolOffsets = new Dictionary<string, IBytecodeDataSymbol>();

            foreach (var dataLine in dataLines)
            {
                // Knock off any comments
                var line = dataLine.Split(';')[0].Trim();
                var dataAllocationDirective = CompilerDataAllocationDirective.ParseLine(line);

                if (string.Compare("db", dataAllocationDirective.Mnemonic, StringComparison.InvariantCultureIgnoreCase) == 0)
                {
                    var operands = dataAllocationDirective.Operands.Select(o => AssemblerUtility.UnboxParsedOperand(o)).ToArray();
                    foreach (var operand in operands)
                    {
                        var ov = (operand is string && string.Compare((string)operand, "$", StringComparison.InvariantCulture) == 0) ? (byte)0x00 : operand;

                        if (ov.GetType() == typeof(string))
                        {
                            var stringBytes = System.Text.Encoding.ASCII.GetBytes((string)ov);
                            bytecode.AddRange(stringBytes);

                            if (dataAllocationDirective.Label != null && !symbolOffsets.ContainsKey(dataAllocationDirective.Label.ToUpperInvariant()))
                                symbolOffsets.Add(dataAllocationDirective.Label.ToUpperInvariant(),
                                typeof(TAddrSize) == typeof(UInt32)
                                ? (IBytecodeDataSymbol)new BytecodeDataSymbol32((UInt32)offsetBytes, (ushort)stringBytes.Length, false)
                                : (IBytecodeDataSymbol)new BytecodeDataSymbol64((UInt64)offsetBytes, (ushort)stringBytes.Length, false));

                            offsetBytes = offsetBytes.Add<TAddrSize>(stringBytes.Length);
                            continue;
                        }

                        if (ov.GetType() == typeof(byte))
                        {
                            bytecode.Add((byte)ov);

                            if (dataAllocationDirective.Label != null && !symbolOffsets.ContainsKey(dataAllocationDirective.Label.ToUpperInvariant()))
                                symbolOffsets.Add(dataAllocationDirective.Label.ToUpperInvariant(),
                                typeof(TAddrSize) == typeof(UInt32)
                                ? (IBytecodeDataSymbol)new BytecodeDataSymbol32((UInt32)offsetBytes, 1, false)
                                : (IBytecodeDataSymbol)new BytecodeDataSymbol64((UInt64)offsetBytes, 1, false));

                            offsetBytes = offsetBytes.Add<TAddrSize>(1);
                            continue;
                        }

                        throw new InvalidOperationException($"Unable to encode operand to data bytes: {operand}");
                    }
                }
                else if (string.Compare("dq", dataAllocationDirective.Mnemonic, StringComparison.InvariantCultureIgnoreCase) == 0)
                {
                    var operands = dataAllocationDirective.Operands.Select(o => AssemblerUtility.UnboxParsedOperand(o)).ToArray();
                    foreach (var operand in operands)
                    {
                        var ov = operand;

                        if (ov.GetType() == typeof(string))
                        {
                            if (double.TryParse((string)ov, out double ovDouble))
                                ov = ovDouble;
                            else if (float.TryParse((string)ov, out float ovFloat))
                                ov = ovFloat;
                            else
                                throw new InvalidOperationException($"Unable to parse string as numeric value: {ov}");
                        }

                        if (ov.GetType() == typeof(double))
                        {
                            var longBytes = BitConverter.GetBytes((double)ov); // This is an array of 8 bytes
                            bytecode.AddRange(longBytes);
                            offsetBytes = offsetBytes.Add<TAddrSize>(8);
                            continue;
                        }
                        else if (ov.GetType() == typeof(float))
                        {
                            var longBytes = BitConverter.GetBytes(Convert.ToDouble((float)ov)); // This is an array of 8 bytes
                            bytecode.AddRange(longBytes);
                            offsetBytes = offsetBytes.Add<TAddrSize>(8);
                            continue;
                        }
                        else if (ov.GetType() == typeof(byte))
                        {
                            var longBytes = BitConverter.GetBytes(Convert.ToUInt64((byte)ov)); // This is an array of 8 bytes
                            bytecode.AddRange(longBytes);
                            offsetBytes = offsetBytes.Add<TAddrSize>(8);
                            continue;
                        }

                        throw new InvalidOperationException($"Unable to encode operand to data bytes: {operand}");
                    }
                }
                else if (string.Compare("equ", dataAllocationDirective.Mnemonic, StringComparison.InvariantCultureIgnoreCase) == 0)
                {
                    // Convert infix to RPN for easy processing
                    var operands = dataAllocationDirective.Operands;
                    var rpn = CompilerDataAllocationDirective.ConvertInfixToReversePolishNotation<TAddrSize>(operands, offsetBytes);
                    var computeStack = new Stack<ValueType>();

                    while (rpn.Count > 0)
                    {
                        var next = rpn.Dequeue();
                        var nextValue = next as ValueType;
                        if (nextValue == null && AssemblerUtility.TryResolveDataAllocationReference((string)next, symbolOffsets, out ValueType nv))
                            nextValue = nv;

                        if (nextValue == null && string.CompareOrdinal("+", (string)next) == 0)
                        {
                            var b = computeStack.Pop();
                            var a = computeStack.Pop();
                            if (a is byte && b is byte)
                                computeStack.Push((byte)a + (byte)b);
                            else if (a is ushort && b is ushort)
                                computeStack.Push((ushort)a + (ushort)b);
                            else if (a is uint && b is uint)
                                computeStack.Push((uint)a + (uint)b);
                            else if (a is ulong && b is ulong)
                                computeStack.Push((ulong)a + (ulong)b);
                            else
                                throw new InvalidOperationException($"Unable to handle addition of {a.GetType().Name} and {b.GetType().Name}");
                            continue;
                        }
                        else if (nextValue == null && string.CompareOrdinal("-", (string)next) == 0)
                        {
                            var b = computeStack.Pop();
                            var a = computeStack.Pop();
                            if (a is byte && b is byte)
                                computeStack.Push((byte)((byte)a - (byte)b));
                            else if (a is ushort && b is ushort)
                                computeStack.Push((ushort)((ushort)a - (ushort)b));
                            else if (a is uint && b is uint)
                                computeStack.Push((uint)((uint)a - (uint)b));
                            else if (a is ulong && b is ulong)
                                computeStack.Push((ulong)((ulong)a - (ulong)b));
                            else if (a is ulong && b is uint)
                                computeStack.Push((ulong)((ulong)a - (uint)b));
                            else if (a is ushort && b is uint)
                                computeStack.Push((uint)((ushort)a - (uint)b));
                            else if (a is uint && b is ushort)
                                computeStack.Push((ushort)((uint)a - (ushort)b));
                            else
                                throw new InvalidOperationException($"Unable to handle subtraction of {a.GetType().Name} and {b.GetType().Name}");
                            continue;
                        }
                        else
                        {
                            if (nextValue == null)
                                throw new InvalidOperationException("Missing value type in nextValue");
                            computeStack.Push(nextValue);
                        }
                    }

                    if (computeStack.Count != 1)
                        throw new InvalidOperationException("At the end of the EQU calculation, exactly one result should be on internal stack");

                    var ov = computeStack.Pop();
                    var ovType = ov.GetType();
                    if (ovType == typeof(ValueType))
                        ovType = typeof(TAddrSize);

                    if (ovType == typeof(ulong))
                    {
                        bytecode.AddRange(BitConverter.GetBytes((ulong)ov));

                        if (dataAllocationDirective.Label != null && !symbolOffsets.ContainsKey(dataAllocationDirective.Label.ToUpperInvariant()))
                            symbolOffsets.Add(dataAllocationDirective.Label.ToUpperInvariant(),
                                typeof(TAddrSize) == typeof(UInt32)
                                ? (IBytecodeDataSymbol)new BytecodeDataSymbol32((UInt32)offsetBytes, 8, true)
                                : (IBytecodeDataSymbol)new BytecodeDataSymbol64((UInt64)offsetBytes, 8, true));

                        offsetBytes = offsetBytes.Add<TAddrSize>(8);
                        continue;
                    }
                    else if (ovType == typeof(uint))
                    {
                        bytecode.AddRange(BitConverter.GetBytes((uint)ov));

                        if (dataAllocationDirective.Label != null && !symbolOffsets.ContainsKey(dataAllocationDirective.Label.ToUpperInvariant()))
                            symbolOffsets.Add(dataAllocationDirective.Label.ToUpperInvariant(),
                                typeof(TAddrSize) == typeof(UInt32)
                                ? (IBytecodeDataSymbol)new BytecodeDataSymbol32((UInt32)offsetBytes, 4, true)
                                : (IBytecodeDataSymbol)new BytecodeDataSymbol64((UInt64)offsetBytes, 4, true));

                        offsetBytes = offsetBytes.Add<TAddrSize>(4);
                        continue;
                    }
                    else if (ovType == typeof(ushort))
                    {
                        bytecode.AddRange(BitConverter.GetBytes((ushort)ov));

                        if (dataAllocationDirective.Label != null && !symbolOffsets.ContainsKey(dataAllocationDirective.Label.ToUpperInvariant()))
                            symbolOffsets.Add(dataAllocationDirective.Label.ToUpperInvariant(),
                            typeof(TAddrSize) == typeof(UInt32)
                            ? (IBytecodeDataSymbol)new BytecodeDataSymbol32((UInt32)offsetBytes, 2, true)
                            : (IBytecodeDataSymbol)new BytecodeDataSymbol64((UInt64)offsetBytes, 2, true));

                        offsetBytes = offsetBytes.Add<TAddrSize>(2);
                        continue;
                    }
                    else if (ovType == typeof(byte))
                    {
                        bytecode.Add((byte)ov);

                        if (dataAllocationDirective.Label != null && !symbolOffsets.ContainsKey(dataAllocationDirective.Label.ToUpperInvariant()))
                            symbolOffsets.Add(dataAllocationDirective.Label.ToUpperInvariant(),
                                (typeof(TAddrSize) == typeof(UInt32))
                                ? (IBytecodeDataSymbol)new BytecodeDataSymbol32((UInt32)offsetBytes, 1, true)
                                : (IBytecodeDataSymbol)new BytecodeDataSymbol64((UInt64)offsetBytes, 1, true));

                        offsetBytes = offsetBytes.Add<TAddrSize>(1);
                        continue;
                    }

                    throw new InvalidOperationException($"Unable to encode result to data bytes: {ov}({ov.GetType().Name})");
                }
                else
                    throw new InvalidOperationException($"Unknown mnemonic: {dataAllocationDirective.Mnemonic}");
            }

            if (typeof(TAddrSize) == typeof(UInt32))
                return new CompileDataSectionResult32(bytecode.ToArray(), symbolOffsets.ToDictionary(k => k.Key, v => (BytecodeDataSymbol32)v.Value));
            else
                return new CompileDataSectionResult64(bytecode.ToArray(), symbolOffsets.ToDictionary(k => k.Key, v => (BytecodeDataSymbol64)v.Value));
        }

        private byte[] Add(byte? typeHintSize, string operand1, string operand2)
        {
            var o1Type = AssemblerUtility.GetOperandType(operand1);
            var o2Type = AssemblerUtility.GetOperandType(operand2);

            switch (o1Type)
            {
                case ParameterType.RegisterReference:
                    {
                        var o1Reg = registers[operand1.ToUpperInvariant()];
                        switch (o2Type)
                        {
                            case ParameterType.Constant:
                                {
                                    if (typeHintSize == 8 || (!typeHintSize.HasValue && o1Reg.Size() == 8))
                                    {
                                        var ret = new byte[10];
                                        ret[0] = (byte)Bytecode.ADD_REG_CON;
                                        ret[1] = (byte)registers[operand1.ToUpperInvariant()];
                                        Array.Copy(BitConverter.GetBytes(operand2.ParseUInt32Constant()), 0, ret, 2, 8);
                                        return ret;
                                    }
                                    else if (typeHintSize == 4 || (!typeHintSize.HasValue && o1Reg.Size() == 4))
                                    {
                                        var ret = new byte[6];
                                        ret[0] = (byte)Bytecode.ADD_REG_CON;
                                        ret[1] = (byte)registers[operand1.ToUpperInvariant()];
                                        Array.Copy(BitConverter.GetBytes(operand2.ParseUInt32Constant()), 0, ret, 2, 4);
                                        return ret;
                                    }
                                    else if (typeHintSize == 2 || (!typeHintSize.HasValue && o1Reg.Size() == 2))
                                    {
                                        var ret = new byte[4];
                                        ret[0] = (byte)Bytecode.ADD_REG_CON;
                                        ret[1] = (byte)registers[operand1.ToUpperInvariant()];
                                        Array.Copy(BitConverter.GetBytes(operand2.ParseUInt16Constant()), 0, ret, 2, 2);
                                        return ret;
                                    }
                                    else if (typeHintSize == 1 || (!typeHintSize.HasValue && o1Reg.Size() == 1))
                                    {
                                        var ret = new byte[3];
                                        ret[0] = (byte)Bytecode.ADD_REG_CON;
                                        ret[1] = (byte)registers[operand1.ToUpperInvariant()];
                                        ret[2] = operand2.ParseByteConstant();
                                        return ret;
                                    }

                                    throw new NotImplementedException();
                                }
                        }
                    }
                    break;
                case ParameterType.RegisterAddress:
                    {
                        switch (o2Type)
                        {
                            case ParameterType.Constant:
                                {
                                    var ret = new byte[6];
                                    ret[0] = (byte)Bytecode.ADD_MEM_CON;
                                    ret[1] = (byte)registers[operand1.TrimStart('[').TrimEnd(']').ToUpperInvariant()];
                                    Array.Copy(BitConverter.GetBytes(operand2.ParseUInt32Constant()), 0, ret, 2, 4);
                                    return ret;
                                }
                        }
                    }
                    break;
                default:
                    throw new Exception($"ERROR: Unable to parse ADD parameters into an opcode, unhandled operand: {operand1}");
            }

            throw new Exception($"ERROR: Unable to parse ADD into an opcode");
        }

        private byte[] And(byte? typeHintSize, string operand1, string operand2)
        {
            var o1Type = AssemblerUtility.GetOperandType(operand1);
            var o2Type = AssemblerUtility.GetOperandType(operand2);

            switch (o1Type)
            {
                case ParameterType.RegisterReference:
                    {
                        var o1Reg = registers[operand1.ToUpperInvariant()];
                        switch (o2Type)
                        {
                            case ParameterType.Constant:
                                {
                                    if (typeHintSize == 8 || (!typeHintSize.HasValue && o1Reg.Size() == 8))
                                    {
                                        var ret = new byte[10];
                                        ret[0] = (byte)Bytecode.AND_REG_CON;
                                        ret[1] = (byte)registers[operand1.ToUpperInvariant()];
                                        Array.Copy(BitConverter.GetBytes(operand2.ParseUInt32Constant()), 0, ret, 2, 8);
                                        return ret;
                                    }
                                    else if (typeHintSize == 4 || (!typeHintSize.HasValue && o1Reg.Size() == 4))
                                    {
                                        var ret = new byte[6];
                                        ret[0] = (byte)Bytecode.AND_REG_CON;
                                        ret[1] = (byte)registers[operand1.ToUpperInvariant()];
                                        Array.Copy(BitConverter.GetBytes(operand2.ParseUInt32Constant()), 0, ret, 2, 4);
                                        return ret;
                                    }
                                    else if (typeHintSize == 2 || (!typeHintSize.HasValue && o1Reg.Size() == 2))
                                    {
                                        var ret = new byte[4];
                                        ret[0] = (byte)Bytecode.AND_REG_CON;
                                        ret[1] = (byte)registers[operand1.ToUpperInvariant()];
                                        Array.Copy(BitConverter.GetBytes(operand2.ParseUInt16Constant()), 0, ret, 2, 2);
                                        return ret;
                                    }
                                    else if (typeHintSize == 1 || (!typeHintSize.HasValue && o1Reg.Size() == 1))
                                    {
                                        var ret = new byte[3];
                                        ret[0] = (byte)Bytecode.AND_REG_CON;
                                        ret[1] = (byte)registers[operand1.ToUpperInvariant()];
                                        ret[2] = operand2.ParseByteConstant();
                                        return ret;
                                    }

                                    throw new NotImplementedException();
                                }
                            default:
                                throw new NotImplementedException();
                        }
                    }
                default:
                    throw new Exception($"ERROR: Unable to parse AND parameters into an opcode, unhandled operand: {operand1}");
            }

            throw new Exception($"ERROR: Unable to parse AND into an opcode");
        }

        private byte[] XOr(string operand1, string operand2)
        {
            var o1Type = AssemblerUtility.GetOperandType(operand1);
            var o2Type = AssemblerUtility.GetOperandType(operand2);

            switch (o1Type)
            {
                case ParameterType.RegisterReference:
                    {
                        var o1Reg = registers[operand1.ToUpperInvariant()];
                        switch (o2Type)
                        {
                            case ParameterType.RegisterReference:
                                {
                                    var o2Reg = registers[operand2.ToUpperInvariant()];
                                    var ret = new byte[3];
                                    ret[0] = (byte)Bytecode.XOR_REG_REG;
                                    ret[1] = (byte)registers[operand1.ToUpperInvariant()];
                                    ret[2] = (byte)registers[operand2.ToUpperInvariant()];
                                    return ret;
                                }
                            default:
                                throw new NotImplementedException();
                        }
                    }
                default:
                    throw new Exception($"ERROR: Unable to parse XOR parameters into an opcode, unhandled operand: {operand1}");
            }

            throw new Exception($"ERROR: Unable to parse XOR into an opcode");
        }

        private byte[] Pop(string operand)
        {
            var ret = new byte[2];
            var operandType = AssemblerUtility.GetOperandType(operand);

            switch (operandType)
            {
                case ParameterType.RegisterReference:
                    {
                        ret[0] = (byte)Bytecode.POP_REG;
                        ret[1] = (byte)registers[operand.ToUpperInvariant()];
                        break;
                    }
                case ParameterType.RegisterAddress:
                    {
                        ret[0] = (byte)Bytecode.POP_MEM;
                        ret[1] = (byte)registers[operand.TrimStart('[').TrimEnd(']').ToUpperInvariant()];
                        break;
                    }
                default:
                    throw new Exception($"ERROR: Unable to parse POP parameters into an opcode, unhandled operand: {operand}");
            }

            return ret;
        }

        public static string GetEnumDescription(Enum value)
        {
            var fi = value.GetType().GetField(value.ToString());
            var attributes = fi?.GetCustomAttributes(typeof(DescriptionAttribute), false) as DescriptionAttribute[];
            if (attributes != null && attributes.Any())
            {
                return attributes.First().Description;
            }

            return value.ToString();
        }
    }

}
