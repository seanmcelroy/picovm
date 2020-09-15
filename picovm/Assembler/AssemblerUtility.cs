using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace picovm.Assembler
{
    public static class AssemblerUtility
    {
        internal static readonly char[] NUMERALS = new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' };

        internal static readonly string[] REGISTER_NAMES = new string[] {
            "RAX", "RBX", "RCX", "RDX",
            "R8", "R9", "R10", "R11",
            "R12", "R13", "R14", "R15",
            "EAX", "AX", "AH", "AL",
            "EBX", "BX", "BH", "BL",
            "ECX", "CX", "CH", "CL",
            "EDX", "DX", "DH", "DL",
            "RSI", "ESI", "SI",
            "RDI", "EDI", "DI",
        };

        public static ulong ParseUInt64Constant(this string operand)
        {
            if (operand.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return ulong.Parse(operand.Substring(2), System.Globalization.NumberStyles.HexNumber);

            if (NUMERALS.Any(c => c == operand[0]) && operand.EndsWith("h", StringComparison.OrdinalIgnoreCase))
                return ulong.Parse(operand.Substring(0, operand.Length - 1), System.Globalization.NumberStyles.HexNumber);

            return ulong.Parse(operand);
        }

        public static uint ParseUInt32Constant(this string operand)
        {
            if (operand.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return uint.Parse(operand.Substring(2), System.Globalization.NumberStyles.HexNumber);

            if (NUMERALS.Any(c => c == operand[0]) && operand.EndsWith("h", StringComparison.OrdinalIgnoreCase))
                return uint.Parse(operand.Substring(0, operand.Length - 1), System.Globalization.NumberStyles.HexNumber);

            return uint.Parse(operand);
        }

        public static ushort ParseUInt16Constant(this string operand)
        {
            if (operand.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return ushort.Parse(operand.Substring(2), System.Globalization.NumberStyles.HexNumber);

            if (NUMERALS.Any(c => c == operand[0]) && operand.EndsWith("h", StringComparison.OrdinalIgnoreCase))
                return ushort.Parse(operand.Substring(0, operand.Length - 1), System.Globalization.NumberStyles.HexNumber);

            return ushort.Parse(operand);
        }

        public static byte ParseByteConstant(this string operand)
        {
            if (operand.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return byte.Parse(operand.Substring(2), System.Globalization.NumberStyles.HexNumber);

            if (NUMERALS.Any(c => c == operand[0]) && operand.EndsWith("h", StringComparison.OrdinalIgnoreCase))
                return byte.Parse(operand.Substring(0, operand.Length - 1), System.Globalization.NumberStyles.HexNumber);

            return byte.Parse(operand);
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

        public static ParameterType GetOperandType(string operand)
        {
            if (operand.StartsWith('[') && operand.EndsWith(']'))
            {
                if (AssemblerUtility.REGISTER_NAMES.Any(r => string.Compare(r, operand.Substring(1, operand.Length - 2), StringComparison.InvariantCultureIgnoreCase) == 0))
                    return ParameterType.RegisterAddress;
                else
                    return ParameterType.VariableAddress;
            }
            if (AssemblerUtility.REGISTER_NAMES.Contains(operand.ToUpperInvariant()))
                return ParameterType.RegisterReference;
            if (ulong.TryParse(operand, System.Globalization.NumberStyles.Integer, System.Globalization.NumberFormatInfo.InvariantInfo, out ulong operandl))
                return ParameterType.Constant;
            if (operand.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && ulong.TryParse(operand.Substring(2), System.Globalization.NumberStyles.HexNumber, System.Globalization.NumberFormatInfo.InvariantInfo, out ulong operandlh))
                return ParameterType.Constant;
            if (AssemblerUtility.NUMERALS.Any(c => c == operand[0]) && operand.EndsWith("h", StringComparison.OrdinalIgnoreCase))
                return ParameterType.Constant;
            if (System.Text.RegularExpressions.Regex.IsMatch(operand, @"\w[\w\d]+"))
                return ParameterType.Variable;
            return ParameterType.Unknown;
        }

        public static object UnboxParsedOperand(string operandPart)
        {
            if (operandPart.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (byte.TryParse(operandPart.Substring(2), NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out byte parsedByte))
                    return parsedByte;
                if (UInt16.TryParse(operandPart.Substring(2), NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out ushort parsedU16))
                    return parsedU16;
                if (UInt32.TryParse(operandPart.Substring(2), NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out uint parsedU32))
                    return parsedU32;
                if (UInt64.TryParse(operandPart.Substring(2), NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out ulong parsedU64))
                    return parsedU64;
                throw new InvalidOperationException($"Unable to parse operand appearing to be a hexadecimal number: {operandPart}");
            }

            if (AssemblerUtility.NUMERALS.Any(c => c == operandPart[0]) && operandPart.EndsWith("h", StringComparison.OrdinalIgnoreCase))
            {
                if (byte.TryParse(operandPart.Substring(0, operandPart.Length - 1), NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out byte parsedByte))
                    return parsedByte;
                if (UInt16.TryParse(operandPart.Substring(0, operandPart.Length - 1), NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out ushort parsedU16))
                    return parsedU16;
                if (UInt32.TryParse(operandPart.Substring(0, operandPart.Length - 1), NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out uint parsedU32))
                    return parsedU32;
                if (UInt64.TryParse(operandPart.Substring(0, operandPart.Length - 1), NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out ulong parsedU64))
                    return parsedU64;
                throw new InvalidOperationException($"Unable to parse operand appearing to be a hexadecimal number: {operandPart}");
            }

            {
                if (byte.TryParse(operandPart, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out byte parsedByte))
                    return parsedByte;
                if (UInt16.TryParse(operandPart, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out ushort parsedU16))
                    return parsedU16;
                if (UInt32.TryParse(operandPart, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out uint parsedU32))
                    return parsedU32;
                if (UInt64.TryParse(operandPart, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out ulong parsedU64))
                    return parsedU64;
                if (double.TryParse(operandPart, NumberStyles.Number, NumberFormatInfo.InvariantInfo, out double parsedDouble))
                    return parsedDouble;
            }

            if (operandPart.StartsWith('\'') && operandPart.EndsWith('\'') && operandPart.Length >= 2)
                operandPart = operandPart.Substring(1, operandPart.Length - 2);
            else if (operandPart.StartsWith('\"') && operandPart.EndsWith('\"') && operandPart.Length >= 2)
                operandPart = operandPart.Substring(1, operandPart.Length - 2);

            return operandPart;
        }


        public static ValueType ResolveDataAllocationReference(string operandPart, Dictionary<string, IBytecodeDataSymbol> symbolOffsets)
        {
            if (TryResolveDataAllocationReference(operandPart, symbolOffsets, out ValueType result))
                return result;

            throw new InvalidOperationException($"Unable to resolve operand: {operandPart}");
        }

        public static bool TryResolveDataAllocationReference(string operandPart, Dictionary<string, IBytecodeDataSymbol> symbolOffsets, out ValueType result)
        {
            var unboxAttempt = AssemblerUtility.UnboxParsedOperand(operandPart);
            if (unboxAttempt is ValueType)
            {
                result = (ValueType)unboxAttempt;
                return true;
            }
            else if (unboxAttempt.GetType() == typeof(string) && symbolOffsets.ContainsKey(operandPart.ToUpperInvariant()))
            {
                result = symbolOffsets[operandPart.ToUpperInvariant()].DataSegmentOffset;
                return true;
            }

            result = 0;
            return false;
        }
    }
}