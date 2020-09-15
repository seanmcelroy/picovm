namespace picovm.Assembler
{
    public enum SectionType
    {
        // Program code
        Text = 1,
        // Read-only data
        Data = 2,
        // Static read-write variables
        BSS = 3
    }
}
