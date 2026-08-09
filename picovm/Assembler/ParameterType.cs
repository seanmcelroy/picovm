namespace picovm.Assembler
{
    public enum ParameterType
    {
        Unknown = 0,
        /// <summary>
        /// Register addressing mode.
        /// (e.g.: EBX)
        /// </summary>
        RegisterReference = 1,
        /// <summary>
        /// Indirect addressing mode.
        /// Register holds an address (e.g.: [EBX])
        /// </summary>
        RegisterIndirect = 2,
        /// <summary>
        /// Immediate addressing mode
        /// (e.g.: a constant, like `65`)
        /// </summary>
        Constant = 3,
        /// <summary>
        /// Immediate addressing mode
        /// (e.g.: a constant, like `counter` without any brackets)
        /// </summary>
        VariableAddress = 4,
        /// <summary>
        /// Direct addressing mode.
        /// Instruction holds the address (e.g.: [counter])
        /// </summary>
        VariableDirect = 5
    }
}
