using System.Collections.Generic;

namespace picovm.Assembler
{
    public readonly struct Macro(string name, byte parameterCount, IEnumerable<string> macroLines)
    {
        public readonly string Name = name;
        public readonly byte ParameterCount = parameterCount;
        public readonly List<string> MacroLines = [.. macroLines];
    }
}