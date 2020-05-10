using System.ComponentModel;
using System.Collections.Generic;

namespace agent_playground
{
    public sealed class CompileTextSectionResult
    {
        public byte[] Bytecode { get; private set; }
        public Dictionary<string, ushort> LabelsOffsets { get; private set; }
        public List<BytecodeTextSymbol> SymbolReferenceOffsets { get; private set; }

        public CompileTextSectionResult(byte[] bytecode, Dictionary<string, ushort> labelOffsets, List<BytecodeTextSymbol> symbolReferenceOffsets)
        {
            this.Bytecode = bytecode;
            this.LabelsOffsets = labelOffsets;
            this.SymbolReferenceOffsets = symbolReferenceOffsets;
        }
    }
}
