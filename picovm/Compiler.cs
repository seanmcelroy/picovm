using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace agent_playground
{
    public class Compiler
    {
        public enum ParameterType
        {
            Unknown = 0,
            RegisterReference = 1,
            RegisterAddress = 2,
            Constant = 3
        }

        private readonly Dictionary<string, Bytecode> opcodes;
        private readonly Dictionary<string, Register> registers;

        public Compiler()
        {
            // Generate opcode dictionary
            opcodes = Enum.GetValues(typeof(Bytecode)).Cast<Bytecode>().ToDictionary(k => GetEnumDescription(k), v => v);
            registers = Enum.GetValues(typeof(Register)).Cast<Register>().ToDictionary(k => GetEnumDescription(k), v => v);
        }

        public IEnumerable<byte> Compile(IEnumerable<string> programLines)
        {
            ushort offsetBytes = 0;

            var labelsOffsets = new Dictionary<string, ushort>();

            foreach (var programLine in programLines)
            {
                // Knock off any comments
                var line = programLine.Split(';')[0].TrimEnd();

                if (line[0] == ' ' && line[line.Length - 1] == ':')
                {
                    labelsOffsets.Add(line.TrimEnd(':'), offsetBytes);
                    continue;
                }

                var lineParts = line.TrimStart(' ').Split(' ').Select(s => s.TrimEnd(',')).ToArray();
                var instruction = lineParts[0].ToUpperInvariant();

                // "Simple" assembly
                if (opcodes.ContainsKey(instruction))
                {
                    var opcode = opcodes[instruction];
                    yield return (byte)opcode;
                    offsetBytes++;
                    switch (opcode)
                    {
                        // END
                        case Bytecode.END:
                            break;
                    }
                }
                else if (instruction == "MOV")
                {
                    var dst = lineParts[lineParts.Length - 2];
                    var dstType = GetOperandType(dst);
                    var src = lineParts[lineParts.Length - 1];
                    var srcType = GetOperandType(src);

                    switch (dstType)
                    {
                        case ParameterType.RegisterReference:
                            {
                                switch (srcType)
                                {
                                    case ParameterType.RegisterReference:
                                        {
                                            yield return (byte)Bytecode.MOV_REG_REG;
                                            offsetBytes++;

                                            yield return (byte)registers[dst];
                                            offsetBytes++;
                                            yield return (byte)registers[src];
                                            offsetBytes++;
                                            continue;
                                        }
                                    case ParameterType.RegisterAddress:
                                        {
                                            yield return (byte)Bytecode.MOV_REG_MEM;
                                            offsetBytes++;

                                            yield return (byte)registers[dst];
                                            offsetBytes++;
                                            yield return (byte)registers[src.TrimStart('[').TrimEnd(']')];
                                            offsetBytes++;
                                            continue;
                                        }
                                    case ParameterType.Constant:
                                        {
                                            yield return (byte)Bytecode.MOV_REG_CON;
                                            offsetBytes++;

                                            var dstReg = registers[dst];
                                            yield return (byte)dstReg;
                                            offsetBytes++;

                                            if (dstReg == Register.EAX || dstReg == Register.EBX)
                                            {
                                                foreach (var b in BitConverter.GetBytes(uint.Parse(src)))
                                                    yield return b;
                                                offsetBytes += 4;
                                            }
                                            else if (dstReg == Register.AX)
                                            {
                                                foreach (var b in BitConverter.GetBytes(ushort.Parse(src)))
                                                    yield return b;
                                                offsetBytes += 2;
                                            }
                                            else if (dstReg == Register.AH || dstReg == Register.AL)
                                            {
                                                yield return byte.Parse(src);
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
                    var bytecode = Pop(lineParts[lineParts.Length - 1]);
                    foreach (var b in bytecode)
                        yield return b;
                    offsetBytes += (ushort)bytecode.Length;
                }
                else if (instruction == "PUSH")
                {
                    var operand = lineParts[lineParts.Length - 1];
                    var operandType = GetOperandType(operand);

                    switch (operandType)
                    {
                        case ParameterType.RegisterReference:
                            {
                                yield return (byte)Bytecode.PUSH_REG;
                                offsetBytes++;
                                yield return (byte)registers[operand];
                                offsetBytes++;
                                continue;
                            }
                        case ParameterType.RegisterAddress:
                            {
                                yield return (byte)Bytecode.PUSH_MEM;
                                offsetBytes++;
                                yield return (byte)registers[operand.TrimStart('[').TrimEnd(']')];
                                offsetBytes++;
                                continue;
                            }
                        case ParameterType.Constant:
                            {
                                yield return (byte)Bytecode.PUSH_CON;
                                offsetBytes++;
                                foreach (var b in BitConverter.GetBytes(uint.Parse(operand)))
                                    yield return b;

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
                    var bytecode = Add(lineParts[lineParts.Length - 2], lineParts[lineParts.Length - 1]);
                    foreach (var b in bytecode)
                        yield return b;
                    offsetBytes += (ushort)bytecode.Length;
                }
                else
                {
                    throw new Exception($"ERROR: Cannot compile: {line}");
                }
            }
        }

        public static ParameterType GetOperandType(string operand) => operand.StartsWith('[') && operand.EndsWith(']')
                                    ? ParameterType.RegisterAddress
                                    : ((new string[] { "EAX", "AX", "AH", "AL", "EBX" }).Contains(operand) ? ParameterType.RegisterReference
                                        : ((ulong.TryParse(operand, System.Globalization.NumberStyles.Integer, System.Globalization.NumberFormatInfo.InvariantInfo, out ulong operandl) ? ParameterType.Constant : ParameterType.Unknown)));

        private byte[] Add(string operand1, string operand2)
        {
            var o1Type = GetOperandType(operand1);
            var o2Type = GetOperandType(operand2);

            switch (o1Type)
            {
                case ParameterType.RegisterReference:
                    {
                        switch (o2Type)
                        {
                            case ParameterType.Constant:
                                {
                                    var ret = new byte[6];
                                    ret[0] = (byte)Bytecode.ADD_REG_CON;
                                    ret[1] = (byte)registers[operand1];
                                    Array.Copy(BitConverter.GetBytes(uint.Parse(operand2)), 0, ret, 2, 4);
                                    return ret;
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
                                    ret[1] = (byte)registers[operand1.TrimStart('[').TrimEnd(']')];
                                    Array.Copy(BitConverter.GetBytes(uint.Parse(operand2)), 0, ret, 2, 4);
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

        private byte[] Pop(string operand)
        {
            var ret = new byte[2];
            var operandType = GetOperandType(operand);

            switch (operandType)
            {
                case ParameterType.RegisterReference:
                    {
                        ret[0] = (byte)Bytecode.POP_REG;
                        ret[1] = (byte)registers[operand];
                        break;
                    }
                case ParameterType.RegisterAddress:
                    {
                        ret[0] = (byte)Bytecode.POP_MEM;
                        ret[1] = (byte)registers[operand.TrimStart('[').TrimEnd(']')];
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
