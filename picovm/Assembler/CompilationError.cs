namespace picovm.Assembler
{
    public readonly struct CompilationError(string message, string? sourceFile = null, ushort? lineNumber = null, ushort? column = null)
    {
        public readonly string Message = message;
        public readonly string? SourceFile = sourceFile;
        public readonly ushort? LineNumber = lineNumber;
        public readonly ushort? Column = column;

        public override string ToString() => $"{Message} in {SourceFile}({LineNumber}:{Column})";
    }
}
