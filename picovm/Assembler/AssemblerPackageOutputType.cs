namespace picovm.Assembler
{
    // NOTE: This is a stub; no actual system-executable output is created at this time, it just runs in the VM in the process
    public enum AssemblerPackageOutputType
    {
        Unknown = 0,
        AOut32 = 1,
        Elf32 = 0x0e32,
        Elf64 = 0x0e64,
        PE = 0x5045
    }
}
