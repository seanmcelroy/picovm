using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace picovm.Compiler
{
    public class BytecodeCompiler
    {
        public enum ParameterType
        {
            Unknown = 0,
            RegisterReference = 1,
            RegisterAddress = 2,
            Constant = 3,
            Variable = 4
        }

        private enum SectionType
        {
            // Program code
            Text = 1,
            // Read-only data
            Data = 2,
            // Static read-write variables
            BSS = 3
        }

        private readonly Dictionary<string, Bytecode> opcodes;
        private readonly Dictionary<string, Register> registers;

        private string entryPoint;

        public string EntryPoint => entryPoint;

        public BytecodeCompiler()
        {
            // Generate opcode dictionary
            opcodes = Enum.GetValues(typeof(Bytecode)).Cast<Bytecode>().ToDictionary(k => GetEnumDescription(k), v => v);
            registers = Enum.GetValues(typeof(Register)).Cast<Register>().ToDictionary(k => GetEnumDescription(k), v => v);
        }

        public CompilationResult Compile(string fileName, IEnumerable<string> programLines)
        {
            var ret = new CompilationResult();
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
                    macros.Add(new Macro
                    {
                        name = macroParts[0],
                        parameterCount = byte.Parse(macroParts[1]),
                        macroLines = new List<string>()
                    });
                    continue;
                }
                else if (inMacroDefinition)
                {
                    if (line.TrimStart(' ', '\t').StartsWith("%endmacro"))
                    {
                        inMacroDefinition = false;
                        continue;
                    }

                    macros.Last().macroLines.Add(line);
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
                        ret.errors.Add(new CompilationError($"Unknown section type: {line}", fileName, lineNumber));
                        throw new InvalidOperationException($"Unknown section type: '{line}' ({fileName}:{lineNumber})");
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
                                entryPoint = line.Substring(line.IndexOf("global ", StringComparison.OrdinalIgnoreCase) + "global ".Length);
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
                        ret.textSegment = bytecodeGeneration.Bytecode;
                        ret.textSegmentSize = (uint)bytecodeGeneration.Bytecode.Length;
                        ret.textLabelsOffsets = bytecodeGeneration.LabelsOffsets;
                        ret.textSymbolReferenceOffsets = bytecodeGeneration.SymbolReferenceOffsets;

                        if (ret.textLabelsOffsets == null || !ret.textLabelsOffsets.ContainsKey(entryPoint))
                        {
                            ret.errors.Add(new CompilationError($"No entry point located in source file.", fileName));
                            throw new NotImplementedException($"Unable to generate compiled output for section type: {section.Key}");
                        }
                        ret.entryPoint = ret.textLabelsOffsets[entryPoint];
                        break;
                    case SectionType.Data:
                        var constGeneration = CompileDataSectionLines(section.Value);
                        ret.dataSegment = constGeneration.Bytecode;
                        ret.dataSegmentSize = (uint)constGeneration.Bytecode.Length;
                        ret.dataSymbolOffsets = constGeneration.SymbolOffsets;
                        break;
                    case SectionType.BSS:
                        var bssGeneration = CompileBssSectionLines(section.Value);
                        ret.bssSymbols = bssGeneration.Symbols;
                        ret.bssSegmentSize = Convert.ToUInt32(bssGeneration.Symbols.Sum(s => s.Size()));
                        break;
                    default:
                        throw new NotImplementedException($"Unable to generate compiled output for section type: {section.Key}");
                }
            }

            // Resolve data section variables to symbols in text
            if (ret.dataSymbolOffsets == null)
            {
                foreach (var missing in ret.textSymbolReferenceOffsets)
                    ret.errors.Add(new CompilationError($"Symbol {missing.name} in program code is undefined; there is NO DATA SECTION!", fileName));
            }
            else
            {
                foreach (var missing in ret.textSymbolReferenceOffsets
                    .Where(tsr => !ret.dataSymbolOffsets.ContainsKey(tsr.name)
                               && !ret.textLabelsOffsets.ContainsKey(tsr.name)
                               && !ret.bssSymbols.Any(bss => string.Compare(bss.name, tsr.name, StringComparison.InvariantCultureIgnoreCase) == 0)))
                    ret.errors.Add(new CompilationError($"Symbol {missing.name} in program code is undefined by the data and BSS sections", fileName));
                if (ret.errors.Count > 0)
                    return ret;

                foreach (var extra in ret.dataSymbolOffsets.Where(dsr => !ret.textSymbolReferenceOffsets.Any(tsr => string.Compare(tsr.name, dsr.Key, StringComparison.InvariantCulture) == 0)))
                    ret.errors.Add(new CompilationError($"Data symbol {extra.Key} is not referenced in program code", fileName));

                // Rebase data symbol offsets
                ret.dataSegmentBase = ret.textSegmentBase + ret.textSegmentSize;
                foreach (var ds in ret.dataSymbolOffsets)
                    ds.Value.dataSegmentOffset += (ushort)ret.dataSegmentBase;

                // Perform text/label replacements
                foreach (var tsr in ret.textSymbolReferenceOffsets.Where(tsr => ret.textLabelsOffsets.ContainsKey(tsr.name)))
                {
                    var labelOffsetAddress = ret.textLabelsOffsets[tsr.name];
                    var labelOffsetAddressBytes = BitConverter.GetBytes(labelOffsetAddress);
                    if (labelOffsetAddressBytes.Length != tsr.referenceLength)
                        throw new InvalidOperationException($"Address size reserved for symbol {tsr.name} is {tsr.referenceLength}, but needed {labelOffsetAddressBytes.Length}");

                    for (var i = 0; i < 4; i++)
                    {
                        if (ret.textSegment[tsr.textSegmentReferenceOffset + i] != 0xEE)
                            throw new InvalidOperationException($"Attempted to overwrite placeholder for {tsr.name} which did not contain placeholder values!");
                    }
                    Array.Copy(labelOffsetAddressBytes, 0, ret.textSegment, tsr.textSegmentReferenceOffset, tsr.referenceLength);
                    Console.Out.WriteLine($"\tLBL {tsr.name}->{labelOffsetAddress}");
                }

                // Perform data replacements
                foreach (var tsr in ret.textSymbolReferenceOffsets.Where(tsr => ret.dataSymbolOffsets.ContainsKey(tsr.name)))
                {
                    var dataSymbol = ret.dataSymbolOffsets[tsr.name];
                    var dataSymbolAddress = dataSymbol.dataSegmentOffset;

                    if (dataSymbol.constant)
                    {
                        // This is a value, just write it directly into the text.
                        if (ret.textSegment[tsr.textSegmentInstructionOffset] == (byte)Bytecode.MOV_REG_MEM)
                            ret.textSegment[tsr.textSegmentInstructionOffset] = (byte)Bytecode.MOV_REG_CON;
                        else
                            throw new InvalidOperationException($"Unable to handle constant inlining of instruction: {ret.textSegment[tsr.textSegmentInstructionOffset]} for symbol {tsr.name}");

                        switch (dataSymbol.length)
                        {
                            case 2:
                                {
                                    switch (tsr.referenceLength)
                                    {
                                        case 1:
                                            throw new InvalidOperationException("Unable to inline constant size of 2 bytes into reserved text section of 1 byte");
                                        case 2:
                                            // 2 to 2, straight array copy.
                                            Array.Copy(ret.dataSegment, dataSymbolAddress - (int)ret.dataSegmentBase, ret.textSegment, tsr.textSegmentReferenceOffset, 2);
                                            for (var i = 0; i < 2; i++)
                                            {
                                                if (ret.textSegment[tsr.textSegmentReferenceOffset + i] != 0xFF)
                                                    throw new InvalidOperationException($"Attempted to overwrite placeholder for {tsr.name} which did not contain placeholder values!");
                                            }
                                            break;
                                        case 4:
                                            // 2 to 4 upsize
                                            var dataSymbolValue = BitConverter.ToUInt16(ret.dataSegment, (int)(dataSymbolAddress - ret.dataSegmentBase));
                                            var tsrValue = Convert.ToUInt32(dataSymbolValue);
                                            // Validate we're overwriting the right place
                                            for (var i = 0; i < 4; i++)
                                            {
                                                if (ret.textSegment[tsr.textSegmentReferenceOffset + i] != 0xFF)
                                                    throw new InvalidOperationException($"Attempted to overwrite placeholder for {tsr.name} which did not contain placeholder values!");
                                            }
                                            Array.Copy(BitConverter.GetBytes(tsrValue), 0, ret.textSegment, tsr.textSegmentReferenceOffset, 4);
                                            break;
                                        default:
                                            throw new NotImplementedException();
                                    }
                                    break;
                                }
                            case 4:
                                {
                                    switch (tsr.referenceLength)
                                    {
                                        case 1:
                                        case 2:
                                            throw new InvalidOperationException($"Unable to inline constant size of 4 bytes into reserved text section of {tsr.referenceLength} bytes");
                                        case 4:
                                            // 4 to 4, straight array copy.
                                            Array.Copy(ret.dataSegment, dataSymbolAddress - (int)ret.dataSegmentBase, ret.textSegment, tsr.textSegmentReferenceOffset, 4);
                                            break;
                                        default:
                                            throw new NotImplementedException();
                                    }
                                    break;
                                }
                            default:
                                throw new NotImplementedException($"Cannot handle symbol of length: {dataSymbol.length}");
                        }
                    }
                    else
                    {
                        // This is a reference, write it's address into the text.
                        var dataSymbolAddressBytes = BitConverter.GetBytes(dataSymbolAddress);
                        if (dataSymbolAddressBytes.Length != tsr.referenceLength)
                            throw new InvalidOperationException($"Address size reserved for symbol {tsr.name} is {tsr.referenceLength}, but needed {dataSymbolAddressBytes.Length}");
                        Array.Copy(dataSymbolAddressBytes, 0, ret.textSegment, tsr.textSegmentReferenceOffset, tsr.referenceLength);
                        Console.Out.WriteLine($"\tDS {tsr.name}->{dataSymbolAddress}");
                    }
                }

                // Perform BSS reference replacements
                var tsrBss = ret.textSymbolReferenceOffsets.Where(tsr => ret.bssSymbols.Exists(bss => string.Compare(bss.name, tsr.name, StringComparison.InvariantCultureIgnoreCase) == 0)).ToArray();
                foreach (var tsr in tsrBss)
                {
                    var bss = ret.bssSymbols.Single(bss => string.Compare(bss.name, tsr.name, StringComparison.InvariantCultureIgnoreCase) == 0);
                    var bssIndex = ret.bssSymbols.IndexOf(bss);
                    uint bssOffset = (ret.textSegmentBase + ret.textSegmentSize) + (ret.dataSegmentSize) + (uint)ret.bssSymbols.Take(bssIndex).Sum(b => b.Size());
                    for (var i = 0; i < 4; i++)
                    {
                        if (ret.textSegment[tsr.textSegmentReferenceOffset + i] != 0xFF)
                            throw new InvalidOperationException($"Attempted to overwrite placeholder for {tsr.name} which did not contain placeholder values!");
                    }
                    Array.Copy(BitConverter.GetBytes(bssOffset), 0, ret.textSegment, tsr.textSegmentReferenceOffset, 4);
                    Console.Out.WriteLine($"\tBSS {bss.name}->{bssOffset}");
                }

            }

            return ret;
        }

        private static uint ParseUInt32Constant(string operand)
        {
            if (operand.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return uint.Parse(operand.Substring(2), System.Globalization.NumberStyles.HexNumber);

            if (BytecodeCompiler.NUMERALS.Any(c => c == operand[0]) && operand.EndsWith("h", StringComparison.OrdinalIgnoreCase))
                return uint.Parse(operand.Substring(0, operand.Length - 1), System.Globalization.NumberStyles.HexNumber);

            return uint.Parse(operand);
        }

        private static ushort ParseUInt16Constant(string operand)
        {
            if (operand.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return ushort.Parse(operand.Substring(2), System.Globalization.NumberStyles.HexNumber);

            if (BytecodeCompiler.NUMERALS.Any(c => c == operand[0]) && operand.EndsWith("h", StringComparison.OrdinalIgnoreCase))
                return ushort.Parse(operand.Substring(0, operand.Length - 1), System.Globalization.NumberStyles.HexNumber);

            return ushort.Parse(operand);
        }

        private static byte ParseByteConstant(string operand)
        {
            if (operand.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return byte.Parse(operand.Substring(2), System.Globalization.NumberStyles.HexNumber);

            if (BytecodeCompiler.NUMERALS.Any(c => c == operand[0]) && operand.EndsWith("h", StringComparison.OrdinalIgnoreCase))
                return byte.Parse(operand.Substring(0, operand.Length - 1), System.Globalization.NumberStyles.HexNumber);

            return byte.Parse(operand);
        }

        private CompileTextSectionResult CompileTextSectionLinesToBytecode(IEnumerable<string> programLines)
        {
            ushort offsetBytes = 0;

            var bytecode = new List<byte>();
            var labelsOffsets = new Dictionary<string, uint>();
            var symbolReferenceOffsets = new List<BytecodeTextSymbol>();

            foreach (var programLine in programLines)
            {
                // Knock off any comments
                var line = programLine.Split(';')[0].TrimEnd();
                var lineParts = line.TrimStart(' ', '\t').Split(new char[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries).ToArray();

                if (lineParts[0].EndsWith(':'))
                {
                    labelsOffsets.Add(lineParts[0].TrimEnd(':'), offsetBytes);
                    if (lineParts.Length == 1)
                        continue;

                    line = lineParts.Skip(1).Aggregate((c, n) => $"{c} {n}");
                }

                var instruction = lineParts[0].ToUpperInvariant();

                // "Simple" assembly
                if (instruction == "END")
                {
                    bytecode.Add((byte)Bytecode.END);
                    offsetBytes++;
                }
                else if (instruction == "INT")
                {
                    var operand = lineParts[lineParts.Length - 1];
                    var operandType = GetOperandType(operand);
                    if (operandType != ParameterType.Constant)
                        throw new Exception($"ERROR: Unable to parse INT operand, expected a constant: {line}");

                    bytecode.Add((byte)Bytecode.INT);
                    offsetBytes++;
                    bytecode.Add(ParseByteConstant(operand));
                    offsetBytes++;
                    continue;
                }
                else if (instruction == "MOV")
                {
                    var dst = lineParts[lineParts.Length - 2].ToUpperInvariant();
                    var dstType = GetOperandType(dst);
                    var src = lineParts[lineParts.Length - 1].ToUpperInvariant();
                    var srcType = GetOperandType(src);

                    switch (dstType)
                    {
                        case ParameterType.RegisterReference:
                            {
                                switch (srcType)
                                {
                                    case ParameterType.RegisterReference:
                                        {
                                            bytecode.Add((byte)Bytecode.MOV_REG_REG);
                                            offsetBytes++;

                                            bytecode.Add((byte)registers[dst]);
                                            offsetBytes++;
                                            bytecode.Add((byte)registers[src]);
                                            offsetBytes++;
                                            continue;
                                        }
                                    case ParameterType.RegisterAddress:
                                        {
                                            bytecode.Add((byte)Bytecode.MOV_REG_MEM);
                                            offsetBytes++;

                                            bytecode.Add((byte)registers[dst]);
                                            offsetBytes++;
                                            bytecode.Add((byte)registers[src.TrimStart('[').TrimEnd(']')]);
                                            offsetBytes++;
                                            continue;
                                        }
                                    case ParameterType.Variable:
                                        {
                                            var instructionOffset = offsetBytes;
                                            bytecode.Add((byte)Bytecode.MOV_REG_MEM);
                                            offsetBytes++;

                                            var regDst = registers[dst.ToUpperInvariant()];
                                            bytecode.Add((byte)regDst);
                                            offsetBytes++;

                                            var textSymbol = new BytecodeTextSymbol
                                            {
                                                name = src,
                                                textSegmentInstructionOffset = instructionOffset,
                                                textSegmentReferenceOffset = offsetBytes,
                                                referenceLength =
                                                    (regDst == Register.EAX || regDst == Register.EBX || regDst == Register.ECX || regDst == Register.EDX) ? (byte)4 :
                                                    ((regDst == Register.AX || regDst == Register.BX || regDst == Register.CX || regDst == Register.DX) ? (byte)2 :
                                                    ((regDst == Register.AH || regDst == Register.BH || regDst == Register.CH || regDst == Register.DH ||
                                                      regDst == Register.AL || regDst == Register.BL || regDst == Register.CL || regDst == Register.DL) ? (byte)1 : (byte)0))
                                            };

                                            if (textSymbol.referenceLength == 0)
                                                throw new InvalidOperationException($"Unable to determine register length: {regDst}");

                                            for (var i = 0; i < textSymbol.referenceLength; i++)
                                                bytecode.Add((byte)0xFF); // UNRESOLVED SYMBOL FOR VARIABLE

                                            symbolReferenceOffsets.Add(textSymbol);
                                            offsetBytes += textSymbol.referenceLength;
                                            continue;
                                        }
                                    case ParameterType.Constant:
                                        {
                                            bytecode.Add((byte)Bytecode.MOV_REG_CON);
                                            offsetBytes++;

                                            var dstReg = registers[dst.ToUpperInvariant()];
                                            bytecode.Add((byte)dstReg);
                                            offsetBytes++;

                                            if (dstReg == Register.EAX || dstReg == Register.EBX || dstReg == Register.ECX || dstReg == Register.EDX)
                                            {
                                                bytecode.AddRange(BitConverter.GetBytes(ParseUInt32Constant(src)));
                                                offsetBytes += 4;
                                            }
                                            else if (dstReg == Register.AX || dstReg == Register.BX || dstReg == Register.CX || dstReg == Register.DX)
                                            {
                                                bytecode.AddRange(BitConverter.GetBytes(ParseUInt16Constant(src)));
                                                offsetBytes += 2;
                                            }
                                            else if (dstReg == Register.AH || dstReg == Register.AL
                                                || dstReg == Register.BH || dstReg == Register.BL
                                                || dstReg == Register.CH || dstReg == Register.CL
                                                || dstReg == Register.DH || dstReg == Register.DL)
                                            {
                                                bytecode.Add(ParseByteConstant(src));
                                                offsetBytes++;
                                            }
                                            else
                                                throw new InvalidOperationException($"Unable to determin destination register type: {dstReg}");

                                            continue;
                                        }
                                    default:
                                        throw new Exception($"ERROR: Unable to parse MOV parameters into an opcode, unhandled src type: {line}");
                                }
                            }
                        default:
                            throw new Exception($"ERROR: Unable to parse MOV parameters into an opcode, unhandled dst type: {line}");
                    }

                    throw new Exception($"ERROR: Unable to parse MOV parameters into an opcode: {line}");
                }
                else if (instruction == "POP")
                {
                    var pbc = Pop(lineParts[lineParts.Length - 1]);
                    bytecode.AddRange(pbc);
                    offsetBytes += (ushort)pbc.Length;
                }
                else if (instruction == "PUSH")
                {
                    var operand = lineParts[lineParts.Length - 1];
                    var operandType = GetOperandType(operand);

                    switch (operandType)
                    {
                        case ParameterType.RegisterReference:
                            {
                                bytecode.Add((byte)Bytecode.PUSH_REG);
                                offsetBytes++;
                                bytecode.Add((byte)registers[operand]);
                                offsetBytes++;
                                continue;
                            }
                        case ParameterType.RegisterAddress:
                            {
                                bytecode.Add((byte)Bytecode.PUSH_MEM);
                                offsetBytes++;
                                bytecode.Add((byte)registers[operand.TrimStart('[').TrimEnd(']')]);
                                offsetBytes++;
                                continue;
                            }
                        case ParameterType.Constant:
                            {
                                bytecode.Add((byte)Bytecode.PUSH_CON);
                                offsetBytes++;
                                bytecode.AddRange(BitConverter.GetBytes(ParseUInt32Constant(operand)));
                                offsetBytes += 4;
                                continue;
                            }
                        default:
                            throw new Exception($"ERROR: Unable to parse PUSH parameters into an opcode, unhandled operand type: {line}");
                    }

                    throw new Exception($"ERROR: Unable to parse PUSH parameters into an opcode: {line}");
                }
                else if (instruction == "ADD")
                {
                    var abc = Add(lineParts[lineParts.Length - 2], lineParts[lineParts.Length - 1]);
                    bytecode.AddRange(abc);
                    offsetBytes += (ushort)abc.Length;
                }
                else if (instruction == "AND")
                {
                    var abc = And(lineParts[lineParts.Length - 2], lineParts[lineParts.Length - 1]);
                    bytecode.AddRange(abc);
                    offsetBytes += (ushort)abc.Length;
                }
                else if (
                    string.Compare("JZ", instruction, StringComparison.InvariantCulture) == 0 ||
                    string.Compare("JMP", instruction, StringComparison.InvariantCulture) == 0)
                {
                    var operand = lineParts[lineParts.Length - 1];

                    if (string.Compare("JZ", instruction, StringComparison.InvariantCulture) == 0)
                        bytecode.Add((byte)Bytecode.JZ);
                    else if (string.Compare("JMP", instruction, StringComparison.InvariantCulture) == 0)
                        bytecode.Add((byte)Bytecode.JMP);
                    offsetBytes++;

                    var textSymbol = new BytecodeTextSymbol
                    {
                        name = operand,
                        textSegmentInstructionOffset = (ushort)(offsetBytes - 1),
                        textSegmentReferenceOffset = offsetBytes,
                        referenceLength = 4
                    };

                    for (var i = 0; i < textSymbol.referenceLength; i++)
                        bytecode.Add((byte)0xEE); // UNRESOLVED SYMBOL FOR LABEL

                    symbolReferenceOffsets.Add(textSymbol);
                    offsetBytes += textSymbol.referenceLength;
                }
                else
                    throw new Exception($"ERROR: Cannot compile: {line}");
            }

            return new CompileTextSectionResult(bytecode.ToArray(), labelsOffsets, symbolReferenceOffsets);
        }

        private CompileDataSectionResult CompileDataSectionLines(IEnumerable<string> dataLines)
        {
            ushort offsetBytes = 0;

            var bytecode = new List<byte>();
            var symbolOffsets = new Dictionary<string, BytecodeDataSymbol>();

            foreach (var dataLine in dataLines)
            {
                // Knock off any comments
                var line = dataLine.Split(';')[0].Trim();
                var dataAllocationDirective = CompilerDataAllocationDirective.ParseLine(line);

                if (string.Compare("db", dataAllocationDirective.Mnemonic, StringComparison.InvariantCultureIgnoreCase) == 0)
                {
                    var operands = dataAllocationDirective.Operands.Select(o => CompilerDataAllocationDirective.UnboxParsedOperand(o)).ToArray();
                    foreach (var operand in operands)
                    {
                        var ov = (operand is string && string.Compare((string)operand, "$", StringComparison.InvariantCulture) == 0) ? (byte)0x00 : operand;

                        if (ov.GetType() == typeof(string))
                        {
                            var stringBytes = System.Text.Encoding.ASCII.GetBytes((string)ov);
                            bytecode.AddRange(stringBytes);

                            if (dataAllocationDirective.Label != null && !symbolOffsets.ContainsKey(dataAllocationDirective.Label.ToUpperInvariant()))
                                symbolOffsets.Add(dataAllocationDirective.Label.ToUpperInvariant(), new BytecodeDataSymbol { dataSegmentOffset = offsetBytes, length = (ushort)stringBytes.Length });

                            offsetBytes += (ushort)stringBytes.Length;
                            continue;
                        }

                        if (ov.GetType() == typeof(byte))
                        {
                            bytecode.Add((byte)ov);

                            if (dataAllocationDirective.Label != null && !symbolOffsets.ContainsKey(dataAllocationDirective.Label.ToUpperInvariant()))
                                symbolOffsets.Add(dataAllocationDirective.Label.ToUpperInvariant(), new BytecodeDataSymbol { dataSegmentOffset = offsetBytes, length = 1 });

                            offsetBytes++;
                            continue;
                        }

                        throw new InvalidOperationException($"Unable to encode operand to data bytes: {operand}");
                    }
                }
                else if (string.Compare("dq", dataAllocationDirective.Mnemonic, StringComparison.InvariantCultureIgnoreCase) == 0)
                {
                    var operands = dataAllocationDirective.Operands.Select(o => CompilerDataAllocationDirective.UnboxParsedOperand(o)).ToArray();
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
                            offsetBytes += 8;
                            continue;
                        }
                        else if (ov.GetType() == typeof(float))
                        {
                            var longBytes = BitConverter.GetBytes(Convert.ToDouble((float)ov)); // This is an array of 8 bytes
                            bytecode.AddRange(longBytes);
                            offsetBytes += 8;
                            continue;
                        }
                        else if (ov.GetType() == typeof(byte))
                        {
                            var longBytes = BitConverter.GetBytes(Convert.ToUInt64((byte)ov)); // This is an array of 8 bytes
                            bytecode.AddRange(longBytes);
                            offsetBytes += 8;
                            continue;
                        }

                        throw new InvalidOperationException($"Unable to encode operand to data bytes: {operand}");
                    }
                }
                else if (string.Compare("equ", dataAllocationDirective.Mnemonic, StringComparison.InvariantCultureIgnoreCase) == 0)
                {
                    // Convert infix to RPN for easy processing
                    var operands = dataAllocationDirective.Operands;
                    var rpn = CompilerDataAllocationDirective.ConvertInfixToReversePolishNotation(operands, offsetBytes);
                    var computeStack = new Stack<ValueType>();

                    while (rpn.Count > 0)
                    {
                        var next = rpn.Dequeue();
                        var nextValue = next as ValueType;
                        if (nextValue == null && CompilerDataAllocationDirective.TryResolveDataAllocationReference((string)next, symbolOffsets, out ValueType nv))
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
                            else if (a is ushort && b is uint)
                                computeStack.Push((uint)((ushort)a - (uint)b));
                            else if (a is uint && b is ushort)
                                computeStack.Push((ushort)((uint)a - (ushort)b));
                            else
                                throw new InvalidOperationException($"Unable to handle subtraction of {a.GetType().Name} and {b.GetType().Name}");
                            continue;
                        }
                        else
                            computeStack.Push(nextValue);
                    }

                    if (computeStack.Count != 1)
                        throw new InvalidOperationException("At the end of the EQU calculation, exactly one result should be on internal stack");

                    var ov = computeStack.Pop();
                    if (ov.GetType() == typeof(uint))
                    {
                        bytecode.AddRange(BitConverter.GetBytes((uint)ov));

                        if (dataAllocationDirective.Label != null && !symbolOffsets.ContainsKey(dataAllocationDirective.Label.ToUpperInvariant()))
                            symbolOffsets.Add(dataAllocationDirective.Label.ToUpperInvariant(), new BytecodeDataSymbol { dataSegmentOffset = offsetBytes, length = 4, constant = true });

                        offsetBytes += 4;
                        continue;
                    }
                    else if (ov.GetType() == typeof(ushort))
                    {
                        bytecode.AddRange(BitConverter.GetBytes((ushort)ov));

                        if (dataAllocationDirective.Label != null && !symbolOffsets.ContainsKey(dataAllocationDirective.Label.ToUpperInvariant()))
                            symbolOffsets.Add(dataAllocationDirective.Label.ToUpperInvariant(), new BytecodeDataSymbol { dataSegmentOffset = offsetBytes, length = 2, constant = true });

                        offsetBytes += 2;
                        continue;
                    }
                    else if (ov.GetType() == typeof(byte))
                    {
                        bytecode.Add((byte)ov);

                        if (dataAllocationDirective.Label != null && !symbolOffsets.ContainsKey(dataAllocationDirective.Label.ToUpperInvariant()))
                            symbolOffsets.Add(dataAllocationDirective.Label.ToUpperInvariant(), new BytecodeDataSymbol { dataSegmentOffset = offsetBytes, length = 1, constant = true });

                        offsetBytes++;
                        continue;
                    }

                    throw new InvalidOperationException($"Unable to encode result to data bytes: {ov}({ov.GetType().Name})");
                }
                else
                    throw new InvalidOperationException($"Unknown mnemonic: {dataAllocationDirective.Mnemonic}");
            }

            return new CompileDataSectionResult(bytecode.ToArray(), symbolOffsets);
        }

        private CompileBssSectionResult CompileBssSectionLines(IEnumerable<string> dataLines)
        {
            var symbols = new List<BytecodeBssSymbol>();

            foreach (var dataLine in dataLines)
            {
                // Knock off any comments
                var line = dataLine.Split(';')[0].Trim();
                var bssAllocationDirective = CompilerBssAllocationDirective.ParseLine(line);

                if (string.Compare("resb", bssAllocationDirective.Mnemonic, StringComparison.InvariantCultureIgnoreCase) == 0)
                {
                    symbols.Add(new BytecodeBssSymbol
                    {
                        name = bssAllocationDirective.Label,
                        type = BytecodeBssSymbol.BssType.Byte,
                        length = bssAllocationDirective.Size
                    });
                }
                else if (string.Compare("resw", bssAllocationDirective.Mnemonic, StringComparison.InvariantCultureIgnoreCase) == 0)
                {
                    symbols.Add(new BytecodeBssSymbol
                    {
                        name = bssAllocationDirective.Label,
                        type = BytecodeBssSymbol.BssType.Word,
                        length = bssAllocationDirective.Size
                    });
                }
                else if (string.Compare("resd", bssAllocationDirective.Mnemonic, StringComparison.InvariantCultureIgnoreCase) == 0)
                {
                    symbols.Add(new BytecodeBssSymbol
                    {
                        name = bssAllocationDirective.Label,
                        type = BytecodeBssSymbol.BssType.DoubleWord,
                        length = bssAllocationDirective.Size
                    });
                }
                else if (string.Compare("resq", bssAllocationDirective.Mnemonic, StringComparison.InvariantCultureIgnoreCase) == 0)
                {
                    symbols.Add(new BytecodeBssSymbol
                    {
                        name = bssAllocationDirective.Label,
                        type = BytecodeBssSymbol.BssType.QuadWord,
                        length = bssAllocationDirective.Size
                    });
                }
                else
                    throw new InvalidOperationException($"Unknown mnemonic: {bssAllocationDirective.Mnemonic}");
            }

            return new CompileBssSectionResult(symbols);
        }

        public static IEnumerable<string> ParseOperandLine(string operandLine)
        {
            int? openingStringQuote = null;
            int? lastYield = null;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < operandLine.Length; i++)
            {
                var c = operandLine[i];

                if (openingStringQuote == null && (c == '\'' || c == '\"'))
                {
                    // Opening of a quoted string
                    openingStringQuote = i;
                    continue;
                }

                if (openingStringQuote != null)
                {
                    if (c == '\'' || c == '\"')
                    {
                        // Closing of a quoted string
                        yield return operandLine.Substring(openingStringQuote.Value, i - openingStringQuote.Value + 1);
                        lastYield = i + 1;
                        openingStringQuote = null;
                        continue;
                    }
                    else
                    {
                        // NO-OP while reading through a quoted string
                        continue;
                    }
                }

                if (c == ' ' || c == '\t')
                {
                    // Whitespace on the operand line
                    if (lastYield == null)
                    {
                        // Whitespace seen right after another yielded element (probably end of a delimiter).  Skip along.
                        yield return operandLine.Substring(0, i);
                        lastYield = i + 1;
                        continue;
                    }
                    else if (i == lastYield.Value)
                    {
                        // Whitespace seen right after another yielded element (probably end of a delimiter).  Skip along.
                        lastYield++;
                    }
                    else
                    {
                        yield return operandLine.Substring(lastYield.Value, i - lastYield.Value);
                        lastYield = i + 1;
                        continue;
                    }
                    continue;
                }

                if (c == ',' || i == operandLine.Length - 1)
                {
                    if (lastYield != null && i == lastYield.Value)
                    {
                        // Delimiter seen right after another yielded element (probably end of a quoted string).  Skip along.
                        lastYield++;
                        continue;
                    }

                    // Yield it back
                    yield return operandLine.Substring(lastYield ?? 0, i - (lastYield ?? 0) + 1).TrimEnd(',');
                    lastYield = i + 1;
                    continue;
                }
            }

            yield break;
        }

        private static readonly string[] registerNames = new string[] {
            "EAX", "AX", "AH", "AL",
            "EBX", "BX", "BH", "BL",
            "ECX", "CX", "CH", "CL",
            "EDX", "DX", "DH", "DL"
        };

        public static readonly char[] NUMERALS = new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' };

        public static ParameterType GetOperandType(string operand)
        {
            if (operand.StartsWith('[') && operand.EndsWith(']'))
                return ParameterType.RegisterAddress;
            if (registerNames.Contains(operand.ToUpperInvariant()))
                return ParameterType.RegisterReference;
            if (ulong.TryParse(operand, System.Globalization.NumberStyles.Integer, System.Globalization.NumberFormatInfo.InvariantInfo, out ulong operandl))
                return ParameterType.Constant;
            if (operand.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && ulong.TryParse(operand.Substring(2), System.Globalization.NumberStyles.HexNumber, System.Globalization.NumberFormatInfo.InvariantInfo, out ulong operandlh))
                return ParameterType.Constant;
            if (NUMERALS.Any(c => c == operand[0]) && operand.EndsWith("h", StringComparison.OrdinalIgnoreCase))
                return ParameterType.Constant;
            if (System.Text.RegularExpressions.Regex.IsMatch(operand, @"\w[\w\d]+"))
                return ParameterType.Variable;
            return ParameterType.Unknown;
        }

        private byte[] Add(string operand1, string operand2)
        {
            var o1Type = GetOperandType(operand1);
            var o2Type = GetOperandType(operand2);

            switch (o1Type)
            {
                case ParameterType.RegisterReference:
                    {
                        var o1Reg = registers[operand1.ToUpperInvariant()];
                        switch (o2Type)
                        {
                            case ParameterType.Constant:
                                {
                                    if (o1Reg == Register.EAX || o1Reg == Register.EBX || o1Reg == Register.ECX || o1Reg == Register.EDX)
                                    {
                                        var ret = new byte[6];
                                        ret[0] = (byte)Bytecode.ADD_REG_CON;
                                        ret[1] = (byte)registers[operand1.ToUpperInvariant()];
                                        Array.Copy(BitConverter.GetBytes(ParseUInt32Constant(operand2)), 0, ret, 2, 4);
                                        return ret;
                                    }
                                    else if (o1Reg == Register.AX || o1Reg == Register.BX || o1Reg == Register.CX || o1Reg == Register.DX)
                                    {
                                        var ret = new byte[4];
                                        ret[0] = (byte)Bytecode.ADD_REG_CON;
                                        ret[1] = (byte)registers[operand1.ToUpperInvariant()];
                                        Array.Copy(BitConverter.GetBytes(ParseUInt16Constant(operand2)), 0, ret, 2, 2);
                                        return ret;
                                    }
                                    else if (o1Reg == Register.AH || o1Reg == Register.AL
                                        || o1Reg == Register.BH || o1Reg == Register.BL
                                        || o1Reg == Register.CH || o1Reg == Register.CL
                                        || o1Reg == Register.DH || o1Reg == Register.DL)
                                    {
                                        var ret = new byte[3];
                                        ret[0] = (byte)Bytecode.ADD_REG_CON;
                                        ret[1] = (byte)registers[operand1.ToUpperInvariant()];
                                        ret[2] = ParseByteConstant(operand2);
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
                                    Array.Copy(BitConverter.GetBytes(ParseUInt32Constant(operand2)), 0, ret, 2, 4);
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

        private byte[] And(string operand1, string operand2)
        {
            var o1Type = GetOperandType(operand1);
            var o2Type = GetOperandType(operand2);

            switch (o1Type)
            {
                case ParameterType.RegisterReference:
                    {
                        var o1Reg = registers[operand1.ToUpperInvariant()];
                        switch (o2Type)
                        {
                            case ParameterType.Constant:
                                {
                                    if (o1Reg == Register.EAX || o1Reg == Register.EBX || o1Reg == Register.ECX || o1Reg == Register.EDX)
                                    {
                                        var ret = new byte[6];
                                        ret[0] = (byte)Bytecode.AND_REG_CON;
                                        ret[1] = (byte)registers[operand1.ToUpperInvariant()];
                                        Array.Copy(BitConverter.GetBytes(ParseUInt32Constant(operand2)), 0, ret, 2, 4);
                                        return ret;
                                    }
                                    else if (o1Reg == Register.AX || o1Reg == Register.BX || o1Reg == Register.CX || o1Reg == Register.DX)
                                    {
                                        var ret = new byte[4];
                                        ret[0] = (byte)Bytecode.AND_REG_CON;
                                        ret[1] = (byte)registers[operand1.ToUpperInvariant()];
                                        Array.Copy(BitConverter.GetBytes(ParseUInt16Constant(operand2)), 0, ret, 2, 2);
                                        return ret;
                                    }
                                    else if (o1Reg == Register.AH || o1Reg == Register.AL
                                        || o1Reg == Register.BH || o1Reg == Register.BL
                                        || o1Reg == Register.CH || o1Reg == Register.CL
                                        || o1Reg == Register.DH || o1Reg == Register.DL)
                                    {
                                        var ret = new byte[3];
                                        ret[0] = (byte)Bytecode.AND_REG_CON;
                                        ret[1] = (byte)registers[operand1.ToUpperInvariant()];
                                        ret[2] = ParseByteConstant(operand2);
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

        private byte[] Pop(string operand)
        {
            var ret = new byte[2];
            var operandType = GetOperandType(operand);

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
            var attributes = fi.GetCustomAttributes(typeof(DescriptionAttribute), false) as DescriptionAttribute[];
            if (attributes != null && attributes.Any())
            {
                return attributes.First().Description;
            }

            return value.ToString();
        }
    }

}
