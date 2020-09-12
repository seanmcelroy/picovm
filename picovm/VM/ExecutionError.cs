namespace picovm.VM
{
    public sealed class ExecutionError
    {
        public string Message { get; private set; }
        public string? SourceFile { get; private set; }
        public ushort? LineNumber { get; private set; }
        public ushort? Column { get; private set; }

        public ExecutionError(string message, string? sourceFile = null, ushort? lineNumber = null, ushort? column = null)
        {
            this.Message = message;
            this.SourceFile = sourceFile;
            this.LineNumber = lineNumber;
            this.Column = column;

            System.Console.Error.WriteLine(ToString());
        }

        public override string ToString() => $"{Message} in {SourceFile}({LineNumber}:{Column})";
    }
}
