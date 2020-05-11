using System.Collections.Generic;

namespace picovm.Compiler
{
    public sealed class CompileDataSectionResult
    {
        public byte[] Bytecode { get; private set; }
        public Dictionary<string, BytecodeDataSymbol> SymbolOffsets { get; private set; }

        public CompileDataSectionResult(byte[] bytecode, Dictionary<string, BytecodeDataSymbol> symbolOffsets)
        {
            this.Bytecode = bytecode;
            this.SymbolOffsets = symbolOffsets;
        }
    }
}
