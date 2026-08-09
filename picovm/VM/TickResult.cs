namespace picovm.VM
{
    public readonly struct TickResult(TickErrorCode ErrorCode, bool Done, params ExecutionError[] Errors)
    {
        public readonly TickErrorCode ErrorCode = ErrorCode;
        public readonly ExecutionError[] Errors = Errors;
        public readonly bool Done = Done;
    }
}