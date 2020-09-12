using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace picovm.VM
{
    public sealed class ExecutionResult
    {
        public readonly int ErrorCode;
        public readonly ImmutableList<ExecutionError> Errors;
        public bool Success => Errors == null || Errors.Count == 0;

        public ExecutionResult(
            int errorCode,
            IEnumerable<ExecutionError>? errors = null)
        {
            this.ErrorCode = errorCode;
            this.Errors = errors == null ? ImmutableList<ExecutionError>.Empty : ImmutableList<ExecutionError>.Empty.AddRange(errors);
        }

        public static ExecutionResult Error(int errorCode, string message, string? sourceFile = null, ushort? lineNumber = null, ushort? column = null)
        {
            return new ExecutionResult(errorCode, new[] { new ExecutionError(message, sourceFile, lineNumber, column) });
        }
    }
}