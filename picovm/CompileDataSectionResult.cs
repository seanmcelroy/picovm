using System.Collections.Generic;

namespace agent_playground
{
    public sealed class CompileDataSection
    {
        public byte[] Bytecode { get; private set; }
        public Dictionary<string, BytecodeDataSymbol> SymbolOffsets { get; private set; }

        public CompileDataSection(byte[] bytecode, Dictionary<string, BytecodeDataSymbol> symbolOffsets)
        {
            this.Bytecode = bytecode;
            this.SymbolOffsets = symbolOffsets;
        }
    }
}
