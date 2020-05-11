namespace picovm.VM
{
    public interface IKernel
    {
        bool HandleInterrupt(ref ulong[] registers, ref byte[] memory);
    }
}