using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace picovm.Compiler
{
    public sealed class CompilerDataAllocationDirective
    {
        public static readonly string[] SYMBOLS = new string[] {
            "DB", "DW", "DD", "DQ", "DUP", "EQU"
        };

        public string Label { get; private set; }

        public string Mnemonic { get; private set; }

        public string[] Operands { get; private set; }

        private CompilerDataAllocationDirective()
        {
        }

        public static CompilerDataAllocationDirective ParseLine(string directiveLine)
        {
            var ret = new CompilerDataAllocationDirective();

            var lineParts = directiveLine.Split(new char[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);

            // Ignore whitespace between the first token and the second if the second is a colon.  Poorly formatted label.
            if (lineParts.Length > 2 && lineParts[1].Length == 1 && lineParts[1][0] == ':')
            {
                var respin = new List<string>(new string[] { lineParts.Take(2).Aggregate((c, n) => c + n) });
                respin.AddRange(lineParts.Skip(2));
                lineParts = respin.ToArray();
            }

            if (SYMBOLS.Any(s => string.Compare(s, lineParts[0], StringComparison.InvariantCultureIgnoreCase) != 0 &&
                SYMBOLS.Any(s => string.Compare(s, lineParts[1], StringComparison.InvariantCultureIgnoreCase) == 0)))
                ret.Label = lineParts[0].TrimEnd(':');

            var labelIndex = (ret.Label == null) ? default(int?) : directiveLine.IndexOf(ret.Label);

            ret.Mnemonic = labelIndex == null ? lineParts[0] : lineParts[1];
            var mnemonicIndex = directiveLine.Substring(labelIndex ?? 0).IndexOf(ret.Mnemonic);

            var operandLine = directiveLine.Substring(mnemonicIndex + ret.Mnemonic.Length).TrimStart(' ', '\t');
            ret.Operands = BytecodeCompiler.ParseOperandLine(operandLine).ToArray();

            return ret;
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

            if (BytecodeCompiler.NUMERALS.Any(c => c == operandPart[0]) && operandPart.EndsWith("h", StringComparison.OrdinalIgnoreCase))
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

        public static ValueType ResolveDataAllocationReference(string operandPart, Dictionary<string, BytecodeDataSymbol> symbolOffsets)
        {
            if (TryResolveDataAllocationReference(operandPart, symbolOffsets, out ValueType result))
                return result;

            throw new InvalidOperationException($"Unable to resolve operand: {operandPart}");
        }

        public static bool TryResolveDataAllocationReference(string operandPart, Dictionary<string, BytecodeDataSymbol> symbolOffsets, out ValueType result)
        {
            var unboxAttempt = UnboxParsedOperand(operandPart);
            if (unboxAttempt is ValueType)
            {
                result = (ValueType)unboxAttempt;
                return true;
            }
            else if (unboxAttempt.GetType() == typeof(string) && symbolOffsets.ContainsKey(operandPart.ToUpperInvariant()))
            {
                result = symbolOffsets[operandPart.ToUpperInvariant()].dataSegmentOffset;
                return true;
            }

            result = 0;
            return false;
        }

        public static Queue<object> ConvertInfixToReversePolishNotation(IEnumerable<string> tokens, ushort offsetBytes)
        {
            var rpnQueue = new Queue<object>();
            var rpnStack = new Stack<string>();

            var precedence = new Dictionary<string, int>() {
                {"^",6},
                {"/",5},
                {"*",5},
                {"+",4},
                {"-",4},
                {"(",0},
            };

            // Some tokens may be run together with no whitespacing, but are separated by operators.
            var operators = precedence.Select(k => k.Key[0]).ToArray();
            var respinList = new List<string>();
            foreach (var token in tokens)
            {
                if (operators.Any(o => token.Contains(o)))
                {
                    var sb = new StringBuilder();
                    var j = 0;
                    for (var i = 0; i < token.Length; i++)
                    {
                        var c = token[i];
                        var matchOperator = operators.SingleOrDefault(o => c == o);
                        if (!default(char).Equals(matchOperator))
                        {
                            if (i > 0)
                                respinList.Add(token.Substring(j, i));
                            respinList.Add(c.ToString());
                            j = i;
                        }
                    }
                    if (j + 1 < token.Length)
                        respinList.Add(token.Substring(j + 1));
                }
                else
                    respinList.Add(token);
            }

            foreach (var infix in respinList)
            {
                object infixValue;
                if (string.Compare("$", infix, StringComparison.InvariantCulture) == 0)
                    infixValue = offsetBytes;
                else if (byte.TryParse(infix, out byte infixByte))
                    infixValue = infixByte;
                else if (ushort.TryParse(infix, out ushort infixUshort))
                    infixValue = infixUshort;
                else if (uint.TryParse(infix, out uint infixUInt))
                    infixValue = infixUInt;
                else if (ulong.TryParse(infix, out ulong infixULong))
                    infixValue = infixULong;
                else
                    infixValue = infix;

                var infixValueType = infixValue.GetType();

                // Step 1 - if is a number: add it to output queue
                if (infixValueType == typeof(byte)
                || infixValueType == typeof(ushort)
                || infixValueType == typeof(uint)
                || infixValueType == typeof(ulong))
                {
                    rpnQueue.Enqueue(infixValue);
                    continue;
                }

                // Step 2 - if ( push it on the stack.
                if (infixValueType == typeof(string) && string.Compare("(", (string)infixValue) == 0)
                {
                    rpnStack.Push("(");
                    continue;
                }

                // Step 3 -- if is ')' :{ Pop items off stack to output Q until '(' reached and delete the '(' from the stack S. }
                if (infixValueType == typeof(string) && string.Compare(")", (string)infixValue) == 0)
                {
                    do
                    {
                        rpnQueue.Enqueue(rpnStack.Pop());
                    } while (string.Compare(rpnStack.Peek().ToString(), "(", StringComparison.InvariantCulture) != 0);
                    rpnStack.Pop(); // Pop final '('
                    continue;
                }

                // Step 4
                if (infixValueType == typeof(string) && precedence.ContainsKey((string)infixValue))
                {
                    while (rpnStack.Count > 0 && precedence[rpnStack.Peek()] >= precedence[infix])
                        rpnQueue.Enqueue(rpnStack.Pop());
                    rpnStack.Push(infix);
                    continue;
                }

                // Step 5 - Assume it is a symbol
                rpnQueue.Enqueue(infixValue);
                continue;
            }

            while (rpnStack.Count > 0)
                rpnQueue.Enqueue(rpnStack.Pop());

            return rpnQueue;
        }
    }
}