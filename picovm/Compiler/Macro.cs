using System.Collections.Generic;

namespace picovm.Compiler
{
    public struct Macro
    {
        public string name;
        public byte parameterCount;
        public List<string> macroLines;
    }
}