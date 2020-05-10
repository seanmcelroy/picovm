using System;
using System.Collections.Generic;
using System.Linq;

namespace agent_playground
{

    public class Agent
    {
        public const int E_INVALID = -1;

        #region Registers
        // General registers
        public const byte R_EAX = 0;
        public const byte R_EBX = 1;
        public const byte R_ECX = 2;
        public const byte R_EDX = 3;
        // Segment registers
        public const byte R_CS = 4;
        public const byte R_DS = 5;
        public const byte R_ES = 6;
        public const byte R_FS = 7;
        public const byte R_GS = 8;
        public const byte R_SS = 9;
        // Index and pointers
        public const byte R_ESI = 10;
        public const byte R_EDI = 11;
        public const byte R_EBP = 12;
        public const byte R_EIP = 13;
        public const byte R_ESP = 14;
        // Indicator
        public const byte R_EFLAGS = 15;
        #endregion

        private ulong[] registers = new ulong[16];

        private bool[] flags = new bool[2];

        private readonly byte[] memory = new byte[65535];

        private uint instructionPointer = 0;
        private uint stackPointer = 65535;

        public uint StackPointer => this.stackPointer;

        public Agent(IEnumerable<byte> program)
        {
            var programArray = program.ToArray();
            Array.Copy(programArray, memory, programArray.Length);
        }
        public Agent(byte[] program)
        {
            Array.Copy(program, memory, program.Length);
        }

        public uint ReadExtendedRegister(Register reference)
        {
            // http://www.cs.virginia.edu/~evans/cs216/guides/x86.html

            // https://stackoverflow.com/questions/1209439/what-is-the-best-way-to-combine-two-uints-into-a-ulong-in-c-sharp
            uint ret;
            switch (reference)
            {
                case Register.EAX:
                    {
                        var u64 = registers[R_EAX];
                        ret = (uint)(u64 & uint.MaxValue);
                    }
                    break;
                case Register.EBX:
                    {
                        var u64 = registers[R_EBX];
                        ret = (uint)(u64 & uint.MaxValue);
                    }
                    break;
                case Register.ECX:
                    {
                        var u64 = registers[R_ECX];
                        ret = (uint)(u64 & uint.MaxValue);
                    }
                    break;
                case Register.EDX:
                    {
                        var u64 = registers[R_EDX];
                        ret = (uint)(u64 & uint.MaxValue);
                    }
                    break;
                default:
                    throw new InvalidOperationException($"ERROR: Unknown extended register {reference}!");
            }
            return ret;
        }

        public ushort ReadRegister(Register reference)
        {
            // 16 bits
            // We want to read the right-most 16 bits of the 64-bit value
            ushort ret;
            switch (reference)
            {
                case Register.AX:
                    ret = (ushort)(registers[R_EAX] & (ulong)ushort.MaxValue);
                    break;
                case Register.BX:
                    ret = (ushort)(registers[R_EBX] & (ulong)ushort.MaxValue);
                    break;
                case Register.CX:
                    ret = (ushort)(registers[R_ECX] & (ulong)ushort.MaxValue);
                    break;
                case Register.DX:
                    ret = (ushort)(registers[R_EDX] & (ulong)ushort.MaxValue);
                    break;
                default:
                    throw new InvalidOperationException($"ERROR: Unknown register {reference}!");
            }
            return ret;
        }

        public byte ReadHalfRegister(Register reference)
        {
            // 8 bits
            // We want to read the right-most 8 bits of the 64-bit value
            byte ret;
            switch (reference)
            {
                case Register.AH:
                    ret = (byte)((registers[R_EAX] & (ulong)0xFF00) >> 8);
                    break;
                case Register.AL:
                    ret = (byte)(registers[R_EAX] & (ulong)0x00FF);
                    break;
                case Register.BH:
                    ret = (byte)((registers[R_EBX] & (ulong)0xFF00) >> 8);
                    break;
                case Register.BL:
                    ret = (byte)(registers[R_EBX] & (ulong)0x00FF);
                    break;
                case Register.CH:
                    ret = (byte)((registers[R_ECX] & (ulong)0xFF00) >> 8);
                    break;
                case Register.CL:
                    ret = (byte)(registers[R_ECX] & (ulong)0x00FF);
                    break;
                case Register.DH:
                    ret = (byte)((registers[R_EDX] & (ulong)0xFF00) >> 8);
                    break;
                case Register.DL:
                    ret = (byte)(registers[R_EDX] & (ulong)0x00FF);
                    break;
                default:
                    throw new InvalidOperationException($"ERROR: Unknown register {reference}!");
            }
            return ret;
        }

        public void WriteExtendedRegister(Register reference, uint value)
        {
            switch (reference)
            {
                case Register.EAX:
                    {
                        const uint hi = 0;
                        var lo = value;
                        registers[R_EAX] = (ulong)hi << 32 | lo;

                    }
                    break;
                case Register.EBX:
                    {
                        const uint hi = 0;
                        var lo = value;
                        registers[R_EBX] = (ulong)hi << 32 | lo;

                    }
                    break;
                case Register.ECX:
                    {
                        const uint hi = 0;
                        var lo = value;
                        registers[R_ECX] = (ulong)hi << 32 | lo;

                    }
                    break;
                case Register.EDX:
                    {
                        const uint hi = 0;
                        var lo = value;
                        registers[R_EDX] = (ulong)hi << 32 | lo;

                    }
                    break;
                default:
                    throw new InvalidOperationException($"ERROR: Unknown extended register {reference}!");
            }
        }

        public void WriteRegister(Register reference, ushort value)
        {
            // 16 bits
            // We want to overwrite the right-most 8 bits of the 64-bit value
            // reg_data = (reg_data & (~bit_mask)) | (new_value << 5)
            // https://stackoverflow.com/questions/5925755/how-to-replace-bits-in-a-bitfield-without-affecting-other-bits-using-c

            switch (reference)
            {
                case Register.AX:
                    registers[R_EAX] = registers[R_EAX] & ~((ulong)ushort.MaxValue) | (ulong)value;
                    break;
                case Register.BX:
                    registers[R_EBX] = registers[R_EBX] & ~((ulong)ushort.MaxValue) | (ulong)value;
                    break;
                case Register.CX:
                    registers[R_ECX] = registers[R_ECX] & ~((ulong)ushort.MaxValue) | (ulong)value;
                    break;
                case Register.DX:
                    registers[R_EDX] = registers[R_EDX] & ~((ulong)ushort.MaxValue) | (ulong)value;
                    break;
                default:
                    throw new InvalidOperationException($"ERROR: Unknown register {reference}!");
            }
        }

        public void WriteHalfRegister(Register reference, byte value)
        {
            // 8 bits / 1 byte
            switch (reference)
            {
                case Register.AH:
                    registers[R_EAX] = registers[R_EAX] & ~((ulong)0xFF00) | ((ulong)value << 8);
                    break;
                case Register.AL:
                    registers[R_EAX] = registers[R_EAX] & ~((ulong)0x00FF) | (ulong)value;
                    break;
                case Register.BH:
                    registers[R_EBX] = registers[R_EBX] & ~((ulong)0xFF00) | ((ulong)value << 8);
                    break;
                case Register.BL:
                    registers[R_EBX] = registers[R_EBX] & ~((ulong)0x00FF) | (ulong)value;
                    break;
                case Register.CH:
                    registers[R_ECX] = registers[R_ECX] & ~((ulong)0xFF00) | ((ulong)value << 8);
                    break;
                case Register.CL:
                    registers[R_ECX] = registers[R_ECX] & ~((ulong)0x00FF) | (ulong)value;
                    break;
                case Register.DH:
                    registers[R_EDX] = registers[R_EDX] & ~((ulong)0xFF00) | ((ulong)value << 8);
                    break;
                case Register.DL:
                    registers[R_EDX] = registers[R_EDX] & ~((ulong)0x00FF) | (ulong)value;
                    break;
                default:
                    throw new InvalidOperationException($"ERROR: Unknown register {reference}!");
            }
        }

        public uint StackPeek32() => BitConverter.ToUInt32(memory, (int)stackPointer);

        public uint StackPop32()
        {
            var ret = BitConverter.ToUInt32(memory, (int)stackPointer);
            stackPointer += 4;
            return ret;
        }

        public ushort StackPop16()
        {
            var ret = BitConverter.ToUInt16(memory, (int)stackPointer);
            stackPointer += 2;
            return ret;
        }

        public byte StackPop8()
        {
            var ret = memory[(int)stackPointer];
            stackPointer++;
            return ret;
        }

        public void StackPush(uint value)
        {
            // Push is ALWAYS a 32-bit operation.  Callers convert.
            Array.Copy(BitConverter.GetBytes(value), 0, memory, stackPointer - 4, 4);
            stackPointer -= 4;
        }

        public void Dump()
        {
            Console.WriteLine();
            Console.Write($"EAX: 0x{ReadExtendedRegister(Register.EAX):X4} ({ReadExtendedRegister(Register.EAX).ToString().PadLeft(2)})\t");
            Console.Write($"EBX: 0x{ReadExtendedRegister(Register.EBX):X4} ({ReadExtendedRegister(Register.EBX)})\t");
            Console.Write($"ECX: 0x{ReadExtendedRegister(Register.ECX):X4} ({ReadExtendedRegister(Register.ECX)})\t");
            Console.WriteLine($"EDX: 0x{ReadExtendedRegister(Register.EDX):X4} ({ReadExtendedRegister(Register.EDX)})");
            Console.WriteLine($"EIP: 0x{instructionPointer:X4} ({instructionPointer})\tESP: 0x{stackPointer:X4} ({stackPointer})");
            Console.WriteLine("(Stack)");
            var i = (uint)memory.Length;
            var qword = new byte[8];
            do
            {
                Array.Copy(memory, i - 8, qword, 0, 8);
                var output = qword.Select(b => $"{b:X2}").Aggregate((c, n) => $"{c} {n}");
                Console.WriteLine($"{i}\t: {output}");
                i -= 8;
            } while (i > stackPointer);
            Console.WriteLine("...");
            i = instructionPointer + (8 - instructionPointer % 8);
            do
            {
                Array.Copy(memory, i - 8, qword, 0, 8);
                var output = qword.Select(b => $"{b:X2}").Aggregate((c, n) => $"{c} {n}");
                Console.WriteLine($"{i}\t: {output}");
                i -= 8;
            } while (i > 0);
        }

        // Processes the interrupt, returns whether the process should be terminated.
        public bool KernelInterrupt()
        {
            // Linux-y interrupt syscalls
            var syscall = ReadExtendedRegister(Register.EAX);
            switch (syscall)
            {
                case 1: // sys_exit
                    return true;
                case 4: // sys_write
                    var fd = ReadExtendedRegister(Register.EBX);
                    var outputIndex = ReadExtendedRegister(Register.ECX);
                    var outputLength = ReadExtendedRegister(Register.EDX);

                    var outputBytes = new byte[outputLength];
                    Array.Copy(memory, outputIndex, outputBytes, 0, outputLength);
                    var outputString = System.Text.Encoding.ASCII.GetString(outputBytes);

                    switch (fd)
                    {
                        case 1: // STDOUT
                            Console.Out.Write(outputString);
                            return false;
                        case 2: // STDERR
                            Console.Error.Write(outputString);
                            return false;
                        default:
                            Dump();
                            throw new InvalidOperationException($"Unknown file descriptor for sys_write: {fd}");
                    }
                default:
                    throw new InvalidOperationException($"Unknown syscall number during kernel interrupt: {syscall}");
            }
        }

        public int? Tick()
        {
            var instruction = (Bytecode)memory[instructionPointer];
            instructionPointer++;

            int? ret = null;
            switch (instruction)
            {
                case Bytecode.END:
                    ret = 0;
                    break;
                case Bytecode.ADD_REG_CON:
                    {
                        var operand1 = (Register)memory[instructionPointer];
                        instructionPointer++;

                        if (operand1 == Register.EAX || operand1 == Register.EBX || operand1 == Register.ECX || operand1 == Register.EDX)
                        {
                            var operand1value = ReadExtendedRegister(operand1);
                            instructionPointer++;
                            var operand2value = BitConverter.ToUInt32(memory, (int)instructionPointer);
                            instructionPointer += 4;
                            WriteExtendedRegister(operand1, operand1value + operand2value);
                        }
                        else if (operand1 == Register.AX || operand1 == Register.BX || operand1 == Register.CX || operand1 == Register.DX)
                        {
                            var operand1value = ReadRegister(operand1);
                            instructionPointer++;
                            var operand2value = BitConverter.ToUInt16(memory, (int)instructionPointer);
                            instructionPointer += 2;
                            WriteRegister(operand1, ((ushort)(operand1value + operand2value)));
                        }
                        else if (operand1 == Register.AH || operand1 == Register.AL
                            || operand1 == Register.BH || operand1 == Register.BL
                            || operand1 == Register.CH || operand1 == Register.CL
                            || operand1 == Register.DH || operand1 == Register.DL)
                        {
                            var operand1value = ReadHalfRegister(operand1);
                            instructionPointer++;
                            var operand2value = memory[(int)instructionPointer];
                            instructionPointer++;
                            WriteHalfRegister(operand1, ((byte)(operand1value + operand2value)));
                        }
                        else
                            throw new InvalidOperationException($"ERROR: ADD cannot handle the type of register targeted: {operand1}");

                        break;
                    }
                case Bytecode.ADD_MEM_CON:
                    {
                        var operand1 = (Register)memory[instructionPointer];
                        instructionPointer++;

                        if (operand1 == Register.EAX || operand1 == Register.EBX || operand1 == Register.ECX || operand1 == Register.EDX)
                        {
                            var loc = ReadExtendedRegister(operand1);
                            var operand1value = BitConverter.ToUInt32(memory, (int)loc);
                            var operand2value = BitConverter.ToUInt32(memory, (int)instructionPointer);
                            instructionPointer += 4;
                            Array.Copy(BitConverter.GetBytes(operand1value + operand2value), 0, memory, loc, 4);
                        }
                        else if (operand1 == Register.AX || operand1 == Register.BX || operand1 == Register.CX || operand1 == Register.DX)
                        {
                            var loc = ReadRegister(operand1);
                            var operand1value = BitConverter.ToUInt16(memory, (int)loc);
                            var operand2value = BitConverter.ToUInt16(memory, (int)instructionPointer);
                            instructionPointer += 2;
                            Array.Copy(BitConverter.GetBytes((ushort)(operand1value + operand2value)), 0, memory, loc, 2);
                        }
                        else if (operand1 == Register.AH || operand1 == Register.AL
                            || operand1 == Register.BH || operand1 == Register.BL
                            || operand1 == Register.CH || operand1 == Register.CL
                            || operand1 == Register.DH || operand1 == Register.DL)
                        {
                            var loc = ReadHalfRegister(operand1);
                            var operand1value = memory[(int)loc];
                            var operand2value = memory[(int)instructionPointer];
                            instructionPointer += 1;
                            memory[(int)loc] = (byte)(operand1value + operand2value);
                        }
                        else
                            throw new InvalidOperationException($"ERROR: ADD cannot handle the type of register targeted: {operand1}");

                        break;
                    }
                case Bytecode.AND_REG_CON:
                    {
                        var operand1 = (Register)memory[instructionPointer];
                        instructionPointer++;

                        if (operand1 == Register.EAX || operand1 == Register.EBX || operand1 == Register.ECX || operand1 == Register.EDX)
                        {
                            var operand1value = ReadExtendedRegister(operand1);
                            var operand2value = BitConverter.ToUInt32(memory, (int)instructionPointer);
                            instructionPointer += 4;
                            var val = operand1value & operand2value;
                            flags[(int)Flag.ZERO_FLAG] = val == 0;
                            WriteExtendedRegister(operand1, val);
                        }
                        else if (operand1 == Register.AX || operand1 == Register.BX || operand1 == Register.CX || operand1 == Register.DX)
                        {
                            var operand1value = ReadRegister(operand1);
                            var operand2value = BitConverter.ToUInt16(memory, (int)instructionPointer);
                            instructionPointer += 2;
                            var val = (ushort)(operand1value & operand2value);
                            flags[(int)Flag.ZERO_FLAG] = val == 0;
                            WriteRegister(operand1, val);
                        }
                        else if (operand1 == Register.AH || operand1 == Register.AL
                            || operand1 == Register.BH || operand1 == Register.BL
                            || operand1 == Register.CH || operand1 == Register.CL
                            || operand1 == Register.DH || operand1 == Register.DL)
                        {
                            var operand1value = ReadHalfRegister(operand1);
                            var operand2value = memory[(int)instructionPointer];
                            instructionPointer++;
                            var val = (byte)(operand1value & operand2value);
                            flags[(int)Flag.ZERO_FLAG] = val == 0;
                            WriteHalfRegister(operand1, val);
                        }
                        else
                            throw new InvalidOperationException($"ERROR: AND cannot handle the type of register targeted: {operand1}");

                        break;
                    }
                case Bytecode.INT:
                    {
                        // Interrupt number
                        var interruptVector = memory[(int)instructionPointer];
                        instructionPointer++;

                        switch (interruptVector)
                        {
                            // Linux kernel interrupt
                            case 0x80:
                                if (KernelInterrupt())
                                    return 0;
                                break;
                        }

                        break;
                    }
                case Bytecode.MOV_REG_REG:
                    {
                        var dst = (Register)memory[instructionPointer];
                        instructionPointer++;
                        var src = (Register)memory[instructionPointer];
                        instructionPointer++;

                        if (src == Register.EAX || src == Register.EBX || src == Register.ECX || src == Register.EDX)
                        {
                            var srcVal = ReadExtendedRegister(src);
                            if (dst == Register.EAX || dst == Register.EBX || dst == Register.ECX || dst == Register.EDX)
                                WriteExtendedRegister(dst, srcVal);
                            else if (dst == Register.AX || dst == Register.BX || dst == Register.CX || dst == Register.DX)
                                throw new InvalidOperationException("ERROR: MOV dst is a word but source is a dword");
                            else if (dst == Register.AH || dst == Register.AL
                                || dst == Register.BH || dst == Register.BL
                                || dst == Register.CH || dst == Register.CL
                                || dst == Register.DH || dst == Register.DL)
                                throw new InvalidOperationException("ERROR: MOV dst is a byte but source is a dword");
                            else
                                throw new InvalidOperationException("ERROR: Unrecognized register for MOV dst");
                        }
                        else if (src == Register.AX || src == Register.BX || src == Register.CX || src == Register.DX)
                        {
                            var srcVal = ReadRegister(src);
                            if (dst == Register.EAX || dst == Register.EBX || dst == Register.ECX || dst == Register.EDX)
                                WriteExtendedRegister(dst, srcVal);
                            else if (dst == Register.AX || dst == Register.BX || dst == Register.CX || dst == Register.DX)
                                WriteRegister(dst, srcVal);
                            else if (dst == Register.AH || dst == Register.AL
                                || dst == Register.BH || dst == Register.BL
                                || dst == Register.CH || dst == Register.CL
                                || dst == Register.DH || dst == Register.DL)
                                throw new InvalidOperationException("ERROR: MOV dst is a byte but source is a word");
                            else
                                throw new InvalidOperationException("ERROR: Unrecognized register for MOV dst");
                        }
                        else if (src == Register.AH || src == Register.AL
                            || src == Register.BH || src == Register.BL
                            || src == Register.CH || src == Register.CL
                            || src == Register.DH || src == Register.DL)
                        {
                            var srcVal = ReadHalfRegister(src);
                            if (dst == Register.EAX || dst == Register.EBX || dst == Register.ECX || dst == Register.EDX)
                                WriteExtendedRegister(dst, srcVal);
                            else if (dst == Register.AX || dst == Register.BX || dst == Register.CX || dst == Register.DX)
                                WriteRegister(dst, srcVal);
                            else if (dst == Register.AH || dst == Register.AL
                                || dst == Register.BH || dst == Register.BL
                                || dst == Register.CH || dst == Register.CL
                                || dst == Register.DH || dst == Register.DL)
                                WriteHalfRegister(dst, srcVal);
                            else
                                throw new InvalidOperationException("ERROR: Unrecognized register for MOV dst");
                        }
                        else
                        {
                            Dump();
                            throw new InvalidOperationException("ERROR: Unrecognized register for MOV src");
                        }

                        break;
                    }
                case Bytecode.MOV_REG_MEM:
                    {
                        var dst = (Register)memory[instructionPointer];
                        instructionPointer++;

                        if (dst == Register.EAX || dst == Register.EBX || dst == Register.ECX || dst == Register.EDX)
                        {
                            var loc = BitConverter.ToUInt32(memory, (int)instructionPointer);
                            instructionPointer += 4;
                            WriteExtendedRegister(dst, loc);
                        }
                        else if (dst == Register.AX || dst == Register.BX || dst == Register.CX || dst == Register.DX)
                        {
                            var loc = BitConverter.ToUInt16(memory, (int)instructionPointer);
                            instructionPointer += 2;
                            WriteRegister(dst, loc);
                        }
                        else if (dst == Register.AH || dst == Register.AL
                            || dst == Register.BH || dst == Register.BL
                            || dst == Register.CH || dst == Register.CL
                            || dst == Register.DH || dst == Register.DL)
                        {
                            var loc = memory[instructionPointer];
                            instructionPointer++;
                            WriteHalfRegister(dst, loc);
                        }
                        else
                        {
                            Dump();
                            throw new InvalidOperationException($"ERROR: Unrecognized register for MOV dst: {dst}");
                        }

                        break;
                    }
                case Bytecode.MOV_REG_CON:
                    {
                        var dst = (Register)memory[instructionPointer];
                        instructionPointer++;

                        if (dst == Register.EAX || dst == Register.EBX || dst == Register.ECX || dst == Register.EDX)
                        {
                            var val = BitConverter.ToUInt32(memory, (int)instructionPointer);
                            instructionPointer += 4;
                            WriteExtendedRegister(dst, val);
                        }
                        else if (dst == Register.AX || dst == Register.BX || dst == Register.CX || dst == Register.DX)
                        {
                            var val = BitConverter.ToUInt16(memory, (int)instructionPointer);
                            instructionPointer += 2;
                            WriteRegister(dst, val);
                        }
                        else if (dst == Register.AH || dst == Register.AL
                            || dst == Register.BH || dst == Register.BL
                            || dst == Register.CH || dst == Register.CL
                            || dst == Register.DH || dst == Register.DL)
                        {
                            var val = memory[(int)instructionPointer];
                            instructionPointer++;
                            WriteHalfRegister(dst, val);
                        }
                        else
                        {
                            Dump();
                            throw new InvalidOperationException("ERROR: Unrecognized register for MOV dst");
                        }
                        break;
                    }
                case Bytecode.POP_REG:
                    {
                        var operand = (Register)memory[instructionPointer];
                        instructionPointer++;

                        if (operand == Register.EAX || operand == Register.EBX || operand == Register.ECX || operand == Register.EDX)
                            WriteExtendedRegister(operand, StackPop32());
                        else if (operand == Register.AX || operand == Register.BX || operand == Register.CX || operand == Register.DX)
                            WriteRegister(operand, StackPop16());
                        else if (operand == Register.AH || operand == Register.AL
                            || operand == Register.BH || operand == Register.BL
                            || operand == Register.CH || operand == Register.CL
                            || operand == Register.DH || operand == Register.DL)
                            WriteHalfRegister(operand, StackPop8());
                        else
                        {
                            Dump();
                            throw new InvalidOperationException("ERROR: Unrecognized register for POP");
                        }

                        break;
                    }
                case Bytecode.POP_MEM:
                    {
                        var operand = (Register)memory[instructionPointer];
                        instructionPointer++;

                        if (operand == Register.EAX || operand == Register.EBX || operand == Register.ECX || operand == Register.EDX)
                        {
                            var loc = ReadExtendedRegister(operand);
                            Array.Copy(BitConverter.GetBytes(StackPop32()), 0, memory, loc, 4);
                        }
                        else if (operand == Register.AX || operand == Register.BX || operand == Register.CX || operand == Register.DX)
                        {
                            var loc = ReadRegister(operand);
                            Array.Copy(BitConverter.GetBytes(StackPop16()), 0, memory, loc, 2);
                        }
                        else if (operand == Register.AH || operand == Register.AL
                            || operand == Register.BH || operand == Register.BL
                            || operand == Register.CH || operand == Register.CL
                            || operand == Register.DH || operand == Register.DL)
                        {
                            var loc = ReadHalfRegister(operand);
                            memory[(int)loc] = StackPop8();
                        }
                        else
                        {
                            Dump();
                            throw new InvalidOperationException("ERROR: Unrecognized register for POP");
                        }

                        break;
                    }
                case Bytecode.PUSH_REG:
                    {
                        var operand = (Register)memory[instructionPointer];
                        instructionPointer++;
                        if (operand == Register.EAX || operand == Register.EBX || operand == Register.ECX || operand == Register.EDX)
                            StackPush(ReadExtendedRegister(operand));
                        else if (operand == Register.AX || operand == Register.BX || operand == Register.CX || operand == Register.DX)
                            StackPush(ReadRegister(operand));
                        else if (operand == Register.AH || operand == Register.AL
                            || operand == Register.BH || operand == Register.BL
                            || operand == Register.CH || operand == Register.CL
                            || operand == Register.DH || operand == Register.DL)
                            StackPush(ReadHalfRegister(operand));
                        else
                        {
                            Dump();
                            throw new InvalidOperationException("ERROR: Unrecognized register for PUSH");
                        }

                        break;
                    }
                case Bytecode.PUSH_MEM:
                    {
                        var operand = (Register)memory[instructionPointer];
                        instructionPointer++;

                        if (operand == Register.EAX || operand == Register.EBX || operand == Register.ECX || operand == Register.EDX)
                        {
                            var loc = ReadExtendedRegister(operand);
                            var val = BitConverter.ToUInt32(memory, (int)loc);
                            StackPush(val);
                        }
                        else if (operand == Register.AX || operand == Register.BX || operand == Register.CX || operand == Register.DX)
                        {
                            var loc = ReadRegister(operand);
                            var val = BitConverter.ToUInt16(memory, (int)loc);
                            StackPush(val);
                        }
                        else if (operand == Register.AH || operand == Register.AL
                            || operand == Register.BH || operand == Register.BL
                            || operand == Register.CH || operand == Register.CL
                            || operand == Register.DH || operand == Register.DL)
                        {
                            var loc = ReadHalfRegister(operand);
                            var val = memory[(int)loc];
                            StackPush(val);
                        }
                        else
                        {
                            Dump();
                            throw new InvalidOperationException("ERROR: Unrecognized register for PUSH");
                        }

                        break;
                    }
                case Bytecode.PUSH_CON:
                    {
                        // Push is ALWAYS a 32-bit operation
                        var operand = (Register)memory[instructionPointer];
                        var val = BitConverter.ToUInt32(memory, (int)instructionPointer);
                        instructionPointer += 4;
                        StackPush(val);

                        break;
                    }
                case Bytecode.JMP:
                    {
                        var loc = BitConverter.ToUInt32(memory, (int)instructionPointer);
                        instructionPointer = loc;
                        break;
                    }
                case Bytecode.JZ:
                    {
                        var loc = BitConverter.ToUInt32(memory, (int)instructionPointer);
                        if (flags[(int)Flag.ZERO_FLAG])
                            instructionPointer = loc;
                        else
                            instructionPointer += 4;
                        break;
                    }
                default:
                    Console.Error.WriteLine($"ERROR: Unknown bytecode {instruction} EIP={instructionPointer - 1}!");
                    Dump();
                    ret = E_INVALID;
                    break;
            }

            return ret;
        }
    }
}
