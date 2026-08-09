using System.Collections.Generic;
using System.Collections.Immutable;

namespace picovm.VM
{
    public readonly struct ExecutionResult(
        int ErrorCode,
        params IEnumerable<ExecutionError> Errors)
    {
        public readonly int ErrorCode = ErrorCode;
        public readonly ImmutableList<ExecutionError> Errors = Errors == null ? [] : [.. Errors];
        public readonly bool Success => Errors == null || Errors.Count == 0;

        public static ExecutionResult Error(int errorCode, string message, string? sourceFile = null, ushort? lineNumber = null, ushort? column = null)
        {
            return new ExecutionResult(errorCode, [new ExecutionError(message, sourceFile, lineNumber, column)]);
        }
    }
}