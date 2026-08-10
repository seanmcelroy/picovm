using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;

namespace picovm.Assembler
{
    public class BytecodeCompiler<TAddrSize> : IBytecodeCompiler
        where TAddrSize : struct, IComparable, IComparable<TAddrSize>, IConvertible, IEquatable<TAddrSize>, IFormattable
    {
        private readonly Dictionary<string, Register> registers;

        public BytecodeCompiler()
        {
            // Generate opcode dictionary
            registers = Enum.GetValues<Register>().Cast<Register>().ToDictionary(k => GetEnumDescription(k), v => v);
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
            TAddrSize zero = typeof(TAddrSize) == typeof(UInt32) ? (TAddrSize)(ValueType)(UInt32)0 : (TAddrSize)(ValueType)(UInt64)0;
            // Nullable so that a legitimate entry point at offset zero is distinguishable
            // from "no entry point resolved yet".
            TAddrSize? entryPoint = null;
            TAddrSize textSegmentBase = zero;
            TAddrSize dataSegmentBase = zero;
            byte[]? textSegment = null;
            ImmutableDictionary<string, TAddrSize> textLabelsOffsets = [];
            ImmutableList<BytecodeTextSymbol<TAddrSize>> textSymbolReferenceOffsets = [];
            ImmutableArray<byte> dataSegment = [];
            Dictionary<string, BytecodeDataSymbol<TAddrSize>> dataSymbolOffsets = [];
            ImmutableList<BytecodeBssSymbol> bssSymbols = [];
            var errors = new List<CompilationError>(10);

            // Group program lines into sections
            var sections = new Dictionary<SectionType, List<string>>();
            KeyValuePair<SectionType, List<string>> currentSection = default;

            ushort lineNumber = 0;

            var defines = new Dictionary<string, string>();
            var macros = new List<Macro>();
            var inMacroDefinition = false;
            foreach (var programLine in programLines)
            {
                lineNumber++;

                // Knock off any comments
                var line = programLine.Split(';')[0].Trim(' ', '\t');

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // Defines
                if (line.TrimStart(' ', '\t').StartsWith("%define ", StringComparison.OrdinalIgnoreCase))
                {
                    var defineParts = line[(line.IndexOf("%define ") + 8)..].Split(' ', '\t', StringSplitOptions.RemoveEmptyEntries);
                    var term = defineParts[0];
                    var definition = line[(line.IndexOf(term) + term.Length)..].TrimStart(' ', '\t');
                    defines.Add(term, definition);
                    continue;
                }
                foreach (var define in defines)
                {
                    line = line.Replace(define.Key, define.Value);
                }

                // Macros
                if (line.TrimStart(' ', '\t').StartsWith("%macro", StringComparison.OrdinalIgnoreCase))
                {
                    inMacroDefinition = true;
                    var macroParts = line[(line.IndexOf("%macro") + 6)..].Split(' ', '\t', StringSplitOptions.RemoveEmptyEntries);
                    macros.Add(new Macro(macroParts[0], byte.Parse(macroParts[1]), []));
                    continue;
                }
                else if (inMacroDefinition)
                {
                    if (line.TrimStart(' ', '\t').StartsWith("%endmacro", StringComparison.OrdinalIgnoreCase))
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
                        currentSection = new KeyValuePair<SectionType, List<string>>(SectionType.Text, []);
                        sections.Add(currentSection.Key, currentSection.Value);
                    }
                    else if (line.IndexOf(".data", StringComparison.OrdinalIgnoreCase) > -1)
                    {
                        currentSection = new KeyValuePair<SectionType, List<string>>(SectionType.Data, []);
                        sections.Add(currentSection.Key, currentSection.Value);
                    }
                    else if (line.IndexOf(".bss", StringComparison.OrdinalIgnoreCase) > -1)
                    {
                        currentSection = new KeyValuePair<SectionType, List<string>>(SectionType.BSS, []);
                        sections.Add(currentSection.Key, currentSection.Value);
                    }
                    else
                    {
                        errors.Add(new CompilationError($"Unknown section type: {line}", sourceFilename, lineNumber));
                        throw new InvalidOperationException($"Unknown section type: '{line}' ({sourceFilename}:{lineNumber})");
                    }
                    continue;
                }

                // No .section defined.  Assume we are real-mode assembly and auto-create a text section.
                if (currentSection.Equals(default(KeyValuePair<SectionType, List<string>>)))
                {
                    currentSection = new KeyValuePair<SectionType, List<string>>(SectionType.Text, []);
                    sections.Add(currentSection.Key, currentSection.Value);
                }

                // Special handling for linker directives at this layer-above.
                switch (currentSection.Key)
                {
                    case SectionType.Text:
                        if (line.StartsWith("global ", StringComparison.OrdinalIgnoreCase))
                        {
                            entryPointSymbol = line[(line.IndexOf("global ", StringComparison.OrdinalIgnoreCase) + "global ".Length)..];
                            continue;
                        }
                        break;
                }

                currentSection.Value.Add(line);
            }

            foreach (var section in sections)
            {
                switch (section.Key)
                {
                    case SectionType.Text:
                        var bytecodeGeneration = CompileTextSectionLinesToBytecode(section.Value);
                        textSegment = [.. bytecodeGeneration.Bytecode];
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
                        entryPoint = textLabelsOffsets[entryPointSymbol];
                        break;
                    case SectionType.Data:
                        var constGeneration = CompileDataSectionLines(section.Value);
                        dataSegment = constGeneration.Bytecode;
                        dataSegmentSize = (uint)constGeneration.Bytecode.Length;
                        dataSymbolOffsets = constGeneration.SymbolOffsets.ToDictionary();
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
            if (dataSegment.Length == 0 && dataSymbolOffsets.Count > 0)
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
            dataSegmentBase = textSegmentBase.Add(textSegmentSize.Value);
            foreach (var ds in dataSymbolOffsets)
            {
                BytecodeDataSymbol<TAddrSize> bds = ds.Value;
                bds.DataSegmentOffset = ds.Value.DataSegmentOffset.Add(dataSegmentBase);
                dataSymbolOffsets[ds.Key] = bds;
            }

            // Perform text/label replacements
            foreach (var tsr in textSymbolReferenceOffsets.Where(tsr => textLabelsOffsets.ContainsKey(tsr.Name)))
            {
                ValueType labelOffsetAddress = textLabelsOffsets[tsr.Name];
                var addrSize = typeof(TAddrSize) == typeof(UInt32) ? 4 : 8;
                if (addrSize != tsr.ReferenceLength)
                    throw new InvalidOperationException(
                        $"Symbol {tsr.Name} reserved a {tsr.ReferenceLength}-byte destination, but its address needs {addrSize} bytes; " +
                        $"loading an address into a register narrower than the machine's address width is not supported.");

                if (textSegment == null)
                    throw new InvalidOperationException($"Text segment is not loaded, and so symbol {tsr.Name} cannot be resolved");

                for (var i = 0; i < 4; i++)
                {
                    if (typeof(TAddrSize) == typeof(UInt32))
                    {
                        if (textSegment[Convert.ToInt32(tsr.TextSegmentReferenceOffset.Add(i))] != 0xEE)
                            throw new InvalidOperationException($"Attempted to overwrite placeholder for {tsr.Name} which did not contain placeholder values!");
                    }
                    else
                    {
                        if (textSegment[Convert.ToInt64(tsr.TextSegmentReferenceOffset.Add(i))] != 0xEE)
                            throw new InvalidOperationException($"Attempted to overwrite placeholder for {tsr.Name} which did not contain placeholder values!");
                    }
                }

                var refOffset = typeof(TAddrSize) == typeof(UInt32)
                    ? (int)Convert.ToUInt32(tsr.TextSegmentReferenceOffset)
                    : (int)Convert.ToUInt64(tsr.TextSegmentReferenceOffset);
                var dest = textSegment.AsSpan(refOffset, tsr.ReferenceLength);
                if (typeof(TAddrSize) == typeof(UInt32))
                    BinaryPrimitives.WriteUInt32LittleEndian(dest, (UInt32)labelOffsetAddress);
                else
                    BinaryPrimitives.WriteUInt64LittleEndian(dest, (UInt64)labelOffsetAddress);

                //Console.Out.WriteLine($"\tLBL {tsr.Name}->{labelOffsetAddress}");
            }

            // Perform data replacements
            if (textSymbolReferenceOffsets.Count > 0)
            {
                if (textSegment == null)
                {
                    errors.Add(new CompilationError($"Text segment null, but this is needed to resolve and replace data section variables."));
                    return new CompilationResultBase(errors);
                }

                foreach (var tsr in textSymbolReferenceOffsets.Where(tsr => dataSymbolOffsets.ContainsKey(tsr.Name)))
                {
                    var dataSymbol = dataSymbolOffsets[tsr.Name];
                    TAddrSize dataSymbolAddress = dataSymbol.DataSegmentOffset;

                    if (dataSymbol.Constant)
                    {
                        // This is a value, just write it directly into the text.
                        if (typeof(TAddrSize) == typeof(UInt32))
                        {
                            if (textSegment[(long)(UInt32)(ValueType)tsr.TextSegmentInstructionOffset] != (byte)Bytecode.MOV_IMMEDIATE)
                                throw new InvalidOperationException($"Unable to handle constant inlining of instruction: {textSegment[(long)(UInt32)(ValueType)tsr.TextSegmentInstructionOffset]} for symbol {tsr.Name}");
                        }
                        else
                        {
                            if (textSegment[(long)(UInt64)(ValueType)tsr.TextSegmentInstructionOffset] != (byte)Bytecode.MOV_IMMEDIATE)
                                throw new InvalidOperationException($"Unable to handle constant inlining of instruction: {textSegment[(long)(UInt64)(ValueType)tsr.TextSegmentInstructionOffset]} for symbol {tsr.Name}");
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
                                            ReadOnlySpan<byte> dataSpan = dataSegment.AsSpan(); // zero copy
                                            int srcOffset = (int)Convert.ToInt64(dataSymbolAddress.Sub(dataSegmentBase));
                                            int dstOffset;

                                            if (typeof(TAddrSize) == typeof(UInt32))
                                                dstOffset = (int)(UInt32)(ValueType)tsr.TextSegmentReferenceOffset;
                                            else
                                                dstOffset = (int)(UInt64)(ValueType)tsr.TextSegmentReferenceOffset;

                                            dataSpan.Slice(srcOffset, 2).CopyTo(textSegment.AsSpan(dstOffset, 2));

                                            for (var i = 0; i < 2; i++)
                                            {
                                                if (typeof(TAddrSize) == typeof(UInt32))
                                                {
                                                    if (textSegment[Convert.ToInt64(ValueTypeUtility.Add(tsr.TextSegmentReferenceOffset, i))] != 0xFF)
                                                        throw new InvalidOperationException($"Attempted to overwrite placeholder for {tsr.Name} which did not contain placeholder values!");
                                                }
                                                else
                                                {
                                                    if (textSegment[Convert.ToInt64(ValueTypeUtility.Add(tsr.TextSegmentReferenceOffset, i))] != 0xFF)
                                                        throw new InvalidOperationException($"Attempted to overwrite placeholder for {tsr.Name} which did not contain placeholder values!");

                                                }
                                            }
                                            break;
                                        case 4:
                                            // 2 to 4 upsize
                                            var dataSymbolValue = BinaryPrimitives.ReadUInt16LittleEndian(dataSegment.AsSpan(Convert.ToInt32(dataSymbolAddress.Sub(dataSegmentBase)), 2));
                                            var tsrValue = Convert.ToUInt32(dataSymbolValue);
                                            // Validate we're overwriting the right place
                                            for (var i = 0; i < 4; i++)
                                            {
                                                if (typeof(TAddrSize) == typeof(UInt32))
                                                {
                                                    if (textSegment[Convert.ToInt64(ValueTypeUtility.Add(tsr.TextSegmentReferenceOffset, i))] != 0xFF)
                                                        throw new InvalidOperationException($"Attempted to overwrite placeholder for {tsr.Name} which did not contain placeholder values!");
                                                }
                                                else
                                                {
                                                    if (textSegment[Convert.ToInt64(ValueTypeUtility.Add(tsr.TextSegmentReferenceOffset, i))] != 0xFF)
                                                        throw new InvalidOperationException($"Attempted to overwrite placeholder for {tsr.Name} which did not contain placeholder values!");
                                                }
                                            }
                                            if (typeof(TAddrSize) == typeof(UInt32))
                                                BinaryPrimitives.WriteUInt32LittleEndian(textSegment.AsSpan((int)(UInt32)(ValueType)tsr.TextSegmentReferenceOffset, 4), tsrValue);
                                            else
                                                BinaryPrimitives.WriteUInt32LittleEndian(textSegment.AsSpan((int)(UInt64)(ValueType)tsr.TextSegmentReferenceOffset, 4), tsrValue);

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
                                            var srcOffset = Convert.ToInt32(dataSymbolAddress.Sub(dataSegmentBase));
                                            var dstOffset = typeof(TAddrSize) == typeof(UInt32)
                                                ? (int)(UInt32)(ValueType)tsr.TextSegmentReferenceOffset
                                                : (int)(UInt64)(ValueType)tsr.TextSegmentReferenceOffset;
                                            dataSegment.AsSpan(srcOffset, 4).CopyTo(textSegment.AsSpan(dstOffset, 4));
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
                                            var srcOffset = Convert.ToInt32(dataSymbolAddress.Sub(dataSegmentBase));
                                            var dstOffset = typeof(TAddrSize) == typeof(UInt32)
                                                ? (int)(UInt32)(ValueType)tsr.TextSegmentReferenceOffset
                                                : (int)(UInt64)(ValueType)tsr.TextSegmentReferenceOffset;
                                            dataSegment.AsSpan(srcOffset, 8).CopyTo(textSegment.AsSpan(dstOffset, 8));
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
                        var addrSize = typeof(TAddrSize) == typeof(UInt32) ? 4 : 8;
                        if (addrSize != tsr.ReferenceLength)
                            throw new InvalidOperationException(
                                $"Symbol {tsr.Name} reserved a {tsr.ReferenceLength}-byte destination, but its address needs {addrSize} bytes; " +
                                $"loading an address into a register narrower than the machine's address width is not supported.");

                        var refOffset = typeof(TAddrSize) == typeof(UInt32)
                            ? (int)(UInt32)(ValueType)tsr.TextSegmentReferenceOffset
                            : (int)(UInt64)(ValueType)tsr.TextSegmentReferenceOffset;
                        var dest = textSegment.AsSpan(refOffset, tsr.ReferenceLength);
                        if (typeof(TAddrSize) == typeof(UInt32))
                            BinaryPrimitives.WriteUInt32LittleEndian(dest, (UInt32)(ValueType)dataSymbolAddress);
                        else
                            BinaryPrimitives.WriteUInt64LittleEndian(dest, (UInt64)(ValueType)dataSymbolAddress);
                        //Console.Out.WriteLine($"\tDS {tsr.Name}->{dataSymbolAddress}");
                    }
                }
            }

            // Perform BSS reference replacements
            if (!bssSymbols.IsEmpty)
            {
                if (textSegment == null)
                    throw new InvalidOperationException("Text segment is null when attempting to perform BSS replacements");
                if (textSegmentSize == null)
                    throw new InvalidOperationException("Text segment size is null when attempting to perform BSS replacements");
                if (dataSegmentSize == null)
                    throw new InvalidOperationException("Data segment size is null when attempting to perform BSS replacements");

                var tsrBss = textSymbolReferenceOffsets.Where(tsr => bssSymbols.Exists(bss => string.Compare(bss.name, tsr.Name, StringComparison.InvariantCultureIgnoreCase) == 0)).ToArray();
                Span<byte> buf = stackalloc byte[10];
                foreach (var tsr in tsrBss)
                {
                    var bss = bssSymbols.Single(bss => string.Compare(bss.name, tsr.Name, StringComparison.InvariantCultureIgnoreCase) == 0);
                    var bssIndex = bssSymbols.IndexOf(bss);
                    TAddrSize bssOffset;
                    if (typeof(TAddrSize) == typeof(UInt32))
                    {
                        bssOffset = ValueTypeUtility.Add(textSegmentBase, textSegmentSize.Value + dataSegmentSize.Value + (UInt32)bssSymbols.Take(bssIndex).Sum(b => b.Size()));
                        for (var i = 0; i < 4; i++)
                            if (textSegment[(UInt32)(ValueType)tsr.TextSegmentReferenceOffset + i] != 0xFF)
                                throw new InvalidOperationException($"Attempted to overwrite placeholder for {tsr.Name} which did not contain placeholder values!");
                        BinaryPrimitives.WriteUInt32LittleEndian(textSegment.AsSpan((int)(UInt32)(ValueType)tsr.TextSegmentReferenceOffset, 4), (UInt32)(ValueType)bssOffset);
                    }
                    else
                    {
                        bssOffset = ValueTypeUtility.Add(textSegmentBase, textSegmentSize.Value + dataSegmentSize.Value + (UInt64)bssSymbols.Take(bssIndex).Sum(b => b.Size()));
                        // A 64-bit address occupies all eight reserved bytes.  Validating and
                        // copying only four left the upper half of every BSS address holding
                        // the 0xFF placeholder.
                        for (var i = 0; i < 8; i++)
                            if (textSegment[(long)((UInt64)(ValueType)tsr.TextSegmentReferenceOffset + (UInt64)i)] != 0xFF)
                                throw new InvalidOperationException($"Attempted to overwrite placeholder for {tsr.Name} which did not contain placeholder values!");
                        BinaryPrimitives.WriteUInt64LittleEndian(textSegment.AsSpan((int)(UInt64)(ValueType)tsr.TextSegmentReferenceOffset, 8), (UInt64)(ValueType)bssOffset);
                    }
                    Console.Out.WriteLine($"\tBSS {bss.name}->{bssOffset}");
                }
            }

            if (textSegmentSize == null)
                throw new InvalidOperationException("Text segment size unknown at the end of compilation");
            if (entryPoint == null)
                throw new InvalidOperationException("Entry point unknown at the end of compilation");
            if (textSegment == null)
                throw new InvalidOperationException("Text segment null at the end of compilation");

            return new CompilationResult<TAddrSize>(
                textSegmentSize.Value,
                dataSegmentSize ?? 0,
                bssSegmentSize ?? 0,
                entryPoint.Value,
                textSegmentBase,
                dataSegmentBase,
                textSegment,
                textLabelsOffsets!.ToDictionary(tlo => tlo.Key, tlo => tlo.Value),
                textSymbolReferenceOffsets!.Select(tsr => new BytecodeTextSymbol<TAddrSize>(tsr.Name, tsr.TextSegmentInstructionOffset, tsr.TextSegmentReferenceOffset, tsr.ReferenceLength)),
                dataSegment,
                dataSymbolOffsets!.ToDictionary(ds => ds.Key, ds => new BytecodeDataSymbol<TAddrSize>(ds.Value.DataSegmentOffset, ds.Value.Length, ds.Value.Constant)),
                bssSymbols!,
                errors);
        }


        private CompileTextSectionResult<TAddrSize> CompileTextSectionLinesToBytecode(
            IEnumerable<string> programLines)
        {
            TAddrSize offsetBytes = typeof(TAddrSize) == typeof(UInt32) ? (TAddrSize)(ValueType)(UInt32)0 : (TAddrSize)(ValueType)(UInt64)0;
            var bytecode = new List<byte>();
            var labelsOffsets = new Dictionary<string, TAddrSize>();
            var symbolReferenceOffsets = new List<BytecodeTextSymbol<TAddrSize>>();

            Span<byte> buf = stackalloc byte[10]; // once, at top of the compile loop

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
                    var respin = new List<string>([lineParts.Take(2).Aggregate((c, n) => c + n)]);
                    respin.AddRange(lineParts.Skip(2));
                    lineParts = [.. respin];
                }

                // Parse label
                if (lineParts[0].EndsWith(':'))
                {
                    labelsOffsets.Add(lineParts[0].TrimEnd(':'), offsetBytes);
                    if (lineParts.Count == 1)
                        continue;

                    lineParts = [.. lineParts.Skip(1)];
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
                if (string.Compare("END", instruction, StringComparison.InvariantCulture) == 0)
                {
                    bytecode.Add((byte)Bytecode.END);
                    offsetBytes = offsetBytes.Add(1);
                }
                else if (string.Compare("INT", instruction, StringComparison.InvariantCulture) == 0)
                {
                    var operand = lineParts[^1];
                    var operandType = AssemblerUtility.GetOperandType(operand);
                    if (operandType != ParameterType.Constant)
                        throw new Exception($"ERROR: Unable to parse INT operand, expected a constant: {line}");

                    bytecode.Add((byte)Bytecode.INT);
                    offsetBytes = offsetBytes.Add(1);
                    bytecode.Add(operand.ParseByteConstant());
                    offsetBytes = offsetBytes.Add(1);
                    continue;
                }
                else if (string.Compare("SYSCALL", instruction, StringComparison.InvariantCulture) == 0)
                {
                    bytecode.Add((byte)Bytecode.SYSCALL);
                    offsetBytes = offsetBytes.Add(1);
                    continue;
                }
                else if (string.Compare("MOV", instruction, StringComparison.InvariantCulture) == 0)
                {
                    var dst = lineParts[^2].ToUpperInvariant();
                    var dstType = AssemblerUtility.GetOperandType(dst);
                    var src = lineParts[^1].ToUpperInvariant();
                    var srcType = AssemblerUtility.GetOperandType(src);

                    switch (dstType)
                    {
                        case ParameterType.RegisterReference:
                            {
                                switch (srcType)
                                {
                                    case ParameterType.RegisterReference:
                                        {
                                            bytecode.Add((byte)Bytecode.MOV_REGISTER);
                                            offsetBytes = offsetBytes.Add(1);

                                            bytecode.Add((byte)registers[dst]);
                                            offsetBytes = offsetBytes.Add(1);
                                            bytecode.Add((byte)registers[src]);
                                            offsetBytes = offsetBytes.Add(1);
                                            continue;
                                        }
                                    case ParameterType.RegisterIndirect:
                                        {
                                            bytecode.Add((byte)Bytecode.MOV_INDIRECT);
                                            offsetBytes = offsetBytes.Add(1);

                                            bytecode.Add((byte)registers[dst]);
                                            offsetBytes = offsetBytes.Add(1);
                                            bytecode.Add((byte)registers[src.TrimStart('[').TrimEnd(']')]);
                                            offsetBytes = offsetBytes.Add(1);
                                            continue;
                                        }
                                    case ParameterType.VariableAddress:
                                        {
                                            TAddrSize instructionOffset = offsetBytes;
                                            bytecode.Add((byte)Bytecode.MOV_IMMEDIATE);
                                            offsetBytes = offsetBytes.Add(1);

                                            var regDst = registers[dst.ToUpperInvariant()];
                                            bytecode.Add((byte)regDst);
                                            offsetBytes = offsetBytes.Add(1);

                                            BytecodeTextSymbol<TAddrSize> textSymbol = new(
                                                    src,
                                                    instructionOffset,
                                                    offsetBytes,
                                                (typeHintSize == 8 || (!typeHintSize.HasValue && regDst.Size() == 8)) ? (byte)8 :
                                                ((typeHintSize == 4 || (!typeHintSize.HasValue && regDst.Size() == 4)) ? (byte)4 :
                                                ((typeHintSize == 2 || (!typeHintSize.HasValue && regDst.Size() == 2)) ? (byte)2 :
                                                ((typeHintSize == 1 || (!typeHintSize.HasValue && regDst.Size() == 1)) ? (byte)1 : (byte)0)))
                                            );

                                            if (textSymbol.ReferenceLength == 0)
                                                throw new InvalidOperationException($"Unable to determine register length: {regDst}");

                                            for (var i = 0; i < textSymbol.ReferenceLength; i++)
                                                bytecode.Add(0xFF); // UNRESOLVED SYMBOL FOR VARIABLE

                                            symbolReferenceOffsets.Add(textSymbol);
                                            offsetBytes = offsetBytes.Add(textSymbol.ReferenceLength);
                                            continue;
                                        }
                                    case ParameterType.Constant:
                                        {
                                            var dstReg = registers[dst.ToUpperInvariant()];

                                            // A hint that disagrees with the destination register would size the
                                            // immediate one way while the VM, which infers the width from the
                                            // register alone, reads it another -- silently desynchronising the
                                            // instruction stream from that point on.  There is no sensible
                                            // interpretation of the mismatch, so reject it.
                                            if (typeHintSize.HasValue && typeHintSize.Value != dstReg.Size())
                                                throw new InvalidOperationException($"ERROR: MOV operand size hint of {typeHintSize.Value} byte(s) disagrees with destination register {dstReg} of {dstReg.Size()} byte(s): {line}");

                                            bytecode.Add((byte)Bytecode.MOV_IMMEDIATE);
                                            offsetBytes = offsetBytes.Add(1);

                                            bytecode.Add((byte)dstReg);
                                            offsetBytes = offsetBytes.Add(1);

                                            if (typeHintSize == 8 || (!typeHintSize.HasValue && dstReg.Size() == 8))
                                            {
                                                BinaryPrimitives.WriteUInt64LittleEndian(buf, src.ParseUInt64Constant());
                                                bytecode.AddRange(buf[..8]);
                                                offsetBytes = offsetBytes.Add(8);
                                            }
                                            else if (typeHintSize == 4 || (!typeHintSize.HasValue && dstReg.Size() == 4))
                                            {
                                                BinaryPrimitives.WriteUInt32LittleEndian(buf, src.ParseUInt32Constant());
                                                bytecode.AddRange(buf[..4]);
                                                offsetBytes = offsetBytes.Add(4);
                                            }
                                            else if (typeHintSize == 2 || (!typeHintSize.HasValue && dstReg.Size() == 2))
                                            {
                                                BinaryPrimitives.WriteUInt16LittleEndian(buf, src.ParseUInt16Constant());
                                                bytecode.AddRange(buf[..2]);
                                                offsetBytes = offsetBytes.Add(2);
                                            }
                                            else if (typeHintSize == 1 || (!typeHintSize.HasValue && dstReg.Size() == 1))
                                            {
                                                bytecode.Add(src.ParseByteConstant());
                                                offsetBytes = offsetBytes.Add(1);
                                            }
                                            else
                                                throw new InvalidOperationException($"Unable to determin destination register type: {dstReg}");

                                            continue;
                                        }
                                    default:
                                        throw new Exception($"ERROR: Unable to parse MOV parameters into an opcode, unhandled src type: {line}");
                                }
                            }
                        case ParameterType.VariableDirect:
                            {
                                switch (srcType)
                                {
                                    case ParameterType.Constant:
                                        {
                                            bytecode.Add((byte)Bytecode.MOV_DIRECT);
                                            offsetBytes = offsetBytes.Add(1);

                                            // TODO: HOW BIG?
                                            if (typeHintSize == null)
                                                throw new InvalidOperationException("I can't handle unhinted variable loads yet.  I should scan DS!");

                                            // The destination address is always machine-width; the size
                                            // hint describes the datum stored, not the address locating it.
                                            var addressSize = (byte)(typeof(TAddrSize) == typeof(UInt32) ? 4 : 8);

                                            // A 32-bit agent rejects an 8-byte store when it decodes the size
                                            // byte.  Catching it here turns a runtime crash into a compile error
                                            // that names the offending line.
                                            if (typeHintSize.Value > addressSize)
                                                throw new InvalidOperationException($"ERROR: MOV operand size of {typeHintSize.Value} bytes exceeds the {addressSize}-byte machine width: {line}");

                                            symbolReferenceOffsets.Add(
                                                new BytecodeTextSymbol<TAddrSize>
                                                (
                                                     dst[1..^1], // Strip brackets
                                                     offsetBytes.Sub(1),
                                                     offsetBytes,
                                                     addressSize
                                                ));

                                            for (var i = 0; i < addressSize; i++)
                                                bytecode.Add(0xFF); // UNRESOLVED SYMBOL FOR VARIABLE
                                            offsetBytes = offsetBytes.Add(addressSize);

                                            // Operand size, specified explicitly
                                            bytecode.Add(typeHintSize.Value);
                                            offsetBytes = offsetBytes.Add(1);

                                            var variableSize = typeHintSize.Value;
                                            switch (variableSize)
                                            {
                                                case 8:
                                                    BinaryPrimitives.WriteUInt64LittleEndian(buf, src.ParseUInt64Constant());
                                                    bytecode.AddRange(buf[..8]);
                                                    break;
                                                case 4:
                                                    BinaryPrimitives.WriteUInt32LittleEndian(buf, src.ParseUInt32Constant());
                                                    bytecode.AddRange(buf[..4]);
                                                    break;
                                                case 2:
                                                    BinaryPrimitives.WriteUInt16LittleEndian(buf, src.ParseUInt16Constant());
                                                    bytecode.AddRange(buf[..2]);
                                                    break;
                                                case 1:
                                                    bytecode.Add(src.ParseByteConstant());
                                                    break;
                                                default:
                                                    throw new InvalidOperationException();
                                            }
                                            offsetBytes = offsetBytes.Add(variableSize);
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
                else if (string.Compare("POP", instruction, StringComparison.InvariantCulture) == 0)
                {
                    var written = Pop(buf, lineParts[^1]);
                    bytecode.AddRange(buf[..written]);
                    offsetBytes = offsetBytes.Add(written);
                }
                else if (string.Compare("PUSH", instruction, StringComparison.InvariantCulture) == 0)
                {
                    var operand = lineParts[^1];
                    var operandType = AssemblerUtility.GetOperandType(operand);

                    switch (operandType)
                    {
                        case ParameterType.RegisterReference:
                            {
                                bytecode.Add((byte)Bytecode.PUSH_REG);
                                offsetBytes = offsetBytes.Add(1);
                                bytecode.Add((byte)registers[operand]);
                                offsetBytes = offsetBytes.Add(1);
                                continue;
                            }
                        case ParameterType.RegisterIndirect:
                            {
                                bytecode.Add((byte)Bytecode.PUSH_MEM);
                                offsetBytes = offsetBytes.Add(1);
                                bytecode.Add((byte)registers[operand.TrimStart('[').TrimEnd(']')]);
                                offsetBytes = offsetBytes.Add(1);
                                continue;
                            }
                        case ParameterType.Constant:
                            {
                                bytecode.Add((byte)Bytecode.PUSH_CON);
                                offsetBytes = offsetBytes.Add(1);
                                BinaryPrimitives.WriteUInt32LittleEndian(buf, operand.ParseUInt32Constant());
                                bytecode.AddRange(buf[..4]);
                                offsetBytes = offsetBytes.Add(4);
                                continue;
                            }
                        default:
                            throw new Exception($"ERROR: Unable to parse PUSH parameters into an opcode, unhandled operand type: {line}");
                    }

                    throw new Exception($"ERROR: Unable to parse PUSH parameters into an opcode: {line}");
                }
                else if (string.Compare("ADD", instruction, StringComparison.InvariantCulture) == 0)
                {
                    var written = Add(buf, typeHintSize, lineParts[^2], lineParts[^1]);
                    bytecode.AddRange(buf[..written]);
                    offsetBytes = offsetBytes.Add(written);
                }
                else if (string.Compare("AND", instruction, StringComparison.InvariantCulture) == 0)
                {
                    var written = And(buf, typeHintSize, lineParts[^2], lineParts[^1]);
                    bytecode.AddRange(buf[..written]);
                    offsetBytes = offsetBytes.Add(written);
                }
                else if (string.Compare("CMP", instruction, StringComparison.InvariantCulture) == 0)
                {
                    var written = Cmp(buf, typeHintSize, lineParts[^2], lineParts[^1]);
                    bytecode.AddRange(buf[..written]);
                    offsetBytes = offsetBytes.Add(written);
                }
                else if (string.Compare("XOR", instruction, StringComparison.InvariantCulture) == 0)
                {
                    var written = XOr(buf, lineParts[^2], lineParts[^1]);
                    bytecode.AddRange(buf[..written]);
                    offsetBytes = offsetBytes.Add(written);
                }
                else if (
                    string.Compare("JZ", instruction, StringComparison.InvariantCulture) == 0 ||
                    string.Compare("JE", instruction, StringComparison.InvariantCulture) == 0 ||
                    string.Compare("JNZ", instruction, StringComparison.InvariantCulture) == 0 ||
                    string.Compare("JNE", instruction, StringComparison.InvariantCulture) == 0 ||
                    string.Compare("JO", instruction, StringComparison.InvariantCulture) == 0 ||
                    string.Compare("JNO", instruction, StringComparison.InvariantCulture) == 0 ||
                    string.Compare("JS", instruction, StringComparison.InvariantCulture) == 0 ||
                    string.Compare("JNS", instruction, StringComparison.InvariantCulture) == 0 ||
                    string.Compare("JB", instruction, StringComparison.InvariantCulture) == 0 ||
                    string.Compare("JNAE", instruction, StringComparison.InvariantCulture) == 0 ||
                    string.Compare("JC", instruction, StringComparison.InvariantCulture) == 0 ||
                    string.Compare("JNB", instruction, StringComparison.InvariantCulture) == 0 ||
                    string.Compare("JAE", instruction, StringComparison.InvariantCulture) == 0 ||
                    string.Compare("JNC", instruction, StringComparison.InvariantCulture) == 0 ||
                    string.Compare("JBE", instruction, StringComparison.InvariantCulture) == 0 ||
                    string.Compare("JNA", instruction, StringComparison.InvariantCulture) == 0 ||
                    string.Compare("JA", instruction, StringComparison.InvariantCulture) == 0 ||
                    string.Compare("JNBE", instruction, StringComparison.InvariantCulture) == 0 ||
                    string.Compare("JL", instruction, StringComparison.InvariantCulture) == 0 ||
                    string.Compare("JNGE", instruction, StringComparison.InvariantCulture) == 0 ||
                    string.Compare("JGE", instruction, StringComparison.InvariantCulture) == 0 ||
                    string.Compare("JNL", instruction, StringComparison.InvariantCulture) == 0 ||
                    string.Compare("JLE", instruction, StringComparison.InvariantCulture) == 0 ||
                    string.Compare("JNG", instruction, StringComparison.InvariantCulture) == 0 ||
                    string.Compare("JG", instruction, StringComparison.InvariantCulture) == 0 ||
                    string.Compare("JNLE", instruction, StringComparison.InvariantCulture) == 0 ||
                    string.Compare("JP", instruction, StringComparison.InvariantCulture) == 0 ||
                    string.Compare("JPE", instruction, StringComparison.InvariantCulture) == 0 ||
                    string.Compare("JPO", instruction, StringComparison.InvariantCulture) == 0 ||
                    string.Compare("JNP", instruction, StringComparison.InvariantCulture) == 0 ||
                    string.Compare("JCXZ", instruction, StringComparison.InvariantCulture) == 0 ||
                    string.Compare("JECXZ", instruction, StringComparison.InvariantCulture) == 0 ||
                    string.Compare("JMP", instruction, StringComparison.InvariantCulture) == 0)
                {
                    var operand = lineParts[^1];

                    if (string.Compare("JZ", instruction, StringComparison.InvariantCulture) == 0)
                        bytecode.Add((byte)Bytecode.JZ);
                    else if (string.Compare("JE", instruction, StringComparison.InvariantCulture) == 0)
                        bytecode.Add((byte)Bytecode.JE);
                    else if (string.Compare("JNZ", instruction, StringComparison.InvariantCulture) == 0)
                        bytecode.Add((byte)Bytecode.JNZ);
                    else if (string.Compare("JNE", instruction, StringComparison.InvariantCulture) == 0)
                        bytecode.Add((byte)Bytecode.JNE);
                    else if (string.Compare("JO", instruction, StringComparison.InvariantCulture) == 0)
                        bytecode.Add((byte)Bytecode.JO);
                    else if (string.Compare("JNO", instruction, StringComparison.InvariantCulture) == 0)
                        bytecode.Add((byte)Bytecode.JNO);
                    else if (string.Compare("JS", instruction, StringComparison.InvariantCulture) == 0)
                        bytecode.Add((byte)Bytecode.JS);
                    else if (string.Compare("JNS", instruction, StringComparison.InvariantCulture) == 0)
                        bytecode.Add((byte)Bytecode.JNS);
                    else if (string.Compare("JB", instruction, StringComparison.InvariantCulture) == 0)
                        bytecode.Add((byte)Bytecode.JB);
                    else if (string.Compare("JNAE", instruction, StringComparison.InvariantCulture) == 0)
                        bytecode.Add((byte)Bytecode.JNAE);
                    else if (string.Compare("JC", instruction, StringComparison.InvariantCulture) == 0)
                        bytecode.Add((byte)Bytecode.JC);
                    else if (string.Compare("JNB", instruction, StringComparison.InvariantCulture) == 0)
                        bytecode.Add((byte)Bytecode.JNB);
                    else if (string.Compare("JAE", instruction, StringComparison.InvariantCulture) == 0)
                        bytecode.Add((byte)Bytecode.JAE);
                    else if (string.Compare("JNC", instruction, StringComparison.InvariantCulture) == 0)
                        bytecode.Add((byte)Bytecode.JNC);
                    else if (string.Compare("JBE", instruction, StringComparison.InvariantCulture) == 0)
                        bytecode.Add((byte)Bytecode.JBE);
                    else if (string.Compare("JNA", instruction, StringComparison.InvariantCulture) == 0)
                        bytecode.Add((byte)Bytecode.JNA);
                    else if (string.Compare("JA", instruction, StringComparison.InvariantCulture) == 0)
                        bytecode.Add((byte)Bytecode.JA);
                    else if (string.Compare("JNBE", instruction, StringComparison.InvariantCulture) == 0)
                        bytecode.Add((byte)Bytecode.JNBE);
                    else if (string.Compare("JL", instruction, StringComparison.InvariantCulture) == 0)
                        bytecode.Add((byte)Bytecode.JL);
                    else if (string.Compare("JNGE", instruction, StringComparison.InvariantCulture) == 0)
                        bytecode.Add((byte)Bytecode.JNGE);
                    else if (string.Compare("JGE", instruction, StringComparison.InvariantCulture) == 0)
                        bytecode.Add((byte)Bytecode.JGE);
                    else if (string.Compare("JNL", instruction, StringComparison.InvariantCulture) == 0)
                        bytecode.Add((byte)Bytecode.JNL);
                    else if (string.Compare("JLE", instruction, StringComparison.InvariantCulture) == 0)
                        bytecode.Add((byte)Bytecode.JLE);
                    else if (string.Compare("JNG", instruction, StringComparison.InvariantCulture) == 0)
                        bytecode.Add((byte)Bytecode.JNG);
                    else if (string.Compare("JG", instruction, StringComparison.InvariantCulture) == 0)
                        bytecode.Add((byte)Bytecode.JG);
                    else if (string.Compare("JNLE", instruction, StringComparison.InvariantCulture) == 0)
                        bytecode.Add((byte)Bytecode.JNLE);
                    else if (string.Compare("JP", instruction, StringComparison.InvariantCulture) == 0)
                        bytecode.Add((byte)Bytecode.JP);
                    else if (string.Compare("JPE", instruction, StringComparison.InvariantCulture) == 0)
                        bytecode.Add((byte)Bytecode.JPE);
                    else if (string.Compare("JNP", instruction, StringComparison.InvariantCulture) == 0)
                        bytecode.Add((byte)Bytecode.JNP);
                    else if (string.Compare("JPO", instruction, StringComparison.InvariantCulture) == 0)
                        bytecode.Add((byte)Bytecode.JPO);
                    else if (string.Compare("JCXZ", instruction, StringComparison.InvariantCulture) == 0)
                        bytecode.Add((byte)Bytecode.JCXZ);
                    else if (string.Compare("JECXZ", instruction, StringComparison.InvariantCulture) == 0)
                        bytecode.Add((byte)Bytecode.JECXZ);
                    else if (string.Compare("JMP", instruction, StringComparison.InvariantCulture) == 0)
                        bytecode.Add((byte)Bytecode.JMP);
                    offsetBytes = offsetBytes.Add(1);

                    var textSymbol = new BytecodeTextSymbol<TAddrSize>(operand, offsetBytes.Sub(1), offsetBytes, typeHintSize ?? (typeof(TAddrSize) == typeof(UInt32) ? (byte)4 : (byte)8));

                    for (var i = 0; i < textSymbol.ReferenceLength; i++)
                        bytecode.Add(0xEE); // UNRESOLVED SYMBOL FOR LABEL

                    symbolReferenceOffsets.Add(textSymbol);
                    offsetBytes = offsetBytes.Add(textSymbol.ReferenceLength);
                }
                else
                    throw new Exception($"ERROR: Cannot compile: {line}");
            }

            return new CompileTextSectionResult<TAddrSize>([.. bytecode], labelsOffsets, symbolReferenceOffsets.Cast<BytecodeTextSymbol<TAddrSize>>());
        }

        private CompileDataSectionResult<TAddrSize> CompileDataSectionLines(IEnumerable<string> dataLines)
        {
            TAddrSize zero = typeof(TAddrSize) == typeof(UInt32) ? (TAddrSize)(ValueType)(UInt32)0 : (TAddrSize)(ValueType)(UInt64)0;
            TAddrSize offsetBytes = zero;

            var bytecode = new List<byte>();
            var symbolOffsets = new Dictionary<string, BytecodeDataSymbol<TAddrSize>>();
            Span<byte> buf = stackalloc byte[8]; // once, at top of the compile loop

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

                        if (ov is string ovs)
                        {
                            var stringBytes = System.Text.Encoding.ASCII.GetBytes(ovs);
                            bytecode.AddRange(stringBytes);

                            if (dataAllocationDirective.Label != null && !symbolOffsets.ContainsKey(dataAllocationDirective.Label.ToUpperInvariant()))
                                symbolOffsets.Add(dataAllocationDirective.Label.ToUpperInvariant(),
                                typeof(TAddrSize) == typeof(UInt32)
                                ? new BytecodeDataSymbol<TAddrSize>(offsetBytes, (ushort)stringBytes.Length, false)
                                : new BytecodeDataSymbol<TAddrSize>(offsetBytes, (ushort)stringBytes.Length, false));

                            offsetBytes = offsetBytes.Add(stringBytes.Length);
                            continue;
                        }

                        if (ov is byte ovb)
                        {
                            bytecode.Add(ovb);

                            if (dataAllocationDirective.Label != null && !symbolOffsets.ContainsKey(dataAllocationDirective.Label.ToUpperInvariant()))
                                symbolOffsets.Add(
                                    dataAllocationDirective.Label.ToUpperInvariant(),
                                    new BytecodeDataSymbol<TAddrSize>(offsetBytes, 1, false));

                            offsetBytes = offsetBytes.Add(1);
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

                        if (ov is string ovstr)
                        {
                            if (double.TryParse(ovstr, out double ovDouble))
                                ov = ovDouble;
                            else if (float.TryParse(ovstr, out float ovFloat))
                                ov = ovFloat;
                            else
                                throw new InvalidOperationException($"Unable to parse string as numeric value: {ov}");
                        }

                        switch (ov)
                        {
                            case double ovdbl:
                                BinaryPrimitives.WriteDoubleLittleEndian(buf, ovdbl); // This is 8 bytes
                                bytecode.AddRange(buf[..8]);
                                offsetBytes = offsetBytes.Add(8);
                                continue;
                            case float ovf:
                                BinaryPrimitives.WriteDoubleLittleEndian(buf, Convert.ToDouble(ovf)); // This is 8 bytes
                                bytecode.AddRange(buf[..8]);
                                offsetBytes = offsetBytes.Add(8);
                                continue;
                            case byte ovb:
                                BinaryPrimitives.WriteUInt64LittleEndian(buf, Convert.ToUInt64(ovb)); // This is 8 bytes
                                bytecode.AddRange(buf[..8]);
                                offsetBytes = offsetBytes.Add(8);
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
                        if (nextValue == null && AssemblerUtility.TryResolveDataAllocationReference((string)next, symbolOffsets, out TAddrSize nv))
                            nextValue = nv;

                        if (nextValue == null && string.CompareOrdinal("+", (string)next) == 0)
                        {
                            var b = computeStack.Pop();
                            var a = computeStack.Pop();
                            if (a is byte ab && b is byte bb)
                                computeStack.Push(ab + bb);
                            else if (a is ushort aus && b is ushort bus)
                                computeStack.Push(aus + bus);
                            else if (a is uint aui && b is uint bui)
                                computeStack.Push(aui + bui);
                            else if (a is ulong aul && b is ulong bul)
                                computeStack.Push(aul + bul);
                            else
                                throw new InvalidOperationException($"Unable to handle addition of {a.GetType().Name} and {b.GetType().Name}");
                            continue;
                        }
                        else if (nextValue == null && string.CompareOrdinal("-", (string)next) == 0)
                        {
                            var b = computeStack.Pop();
                            var a = computeStack.Pop();

                            if (a is byte ab && b is byte bb)
                                computeStack.Push((byte)(ab - bb));
                            else if (a is ushort aus)
                            {
                                if (b is ushort bus)
                                    computeStack.Push((ushort)(aus - bus));
                                else if (b is uint bui)
                                    computeStack.Push(aus - bui);
                                else
                                    throw new InvalidOperationException($"Unable to handle subtraction of ushort and {b.GetType().Name}");
                            }
                            else if (a is uint aui)
                            {
                                if (b is uint bui)
                                    computeStack.Push(aui - bui);
                                else if (b is ushort bus)
                                    computeStack.Push((ushort)(aui - bus));
                                else
                                    throw new InvalidOperationException($"Unable to handle subtraction of uint and {b.GetType().Name}");
                            }
                            else if (a is ulong aul)
                            {
                                if (b is ulong bul)
                                    computeStack.Push(aul - bul);
                                else if (b is uint bui)
                                    computeStack.Push(aul - bui);
                                else
                                    throw new InvalidOperationException($"Unable to handle subtraction of ulong and {b.GetType().Name}");
                            }
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

                    if (ov is ulong ovu64)
                    {
                        BinaryPrimitives.WriteUInt64LittleEndian(buf, ovu64);
                        bytecode.AddRange(buf[..8]);

                        if (dataAllocationDirective.Label != null && !symbolOffsets.ContainsKey(dataAllocationDirective.Label.ToUpperInvariant()))
                            symbolOffsets.Add(dataAllocationDirective.Label.ToUpperInvariant(),
                                new BytecodeDataSymbol<TAddrSize>(offsetBytes, 8, true));

                        offsetBytes = offsetBytes.Add(8);
                        continue;
                    }
                    else if (ov is uint ovu32)
                    {
                        BinaryPrimitives.WriteUInt32LittleEndian(buf, ovu32);
                        bytecode.AddRange(buf[..4]);

                        if (dataAllocationDirective.Label != null && !symbolOffsets.ContainsKey(dataAllocationDirective.Label.ToUpperInvariant()))
                            symbolOffsets.Add(
                                dataAllocationDirective.Label.ToUpperInvariant(),
                                new BytecodeDataSymbol<TAddrSize>(offsetBytes, 4, true));

                        offsetBytes = offsetBytes.Add(4);
                        continue;
                    }
                    else if (ov is ushort ovu16)
                    {
                        BinaryPrimitives.WriteUInt16LittleEndian(buf, ovu16);
                        bytecode.AddRange(buf[..2]);

                        if (dataAllocationDirective.Label != null && !symbolOffsets.ContainsKey(dataAllocationDirective.Label.ToUpperInvariant()))
                            symbolOffsets.Add(
                                dataAllocationDirective.Label.ToUpperInvariant(),
                                new BytecodeDataSymbol<TAddrSize>(offsetBytes, 2, true));

                        offsetBytes = offsetBytes.Add(2);
                        continue;
                    }
                    else if (ov is byte ovu8)
                    {
                        bytecode.Add(ovu8);

                        if (dataAllocationDirective.Label != null && !symbolOffsets.ContainsKey(dataAllocationDirective.Label.ToUpperInvariant()))
                            symbolOffsets.Add(
                                dataAllocationDirective.Label.ToUpperInvariant(),
                                new BytecodeDataSymbol<TAddrSize>(offsetBytes, 1, true));

                        offsetBytes = offsetBytes.Add(1);
                        continue;
                    }

                    throw new InvalidOperationException($"Unable to encode result to data bytes: {ov}({ov.GetType().Name})");
                }
                else
                    throw new InvalidOperationException($"Unknown mnemonic: {dataAllocationDirective.Mnemonic}");
            }

            return new CompileDataSectionResult<TAddrSize>([.. bytecode], symbolOffsets);
        }

        /// <summary>
        /// Emits ADD-related machine codes
        /// </summary>
        /// <param name="dest">A span of at least 10 bytes to write to.  The actual amount of bytes written is the return value.</param>
        /// <param name="typeHintSize"></param>
        /// <param name="operand1"></param>
        /// <param name="operand2"></param>
        /// <returns>The number of bytes written to the <paramref name="dest"/> span.</returns>
        /// <exception cref="NotImplementedException"></exception>
        /// <exception cref="Exception"></exception>
        private int Add(Span<byte> dest, byte? typeHintSize, string operand1, string operand2)
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
                                        dest[0] = (byte)Bytecode.ADD_REG_CON;
                                        dest[1] = (byte)registers[operand1.ToUpperInvariant()];
                                        BinaryPrimitives.WriteUInt64LittleEndian(dest[2..], operand2.ParseUInt64Constant());
                                        return 10;
                                    }
                                    else if (typeHintSize == 4 || (!typeHintSize.HasValue && o1Reg.Size() == 4))
                                    {
                                        dest[0] = (byte)Bytecode.ADD_REG_CON;
                                        dest[1] = (byte)registers[operand1.ToUpperInvariant()];
                                        BinaryPrimitives.WriteUInt32LittleEndian(dest[2..], operand2.ParseUInt32Constant());
                                        return 6;
                                    }
                                    else if (typeHintSize == 2 || (!typeHintSize.HasValue && o1Reg.Size() == 2))
                                    {
                                        dest[0] = (byte)Bytecode.ADD_REG_CON;
                                        dest[1] = (byte)registers[operand1.ToUpperInvariant()];
                                        BinaryPrimitives.WriteUInt16LittleEndian(dest[2..], operand2.ParseUInt16Constant());
                                        return 4;
                                    }
                                    else if (typeHintSize == 1 || (!typeHintSize.HasValue && o1Reg.Size() == 1))
                                    {
                                        dest[0] = (byte)Bytecode.ADD_REG_CON;
                                        dest[1] = (byte)registers[operand1.ToUpperInvariant()];
                                        dest[2] = operand2.ParseByteConstant();
                                        return 3;
                                    }

                                    throw new NotImplementedException();
                                }
                        }
                    }
                    break;
                case ParameterType.RegisterIndirect:
                    {
                        switch (o2Type)
                        {
                            case ParameterType.Constant:
                                {
                                    dest[0] = (byte)Bytecode.ADD_MEM_CON;
                                    dest[1] = (byte)registers[operand1.TrimStart('[').TrimEnd(']').ToUpperInvariant()];
                                    BinaryPrimitives.WriteUInt32LittleEndian(dest[2..], operand2.ParseUInt32Constant());
                                    return 6;
                                }
                        }
                    }
                    break;
                default:
                    throw new Exception($"ERROR: Unable to parse ADD parameters into an opcode, unhandled operand: {operand1}");
            }

            throw new Exception($"ERROR: Unable to parse ADD into an opcode");
        }

        private int And(Span<byte> dest, byte? typeHintSize, string operand1, string operand2)
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
                                        dest[0] = (byte)Bytecode.AND_REG_CON;
                                        dest[1] = (byte)registers[operand1.ToUpperInvariant()];
                                        BinaryPrimitives.WriteUInt64LittleEndian(dest[2..], operand2.ParseUInt64Constant());
                                        return 10;
                                    }
                                    else if (typeHintSize == 4 || (!typeHintSize.HasValue && o1Reg.Size() == 4))
                                    {
                                        dest[0] = (byte)Bytecode.AND_REG_CON;
                                        dest[1] = (byte)registers[operand1.ToUpperInvariant()];
                                        BinaryPrimitives.WriteUInt32LittleEndian(dest[2..], operand2.ParseUInt32Constant());
                                        return 6;
                                    }
                                    else if (typeHintSize == 2 || (!typeHintSize.HasValue && o1Reg.Size() == 2))
                                    {
                                        dest[0] = (byte)Bytecode.AND_REG_CON;
                                        dest[1] = (byte)registers[operand1.ToUpperInvariant()];
                                        BinaryPrimitives.WriteUInt16LittleEndian(dest[2..], operand2.ParseUInt16Constant());
                                        return 4;
                                    }
                                    else if (typeHintSize == 1 || (!typeHintSize.HasValue && o1Reg.Size() == 1))
                                    {
                                        dest[0] = (byte)Bytecode.AND_REG_CON;
                                        dest[1] = (byte)registers[operand1.ToUpperInvariant()];
                                        dest[2] = operand2.ParseByteConstant();
                                        return 3;
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

        private int Cmp(Span<byte> dest, byte? typeHintSize, string operand1, string operand2)
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
                                        dest[0] = (byte)Bytecode.CMP_REG_CON;
                                        dest[1] = (byte)o1Reg;
                                        BinaryPrimitives.WriteUInt64LittleEndian(dest[2..], operand2.ParseUInt64Constant());
                                        return 10;
                                    }
                                    else if (typeHintSize == 4 || (!typeHintSize.HasValue && o1Reg.Size() == 4))
                                    {
                                        dest[0] = (byte)Bytecode.CMP_REG_CON;
                                        dest[1] = (byte)o1Reg;
                                        BinaryPrimitives.WriteUInt32LittleEndian(dest[2..], operand2.ParseUInt32Constant());
                                        return 6;
                                    }
                                    else if (typeHintSize == 2 || (!typeHintSize.HasValue && o1Reg.Size() == 2))
                                    {
                                        dest[0] = (byte)Bytecode.CMP_REG_CON;
                                        dest[1] = (byte)o1Reg;
                                        BinaryPrimitives.WriteUInt16LittleEndian(dest[2..], operand2.ParseUInt16Constant());
                                        return 4;
                                    }
                                    else if (typeHintSize == 1 || (!typeHintSize.HasValue && o1Reg.Size() == 1))
                                    {
                                        dest[0] = (byte)Bytecode.CMP_REG_CON;
                                        dest[1] = (byte)o1Reg;
                                        dest[2] = operand2.ParseByteConstant();
                                        return 3;
                                    }

                                    throw new NotImplementedException();
                                }
                        }
                    }
                    break;
                default:
                    throw new Exception($"ERROR: Unable to parse CMP parameters into an opcode, unhandled operand: {operand1}");
            }

            throw new Exception($"ERROR: Unable to parse CMP into an opcode");
        }

        private int XOr(Span<byte> dest, string operand1, string operand2)
        {
            var o1Type = AssemblerUtility.GetOperandType(operand1);
            var o2Type = AssemblerUtility.GetOperandType(operand2);

            switch (o1Type)
            {
                case ParameterType.RegisterReference:
                    {
                        switch (o2Type)
                        {
                            case ParameterType.RegisterReference:
                                {
                                    dest[0] = (byte)Bytecode.XOR_REG_REG;
                                    dest[1] = (byte)registers[operand1.ToUpperInvariant()];
                                    dest[2] = (byte)registers[operand2.ToUpperInvariant()];
                                    return 3;
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

        private int Pop(Span<byte> dest, string operand)
        {
            var operandType = AssemblerUtility.GetOperandType(operand);

            switch (operandType)
            {
                case ParameterType.RegisterReference:
                    {
                        dest[0] = (byte)Bytecode.POP_REG;
                        dest[1] = (byte)registers[operand.ToUpperInvariant()];
                        break;
                    }
                case ParameterType.RegisterIndirect:
                    {
                        dest[0] = (byte)Bytecode.POP_MEM;
                        dest[1] = (byte)registers[operand.TrimStart('[').TrimEnd(']').ToUpperInvariant()];
                        break;
                    }
                default:
                    throw new Exception($"ERROR: Unable to parse POP parameters into an opcode, unhandled operand: {operand}");
            }

            return 2;
        }

        public static string GetEnumDescription(Enum value)
        {
            var fi = value.GetType().GetField(value.ToString());
            if (fi?.GetCustomAttributes(typeof(DescriptionAttribute), false) is DescriptionAttribute[] attributes && attributes.Length != 0)
            {
                return attributes.First().Description;
            }

            return value.ToString();
        }
    }

}
