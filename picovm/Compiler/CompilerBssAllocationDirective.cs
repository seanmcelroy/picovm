using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace picovm.Compiler
{
    public sealed class CompilerBssAllocationDirective
    {
        public static readonly string[] SYMBOLS = new string[] {
            "RESB", "RESW", "RESD", "RESQ"
        };

        public string Label { get; private set; }

        public string Mnemonic { get; private set; }

        public ushort Size { get; private set; }

        private CompilerBssAllocationDirective()
        {
        }

        public static CompilerBssAllocationDirective ParseLine(string directiveLine)
        {
            var ret = new CompilerBssAllocationDirective();

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
            ret.Size = ushort.Parse(operandLine, NumberStyles.Integer);
            return ret;
        }
    }
}