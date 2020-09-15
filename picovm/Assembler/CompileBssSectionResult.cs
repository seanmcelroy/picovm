using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace picovm.Assembler
{
    public sealed class CompileBssSectionResult
    {
        public ImmutableList<BytecodeBssSymbol> Symbols { get; private set; }

        public CompileBssSectionResult(IEnumerable<BytecodeBssSymbol> symbols) =>
            this.Symbols = ImmutableList<BytecodeBssSymbol>.Empty.AddRange(symbols);

        public static CompileBssSectionResult CompileBssSectionLines(IEnumerable<string> dataLines)
        {
            var symbols = new List<BytecodeBssSymbol>();

            foreach (var dataLine in dataLines)
            {
                // Knock off any comments
                var line = dataLine.Split(';')[0].Trim();
                var bssAllocationDirective = CompilerBssAllocationDirective.ParseLine(line);

                if (string.Compare("resb", bssAllocationDirective.Mnemonic, StringComparison.InvariantCultureIgnoreCase) == 0)
                    symbols.Add(new BytecodeBssSymbol(bssAllocationDirective.Label, BytecodeBssSymbol.BssType.Byte, bssAllocationDirective.Size));
                else if (string.Compare("resw", bssAllocationDirective.Mnemonic, StringComparison.InvariantCultureIgnoreCase) == 0)
                    symbols.Add(new BytecodeBssSymbol(bssAllocationDirective.Label, BytecodeBssSymbol.BssType.Word, bssAllocationDirective.Size));
                else if (string.Compare("resd", bssAllocationDirective.Mnemonic, StringComparison.InvariantCultureIgnoreCase) == 0)
                    symbols.Add(new BytecodeBssSymbol(bssAllocationDirective.Label, BytecodeBssSymbol.BssType.DoubleWord, bssAllocationDirective.Size));
                else if (string.Compare("resq", bssAllocationDirective.Mnemonic, StringComparison.InvariantCultureIgnoreCase) == 0)
                    symbols.Add(new BytecodeBssSymbol(bssAllocationDirective.Label, BytecodeBssSymbol.BssType.QuadWord, bssAllocationDirective.Size));
                else
                    throw new InvalidOperationException($"Unknown mnemonic: {bssAllocationDirective.Mnemonic}");
            }

            return new CompileBssSectionResult(symbols);
        }
    }
}
