using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace picovm.Assembler
{
    public sealed class CompilerDataAllocationDirective
    {
        public static readonly string[] SYMBOLS = new string[] {
            "DB", "DW", "DD", "DQ", "DUP", "EQU"
        };

        public string? Label { get; private set; }

        public string Mnemonic { get; private set; }

        public string[] Operands { get; private set; }

        private CompilerDataAllocationDirective(string? label, string mnemonic, string[] operands)
        {
            this.Label = label;
            this.Mnemonic = mnemonic;
            this.Operands = operands;
        }

        public static CompilerDataAllocationDirective ParseLine(string directiveLine)
        {
            var lineParts = directiveLine.Split(new char[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);

            // Ignore whitespace between the first token and the second if the second is a colon.  Poorly formatted label.
            if (lineParts.Length > 2 && lineParts[1].Length == 1 && lineParts[1][0] == ':')
            {
                var respin = new List<string>(new string[] { lineParts.Take(2).Aggregate((c, n) => c + n) });
                respin.AddRange(lineParts.Skip(2));
                lineParts = respin.ToArray();
            }

            string? label = null;
            if (SYMBOLS.Any(s => string.Compare(s, lineParts[0], StringComparison.InvariantCultureIgnoreCase) != 0 &&
                SYMBOLS.Any(s => string.Compare(s, lineParts[1], StringComparison.InvariantCultureIgnoreCase) == 0)))
                label = lineParts[0].TrimEnd(':');

            var labelIndex = (label == null) ? default(int?) : directiveLine.IndexOf(label);

            var mnemonic = labelIndex == null ? lineParts[0] : lineParts[1];
            var mnemonicIndex = directiveLine.Substring(labelIndex ?? 0).IndexOf(mnemonic);

            var operandLine = directiveLine.Substring(mnemonicIndex + mnemonic.Length).TrimStart(' ', '\t');
            var operands = AssemblerUtility.ParseOperandLine(operandLine).ToArray();

            return new CompilerDataAllocationDirective(label, mnemonic, operands);
        }

        public static Queue<object> ConvertInfixToReversePolishNotation<TAddrSize>(IEnumerable<string> tokens, ValueType offsetBytes)
            where TAddrSize : struct
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