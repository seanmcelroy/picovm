using System;
using System.IO;

namespace picovm.VM
{
    public class Linux32Kernel : IKernel
    {
        // Reference: https://www-numi.fnal.gov/offline_software/srt_public_context/WebDocs/Errors/unix_system_errors.html
        public enum Errors : int
        {
            EBADF = 9,
            EINVAL = 22
        }

        public enum FileDescriptors : uint
        {
            STDIN = 0,
            STDOUT = 1,
            STDERR = 2,
        }

        public virtual bool HandleInterrupt(ref ulong[] registers, ref byte[] memory)
        {
            // Linux-y interrupt syscalls
            // See https://syscalls.kernelgrok.com/
            var syscall = Agent.ReadExtendedRegister(registers, Register.EAX);
            return syscall switch
            {
                // sys_exit
                1 => true,
                // sys_read
                3 => sys_read(ref registers, ref memory),
                // sys_write
                4 => sys_write(ref registers, ref memory),
                _ => throw new InvalidOperationException($"Unknown syscall number during kernel interrupt: {syscall}"),
            };
        }

        private static bool sys_read(ref ulong[] registers, ref byte[] memory)
        {
            var fd = Agent.ReadExtendedRegister(registers, Register.EBX);
            var inputIndex = Agent.ReadExtendedRegister(registers, Register.ECX);
            var inputLength = Agent.ReadExtendedRegister(registers, Register.EDX);

            switch (fd)
            {
                case (uint)FileDescriptors.STDIN: // STDIN
                    {
                        // Read directly into memory without array allocations or streams.
                        using var stdin = Console.OpenStandardInput();
                        var target = memory.AsSpan((int)inputIndex, (int)inputLength);
                        int totalRead = 0;
                        while (totalRead < target.Length)
                        {
                            int read = stdin.Read(target[totalRead..]);
                            if (read <= 0)
                                break;
                            totalRead += read;
                            if (target[totalRead - 1] == 0x0a) // If ends with a newline, we can stop now.
                                break;
                        }
                        return false;
                    }
                default:
                    // Error, no such file descriptor found
                    Agent.WriteExtendedRegister(registers, Register.EAX, -1);
                    // TODO: return EBADFD errno?  Where does errno go?
                    throw new InvalidOperationException($"Unknown file descriptor for sys_read: {fd}");
            }

            throw new NotImplementedException();
        }

        private static bool sys_write(ref ulong[] registers, ref byte[] memory)
        {
            var fd = Agent.ReadExtendedRegister(registers, Register.EBX);
            var outputIndex = Agent.ReadExtendedRegister(registers, Register.ECX);
            if (outputIndex > memory.Length)
                throw new InvalidOperationException($"Invalid ECX register value for sys_write: {outputIndex}");
            var outputLength = Agent.ReadExtendedRegister(registers, Register.EDX);

            var outputString = System.Text.Encoding.ASCII.GetString(
                memory.AsSpan((int)outputIndex, (int)outputLength));

            // On success, the number of bytes written is returned.
            // On error, -1 is returned, and errno is set to indicate the cause of the error.

            var ret = false;
            switch (fd)
            {
                case (uint)FileDescriptors.STDOUT:
                    Console.Out.Write(outputString);
                    Agent.WriteExtendedRegister(registers, Register.EAX, outputLength);
                    return ret;
                case (uint)FileDescriptors.STDERR:
                    Console.Error.Write(outputString);
                    Agent.WriteExtendedRegister(registers, Register.EAX, outputLength);
                    return ret;
                default:
                    // Error, no such file descriptor found
                    Agent.WriteExtendedRegister(registers, Register.EAX, -1);
                    // TODO: return EBADFD errno?  Where does errno go?
                    throw new InvalidOperationException($"Unknown file descriptor for sys_write: {fd}");
            }
        }

    }
}