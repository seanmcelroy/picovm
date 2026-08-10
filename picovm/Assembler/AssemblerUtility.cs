using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace picovm.Assembler
{
    public static class AssemblerUtility
    {
        internal static readonly char[] NUMERALS = ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9'];

        internal static readonly string[] REGISTER_NAMES = [
            "RAX", "RBX", "RCX", "RDX",
            "R8", "R9", "R10", "R11",
            "R12", "R13", "R14", "R15",
            "EAX", "AX", "AH", "AL",
            "EBX", "BX", "BH", "BL",
            "ECX", "CX", "CH", "CL",
            "EDX", "DX", "DH", "DL",
            "RSI", "ESI", "SI",
            "RDI", "EDI", "DI",
            "RBP", "EBP", "BP",
            "RIP", "EIP", "IP",
            "RSP", "ESP", "SP",
            "CS", "DS", "SS", "ES", "FS", "GS"
        ];

        public static ulong ParseUInt64Constant(this string operand)
        {
            if (operand.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return ulong.Parse(operand[2..], NumberStyles.HexNumber);

            if (NUMERALS.Any(c => c == operand[0]) && operand.EndsWith("h", StringComparison.OrdinalIgnoreCase))
                return ulong.Parse(operand[..^1], NumberStyles.HexNumber);

            return ulong.Parse(operand);
        }

        public static uint ParseUInt32Constant(this string operand)
        {
            if (operand.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return uint.Parse(operand[2..], NumberStyles.HexNumber);

            if (NUMERALS.Any(c => c == operand[0]) && operand.EndsWith("h", StringComparison.OrdinalIgnoreCase))
                return uint.Parse(operand[..^1], NumberStyles.HexNumber);

            return uint.Parse(operand);
        }

        public static ushort ParseUInt16Constant(this string operand)
        {
            if (operand.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return ushort.Parse(operand[2..], NumberStyles.HexNumber);

            if (NUMERALS.Any(c => c == operand[0]) && operand.EndsWith("h", StringComparison.OrdinalIgnoreCase))
                return ushort.Parse(operand[..^1], NumberStyles.HexNumber);

            return ushort.Parse(operand);
        }

        public static byte ParseByteConstant(this string operand)
        {
            if (operand.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return byte.Parse(operand[2..], NumberStyles.HexNumber);

            if (NUMERALS.Any(c => c == operand[0]) && operand.EndsWith("h", StringComparison.OrdinalIgnoreCase))
                return byte.Parse(operand[..^1], NumberStyles.HexNumber);

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
                        yield return operandLine[..i];
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
                        yield return operandLine[lastYield.Value..i];
                        lastYield = i + 1;
                        continue;
                    }
                    continue;
                }

                if (c == ',')
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

                if (i == operandLine.Length - 1)
                {
                    // Final character of the line, so close out whatever operand is still open.
                    // This must not reuse the delimiter skip above: a one-character trailing
                    // operand begins at exactly the position the previous element ended, so
                    // treating it as a delimiter that follows an already-yielded element
                    // silently discards it.  That dropped the last byte of every data
                    // directive ending in a single character, such as "db 0, 0, 0, 0".
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
                if (REGISTER_NAMES.Any(r => string.Compare(r, operand[1..^1], StringComparison.InvariantCultureIgnoreCase) == 0))
                    return ParameterType.RegisterIndirect;
                else
                    return ParameterType.VariableDirect;
            }
            if (REGISTER_NAMES.Contains(operand.ToUpperInvariant()))
                return ParameterType.RegisterReference;
            if (ulong.TryParse(operand, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out ulong operandl))
                return ParameterType.Constant;
            if (operand.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && ulong.TryParse(operand[2..], NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out ulong operandlh))
                return ParameterType.Constant;
            if (NUMERALS.Any(c => c == operand[0]) && operand.EndsWith("h", StringComparison.OrdinalIgnoreCase))
                return ParameterType.Constant;
            if (System.Text.RegularExpressions.Regex.IsMatch(operand, @"\w[\w\d]*"))
                return ParameterType.VariableAddress;
            return ParameterType.Unknown;
        }

        public static object UnboxParsedOperand(string operandPart)
        {
            if (operandPart.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (byte.TryParse(operandPart[2..], NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out byte parsedByte))
                    return parsedByte;
                if (UInt16.TryParse(operandPart[2..], NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out ushort parsedU16))
                    return parsedU16;
                if (UInt32.TryParse(operandPart[2..], NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out uint parsedU32))
                    return parsedU32;
                if (UInt64.TryParse(operandPart[2..], NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out ulong parsedU64))
                    return parsedU64;
                throw new InvalidOperationException($"Unable to parse operand appearing to be a hexadecimal number: {operandPart}");
            }

            if (NUMERALS.Any(c => c == operandPart[0]) && operandPart.EndsWith("h", StringComparison.OrdinalIgnoreCase))
            {
                if (byte.TryParse(operandPart[..^1], NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out byte parsedByte))
                    return parsedByte;
                if (UInt16.TryParse(operandPart[..^1], NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out ushort parsedU16))
                    return parsedU16;
                if (UInt32.TryParse(operandPart[..^1], NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out uint parsedU32))
                    return parsedU32;
                if (UInt64.TryParse(operandPart[..^1], NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out ulong parsedU64))
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
                operandPart = operandPart[1..^1];
            else if (operandPart.StartsWith('\"') && operandPart.EndsWith('\"') && operandPart.Length >= 2)
                operandPart = operandPart[1..^1];

            return operandPart;
        }

        public static TAddrSize ResolveDataAllocationReference<TAddrSize>(string operandPart, Dictionary<string, BytecodeDataSymbol<TAddrSize>> symbolOffsets)
            where TAddrSize : struct, IComparable, IComparable<TAddrSize>, IConvertible, IEquatable<TAddrSize>, IFormattable
        {
            if (TryResolveDataAllocationReference(operandPart, symbolOffsets, out TAddrSize result))
                return result;

            throw new InvalidOperationException($"Unable to resolve operand: {operandPart}");
        }

        public static bool TryResolveDataAllocationReference<TAddrSize>(string operandPart, Dictionary<string, BytecodeDataSymbol<TAddrSize>> symbolOffsets, out TAddrSize result)
            where TAddrSize : struct, IComparable, IComparable<TAddrSize>, IConvertible, IEquatable<TAddrSize>, IFormattable
        {
            var unboxAttempt = UnboxParsedOperand(operandPart);
            if (unboxAttempt is TAddrSize vt)
            {
                result = vt;
                return true;
            }
            else if (unboxAttempt is string && symbolOffsets.ContainsKey(operandPart.ToUpperInvariant()))
            {
                result = symbolOffsets[operandPart.ToUpperInvariant()].DataSegmentOffset;
                return true;
            }

            result = typeof(TAddrSize) == typeof(UInt32) ? (TAddrSize)(ValueType)(UInt32)0 : (TAddrSize)(ValueType)(UInt64)0;
            return false;
        }
    }
}