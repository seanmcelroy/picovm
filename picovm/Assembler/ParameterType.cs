namespace picovm.Assembler
{
    public enum ParameterType
    {
        Unknown = 0,
        RegisterReference = 1,
        RegisterAddress = 2,
        Constant = 3,
        Variable = 4,
        VariableAddress = 5
    }
}
