using System.Collections.Generic;

namespace picovm.Compiler
{
    public sealed class CompileTextSectionResult
    {
        public byte[] Bytecode { get; private set; }
        public Dictionary<string, uint> LabelsOffsets { get; private set; }
        public List<BytecodeTextSymbol> SymbolReferenceOffsets { get; private set; }

        public CompileTextSectionResult(byte[] bytecode, Dictionary<string, uint> labelOffsets, List<BytecodeTextSymbol> symbolReferenceOffsets)
        {
            this.Bytecode = bytecode;
            this.LabelsOffsets = labelOffsets;
            this.SymbolReferenceOffsets = symbolReferenceOffsets;
        }
    }
}
