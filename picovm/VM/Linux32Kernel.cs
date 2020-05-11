using System;
using System.IO;

namespace picovm.VM
{
    public sealed class Linux32Kernel : IKernel
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

        public bool HandleInterrupt(ref ulong[] registers, ref byte[] memory)
        {
            // Linux-y interrupt syscalls
            // See https://syscalls.kernelgrok.com/
            var syscall = Agent.ReadExtendedRegister(registers, Register.EAX);
            switch (syscall)
            {
                case 1: // sys_exit
                    return true;
                case 3: // sys_read
                    return sys_read(ref registers, ref memory);
                case 4: // sys_write
                    return sys_write(ref registers, ref memory);
                default:
                    throw new InvalidOperationException($"Unknown syscall number during kernel interrupt: {syscall}");
            }
        }

        private static bool sys_read(ref ulong[] registers, ref byte[] memory)
        {
            var fd = Agent.ReadExtendedRegister(registers, Register.EBX);
            var inputIndex = Agent.ReadExtendedRegister(registers, Register.ECX);
            var inputLength = Agent.ReadExtendedRegister(registers, Register.EDX);

            switch (fd)
            {
                case (uint)FileDescriptors.STDIN: // STDIN
                    var inputBuffer = new byte[inputLength];
                    using (var stdin = System.Console.OpenStandardInput())
                    {
                        using (var ms = new MemoryStream(inputBuffer))
                        using (var bw = new BinaryWriter(ms))
                        {
                            byte[] stdinBuffer = new byte[2048];
                            int totalRead = 0;
                            int read;
                            while (totalRead < inputLength && (read = stdin.Read(stdinBuffer, 0, stdinBuffer.Length)) > 0)
                            {
                                var bytesToShare = Math.Min((int)inputLength - totalRead, read);
                                totalRead += read;
                                if (bytesToShare <= 0)
                                    break;
                                bw.Write(stdinBuffer, 0, bytesToShare);
                                if (stdinBuffer[bytesToShare - 1] == 0x0a) // If ends with a newline, we can stop now.
                                    break;
                            }
                            bw.Flush();
                        }
                    }

                    // We received the input; now provide it back.
                    Array.Copy(inputBuffer, 0, memory, inputIndex, inputLength);
                    return false;
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

            var outputBytes = new byte[outputLength];
            Array.Copy(memory, outputIndex, outputBytes, 0, outputLength);
            var outputString = System.Text.Encoding.ASCII.GetString(outputBytes);

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