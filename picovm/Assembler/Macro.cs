using System.Collections.Generic;

namespace picovm.Assembler
{
    public readonly struct Macro
    {
        public readonly string Name;
        public readonly byte ParameterCount;
        public readonly List<string> MacroLines;

        public Macro(string name, byte parameterCount, IEnumerable<string> macroLines)
        {
            this.Name = name;
            this.ParameterCount = parameterCount;
            this.MacroLines = new List<string>(macroLines);
        }
    }
}