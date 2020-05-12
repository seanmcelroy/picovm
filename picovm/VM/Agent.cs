using System;
using System.Collections.Generic;
using System.Linq;
using picovm.Compiler;

namespace picovm.VM
{

    public class Agent
    {
        #region Registers
        // General registers
        public const byte R_A = 0;
        public const byte R_B = 1;
        public const byte R_C = 2;
        public const byte R_D = 3;
        // Segment registers
        public const byte R_CS = 4;
        public const byte R_DS = 5;
        public const byte R_ES = 6;
        public const byte R_FS = 7;
        public const byte R_GS = 8;
        public const byte R_SS = 9;
        // Index and pointers
        public const byte R_SI = 10;
        public const byte R_DI = 11;
        public const byte R_BP = 12;
        public const byte R_IP = 13;
        public const byte R_SP = 14;
        // Indicator
        public const byte R_FLAGS = 15;

        public const byte R_8 = 16;
        public const byte R_9 = 17;
        public const byte R_10 = 18;
        public const byte R_11 = 19;
        public const byte R_12 = 20;
        public const byte R_13 = 21;
        public const byte R_14 = 22;
        public const byte R_15 = 23;

        #endregion

        private ulong[] registers = new ulong[24];

        private bool[] flags = new bool[2];

        private byte[] memory = new byte[65535];

        private uint instructionPointer = 0;

        public uint StackPointer
        {
            get => ReadExtendedRegister(Register.SP);
            set => WriteExtendedRegister(Register.SP, value);
        }

        private readonly IKernel kernel;

        public Agent(IKernel kernel, IEnumerable<byte> program) : this(kernel, program.ToArray())
        {
        }

        public Agent(IKernel kernel, byte[] program)
        {
            this.kernel = kernel;
            Array.Copy(program, memory, program.Length);
            StackPointer = (uint)(memory.Length - 1);
        }

        public static ulong ReadR64Register(ulong[] registers, Register reference)
        {
            switch (reference)
            {
                case Register.RAX:
                    return registers[R_A];
                case Register.RBX:
                    return registers[R_B];
                case Register.RCX:
                    return registers[R_C];
                case Register.RDX:
                    return registers[R_D];
                case Register.R8:
                    return registers[R_8];
                case Register.R9:
                    return registers[R_9];
                case Register.R10:
                    return registers[R_10];
                case Register.R11:
                    return registers[R_11];
                case Register.R12:
                    return registers[R_12];
                case Register.R13:
                    return registers[R_13];
                case Register.R14:
                    return registers[R_14];
                case Register.R15:
                    return registers[R_15];
                case Register.SP:
                    return registers[R_SP];
                default:
                    throw new InvalidOperationException($"ERROR: Unknown x64 register {reference}!");
            }
        }

        public ulong ReadR64Register(Register reference) => ReadR64Register(this.registers, reference);

        public static uint ReadExtendedRegister(ulong[] registers, Register reference)
        {
            // http://www.cs.virginia.edu/~evans/cs216/guides/x86.html

            // https://stackoverflow.com/questions/1209439/what-is-the-best-way-to-combine-two-uints-into-a-ulong-in-c-sharp
            uint ret;
            switch (reference)
            {
                case Register.EAX:
                    {
                        var u64 = registers[R_A];
                        ret = (uint)(u64 & uint.MaxValue);
                    }
                    break;
                case Register.EBX:
                    {
                        var u64 = registers[R_B];
                        ret = (uint)(u64 & uint.MaxValue);
                    }
                    break;
                case Register.ECX:
                    {
                        var u64 = registers[R_C];
                        ret = (uint)(u64 & uint.MaxValue);
                    }
                    break;
                case Register.EDX:
                    {
                        var u64 = registers[R_D];
                        ret = (uint)(u64 & uint.MaxValue);
                    }
                    break;
                case Register.SP:
                    {
                        var u64 = registers[R_SP];
                        ret = (uint)(u64 & uint.MaxValue);
                    }
                    break;
                default:
                    throw new InvalidOperationException($"ERROR: Unknown extended register {reference}!");
            }
            return ret;
        }

        public uint ReadExtendedRegister(Register reference) => ReadExtendedRegister(this.registers, reference);

        public ushort ReadRegister(Register reference)
        {
            // 16 bits
            // We want to read the right-most 16 bits of the 64-bit value
            ushort ret;
            switch (reference)
            {
                case Register.AX:
                    ret = (ushort)(registers[R_A] & (ulong)ushort.MaxValue);
                    break;
                case Register.BX:
                    ret = (ushort)(registers[R_B] & (ulong)ushort.MaxValue);
                    break;
                case Register.CX:
                    ret = (ushort)(registers[R_C] & (ulong)ushort.MaxValue);
                    break;
                case Register.DX:
                    ret = (ushort)(registers[R_D] & (ulong)ushort.MaxValue);
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
                    ret = (byte)((registers[R_A] & (ulong)0xFF00) >> 8);
                    break;
                case Register.AL:
                    ret = (byte)(registers[R_A] & (ulong)0x00FF);
                    break;
                case Register.BH:
                    ret = (byte)((registers[R_B] & (ulong)0xFF00) >> 8);
                    break;
                case Register.BL:
                    ret = (byte)(registers[R_B] & (ulong)0x00FF);
                    break;
                case Register.CH:
                    ret = (byte)((registers[R_C] & (ulong)0xFF00) >> 8);
                    break;
                case Register.CL:
                    ret = (byte)(registers[R_C] & (ulong)0x00FF);
                    break;
                case Register.DH:
                    ret = (byte)((registers[R_D] & (ulong)0xFF00) >> 8);
                    break;
                case Register.DL:
                    ret = (byte)(registers[R_D] & (ulong)0x00FF);
                    break;
                default:
                    throw new InvalidOperationException($"ERROR: Unknown register {reference}!");
            }
            return ret;
        }

        public static void WriteR64Register(ulong[] registers, Register reference, ulong value)
        {
            switch (reference)
            {
                case Register.RAX:
                    registers[R_A] = value;
                    break;
                case Register.RBX:
                    registers[R_B] = value;
                    break;
                case Register.RCX:
                    registers[R_C] = value;
                    break;
                case Register.RDX:
                    registers[R_D] = value;
                    break;
                case Register.R8:
                    registers[R_8] = value;
                    break;
                case Register.R9:
                    registers[R_9] = value;
                    break;
                case Register.R10:
                    registers[R_10] = value;
                    break;
                case Register.R11:
                    registers[R_11] = value;
                    break;
                case Register.R12:
                    registers[R_12] = value;
                    break;
                case Register.R13:
                    registers[R_13] = value;
                    break;
                case Register.R14:
                    registers[R_14] = value;
                    break;
                case Register.R15:
                    registers[R_15] = value;
                    break;
                default:
                    throw new InvalidOperationException($"ERROR: Unknown x64 register {reference}!");
            }
        }

        public void WriteR64Register(Register reference, ulong value) => WriteR64Register(this.registers, reference, value);

        public static void WriteExtendedRegister(ulong[] registers, Register reference, uint value)
        {
            const uint hi = 0;
            var lo = value;
            switch (reference)
            {
                case Register.EAX:
                    registers[R_A] = (ulong)hi << 32 | lo;
                    break;
                case Register.EBX:
                    registers[R_B] = (ulong)hi << 32 | lo;
                    break;
                case Register.ECX:
                    registers[R_C] = (ulong)hi << 32 | lo;
                    break;
                case Register.EDX:
                    registers[R_D] = (ulong)hi << 32 | lo;
                    break;
                case Register.SP:
                    registers[R_SP] = (ulong)hi << 32 | lo;
                    break;
                default:
                    throw new InvalidOperationException($"ERROR: Unknown extended register {reference}!");
            }
        }

        public void WriteExtendedRegister(Register reference, uint value) => WriteExtendedRegister(this.registers, reference, value);

        public static void WriteExtendedRegister(ulong[] registers, Register reference, int value)
        {
            const int hi = 0;
            var lo = value;
            switch (reference)
            {
                case Register.EAX:
                    registers[R_A] = (ulong)(hi << 32 | lo);
                    break;
                case Register.EBX:
                    registers[R_B] = (ulong)(hi << 32 | lo);
                    break;
                case Register.ECX:
                    registers[R_C] = (ulong)(hi << 32 | lo);
                    break;
                case Register.EDX:
                    registers[R_D] = (ulong)(hi << 32 | lo);
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
                    registers[R_A] = registers[R_A] & ~((ulong)ushort.MaxValue) | (ulong)value;
                    break;
                case Register.BX:
                    registers[R_B] = registers[R_B] & ~((ulong)ushort.MaxValue) | (ulong)value;
                    break;
                case Register.CX:
                    registers[R_C] = registers[R_C] & ~((ulong)ushort.MaxValue) | (ulong)value;
                    break;
                case Register.DX:
                    registers[R_D] = registers[R_D] & ~((ulong)ushort.MaxValue) | (ulong)value;
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
                    registers[R_A] = registers[R_A] & ~((ulong)0xFF00) | ((ulong)value << 8);
                    break;
                case Register.AL:
                    registers[R_A] = registers[R_A] & ~((ulong)0x00FF) | (ulong)value;
                    break;
                case Register.BH:
                    registers[R_B] = registers[R_B] & ~((ulong)0xFF00) | ((ulong)value << 8);
                    break;
                case Register.BL:
                    registers[R_B] = registers[R_B] & ~((ulong)0x00FF) | (ulong)value;
                    break;
                case Register.CH:
                    registers[R_C] = registers[R_C] & ~((ulong)0xFF00) | ((ulong)value << 8);
                    break;
                case Register.CL:
                    registers[R_C] = registers[R_C] & ~((ulong)0x00FF) | (ulong)value;
                    break;
                case Register.DH:
                    registers[R_D] = registers[R_D] & ~((ulong)0xFF00) | ((ulong)value << 8);
                    break;
                case Register.DL:
                    registers[R_D] = registers[R_D] & ~((ulong)0x00FF) | (ulong)value;
                    break;
                default:
                    throw new InvalidOperationException($"ERROR: Unknown register {reference}!");
            }
        }

        public uint StackPeek32() => BitConverter.ToUInt32(memory, (int)ReadExtendedRegister(Register.SP));

        public ulong StackPop64()
        {
            var ret = BitConverter.ToUInt64(memory, (int)ReadR64Register(Register.SP));
            StackPointer += 8;
            return ret;
        }

        public uint StackPop32()
        {
            var ret = BitConverter.ToUInt32(memory, (int)StackPointer);
            StackPointer += 4;
            return ret;
        }

        public ushort StackPop16()
        {
            var ret = BitConverter.ToUInt16(memory, (int)StackPointer);
            StackPointer += 2;
            return ret;
        }

        public byte StackPop8()
        {
            var ret = memory[(int)StackPointer];
            StackPointer++;
            return ret;
        }

        public void StackPush(ulong value)
        {
            // Push is ALWAYS a 32-bit operation.  Callers convert.
            Array.Copy(BitConverter.GetBytes(value), 0, memory, StackPointer - 8, 8);
            StackPointer -= 8;
        }

        public void StackPush(uint value)
        {
            // Push is ALWAYS a 32-bit operation.  Callers convert.
            Array.Copy(BitConverter.GetBytes(value), 0, memory, StackPointer - 4, 4);
            StackPointer -= 4;
        }

        public void Dump()
        {
            Console.WriteLine();
            Console.Write($"EAX: 0x{ReadExtendedRegister(Register.EAX):X4} ({ReadExtendedRegister(Register.EAX).ToString().PadLeft(2)})\t");
            Console.Write($"EBX: 0x{ReadExtendedRegister(Register.EBX):X4} ({ReadExtendedRegister(Register.EBX).ToString().PadLeft(2)})\t");
            Console.Write($"ECX: 0x{ReadExtendedRegister(Register.ECX):X4} ({ReadExtendedRegister(Register.ECX).ToString().PadLeft(2)})\t");
            Console.WriteLine($"EDX: 0x{ReadExtendedRegister(Register.EDX):X4} ({ReadExtendedRegister(Register.EDX).ToString().PadLeft(2)})");
            Console.WriteLine($"EIP: 0x{instructionPointer:X4} ({instructionPointer})\tESP: 0x{StackPointer:X4} ({StackPointer})");
            Console.WriteLine("(Stack)");
            var i = (uint)memory.Length;
            var qword = new byte[8];
            do
            {
                Array.Copy(memory, i - 8, qword, 0, 8);
                var output = qword.Select(b => $"{b:X2}").Aggregate((c, n) => $"{c} {n}");
                Console.WriteLine($"{i}\t: {output}");
                i -= 8;
            } while (i > StackPointer);
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

                        switch (operand1.Size())
                        {
                            case 8:
                                {
                                    var operand1value = ReadR64Register(operand1);
                                    instructionPointer++;
                                    var operand2value = BitConverter.ToUInt64(memory, (int)instructionPointer);
                                    instructionPointer += 8;
                                    WriteR64Register(operand1, operand1value + operand2value);
                                    break;
                                }
                            case 4:
                                {
                                    var operand1value = ReadExtendedRegister(operand1);
                                    instructionPointer++;
                                    var operand2value = BitConverter.ToUInt32(memory, (int)instructionPointer);
                                    instructionPointer += 4;
                                    WriteExtendedRegister(operand1, operand1value + operand2value);
                                    break;
                                }
                            case 2:
                                {
                                    var operand1value = ReadRegister(operand1);
                                    instructionPointer++;
                                    var operand2value = BitConverter.ToUInt16(memory, (int)instructionPointer);
                                    instructionPointer += 2;
                                    WriteRegister(operand1, ((ushort)(operand1value + operand2value)));
                                    break;
                                }
                            case 1:
                                {
                                    var operand1value = ReadHalfRegister(operand1);
                                    instructionPointer++;
                                    var operand2value = memory[(int)instructionPointer];
                                    instructionPointer++;
                                    WriteHalfRegister(operand1, ((byte)(operand1value + operand2value)));
                                    break;
                                }
                            default:
                                throw new InvalidOperationException($"ERROR: ADD cannot handle the type of register targeted: {operand1}");
                        }
                        break;
                    }
                case Bytecode.ADD_MEM_CON:
                    {
                        var operand1 = (Register)memory[instructionPointer];
                        instructionPointer++;

                        switch (operand1.Size())
                        {
                            case 8:
                                {
                                    var loc = ReadR64Register(operand1);
                                    var operand1value = BitConverter.ToUInt64(memory, (int)loc);
                                    var operand2value = BitConverter.ToUInt64(memory, (int)instructionPointer);
                                    instructionPointer += 4;
                                    Array.Copy(BitConverter.GetBytes(operand1value + operand2value), 0L, memory, (long)loc, 8);
                                    break;
                                }
                            case 4:
                                {
                                    var loc = ReadExtendedRegister(operand1);
                                    var operand1value = BitConverter.ToUInt32(memory, (int)loc);
                                    var operand2value = BitConverter.ToUInt32(memory, (int)instructionPointer);
                                    instructionPointer += 4;
                                    Array.Copy(BitConverter.GetBytes(operand1value + operand2value), 0, memory, loc, 4);
                                    break;
                                }
                            case 2:
                                {
                                    var loc = ReadRegister(operand1);
                                    var operand1value = BitConverter.ToUInt16(memory, (int)loc);
                                    var operand2value = BitConverter.ToUInt16(memory, (int)instructionPointer);
                                    instructionPointer += 2;
                                    Array.Copy(BitConverter.GetBytes((ushort)(operand1value + operand2value)), 0, memory, loc, 2);
                                    break;
                                }
                            case 1:
                                {
                                    var loc = ReadHalfRegister(operand1);
                                    var operand1value = memory[(int)loc];
                                    var operand2value = memory[(int)instructionPointer];
                                    instructionPointer += 1;
                                    memory[(int)loc] = (byte)(operand1value + operand2value);
                                    break;
                                }
                            default:
                                throw new InvalidOperationException($"ERROR: ADD cannot handle the type of register targeted: {operand1}");
                        }
                        break;
                    }
                case Bytecode.AND_REG_CON:
                    {
                        var operand1 = (Register)memory[instructionPointer];
                        instructionPointer++;

                        switch (operand1.Size())
                        {
                            case 8:
                                {
                                    var operand1value = ReadR64Register(operand1);
                                    var operand2value = BitConverter.ToUInt64(memory, (int)instructionPointer);
                                    instructionPointer += 8;
                                    var val = operand1value & operand2value;
                                    flags[(int)Flag.ZERO_FLAG] = val == 0;
                                    WriteR64Register(operand1, val);
                                    break;
                                }
                            case 4:
                                {
                                    var operand1value = ReadExtendedRegister(operand1);
                                    var operand2value = BitConverter.ToUInt32(memory, (int)instructionPointer);
                                    instructionPointer += 4;
                                    var val = operand1value & operand2value;
                                    flags[(int)Flag.ZERO_FLAG] = val == 0;
                                    WriteExtendedRegister(operand1, val);
                                    break;
                                }
                            case 2:
                                {
                                    var operand1value = ReadRegister(operand1);
                                    var operand2value = BitConverter.ToUInt16(memory, (int)instructionPointer);
                                    instructionPointer += 2;
                                    var val = (ushort)(operand1value & operand2value);
                                    flags[(int)Flag.ZERO_FLAG] = val == 0;
                                    WriteRegister(operand1, val);
                                    break;
                                }
                            case 1:
                                {
                                    var operand1value = ReadHalfRegister(operand1);
                                    var operand2value = memory[(int)instructionPointer];
                                    instructionPointer++;
                                    var val = (byte)(operand1value & operand2value);
                                    flags[(int)Flag.ZERO_FLAG] = val == 0;
                                    WriteHalfRegister(operand1, val);
                                    break;
                                }
                            default:
                                throw new InvalidOperationException($"ERROR: AND cannot handle the type of register targeted: {operand1}");
                        }
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
                                if (kernel.HandleInterrupt(ref registers, ref memory))
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

                        switch (src.Size())
                        {
                            case 8:
                                {
                                    var srcVal = ReadR64Register(src);
                                    switch (dst.Size())
                                    {
                                        case 8:
                                            WriteR64Register(dst, srcVal);
                                            break;
                                        case 4:
                                            throw new InvalidOperationException("ERROR: MOV dst is a dword but source is a qword");
                                        case 2:
                                            throw new InvalidOperationException("ERROR: MOV dst is a word but source is a qword");
                                        case 1:
                                            throw new InvalidOperationException("ERROR: MOV dst is a byte but source is a qword");
                                        default:
                                            throw new InvalidOperationException($"ERROR: Unrecognized register for MOV dst: {dst}");
                                    }
                                    break;
                                }
                            case 4:
                                {
                                    var srcVal = ReadExtendedRegister(src);
                                    switch (dst.Size())
                                    {
                                        case 8:
                                            WriteR64Register(dst, srcVal);
                                            break;
                                        case 4:
                                            WriteExtendedRegister(dst, srcVal);
                                            break;
                                        case 2:
                                            throw new InvalidOperationException("ERROR: MOV dst is a word but source is a dword");
                                        case 1:
                                            throw new InvalidOperationException("ERROR: MOV dst is a byte but source is a dword");
                                        default:
                                            throw new InvalidOperationException($"ERROR: Unrecognized register for MOV dst: {dst}");
                                    }
                                    break;
                                }
                            case 2:
                                {
                                    var srcVal = ReadRegister(src);
                                    switch (dst.Size())
                                    {
                                        case 8:
                                            WriteR64Register(dst, srcVal);
                                            break;
                                        case 4:
                                            WriteExtendedRegister(dst, srcVal);
                                            break;
                                        case 2:
                                            WriteRegister(dst, srcVal);
                                            break;
                                        case 1:
                                            throw new InvalidOperationException("ERROR: MOV dst is a byte but source is a word");
                                        default:
                                            throw new InvalidOperationException($"ERROR: Unrecognized register for MOV dst: {dst}");
                                    }
                                    break;
                                }
                            case 1:
                                {
                                    var srcVal = ReadHalfRegister(src);
                                    switch (dst.Size())
                                    {
                                        case 8:
                                            WriteR64Register(dst, srcVal);
                                            break;
                                        case 4:
                                            WriteExtendedRegister(dst, srcVal);
                                            break;
                                        case 2:
                                            WriteRegister(dst, srcVal);
                                            break;
                                        case 1:
                                            WriteHalfRegister(dst, srcVal);
                                            break;
                                        default:
                                            throw new InvalidOperationException($"ERROR: Unrecognized register for MOV dst: {dst}");
                                    }
                                    break;
                                }
                            default:
                                Dump();
                                throw new InvalidOperationException("ERROR: Unrecognized register for MOV src");
                        }
                        break;
                    }
                case Bytecode.MOV_REG_MEM:
                    {
                        var dst = (Register)memory[instructionPointer];
                        instructionPointer++;
                        switch (dst.Size())
                        {
                            case 8:
                                {
                                    var loc = BitConverter.ToUInt64(memory, (int)instructionPointer);
                                    instructionPointer += 8;
                                    WriteR64Register(dst, loc);
                                    break;
                                }
                            case 4:
                                {
                                    var loc = BitConverter.ToUInt32(memory, (int)instructionPointer);
                                    instructionPointer += 4;
                                    WriteExtendedRegister(dst, loc);
                                    break;
                                }
                            case 2:
                                {
                                    var loc = BitConverter.ToUInt16(memory, (int)instructionPointer);
                                    instructionPointer += 2;
                                    WriteRegister(dst, loc);
                                    break;
                                }
                            case 1:
                                {
                                    var loc = memory[instructionPointer];
                                    instructionPointer++;
                                    WriteHalfRegister(dst, loc);
                                    break;
                                }
                            default:
                                Dump();
                                throw new InvalidOperationException($"ERROR: Unrecognized register for MOV dst: {dst}");
                        }
                        break;
                    }
                case Bytecode.MOV_REG_CON:
                    {
                        var dst = (Register)memory[instructionPointer];
                        instructionPointer++;

                        switch (dst.Size())
                        {
                            case 8:
                                {
                                    var val = BitConverter.ToUInt64(memory, (int)instructionPointer);
                                    instructionPointer += 8;
                                    WriteR64Register(dst, val);
                                    break;
                                }
                            case 4:
                                {
                                    var val = BitConverter.ToUInt32(memory, (int)instructionPointer);
                                    instructionPointer += 4;
                                    WriteExtendedRegister(dst, val);
                                    break;
                                }
                            case 2:
                                {
                                    var val = BitConverter.ToUInt16(memory, (int)instructionPointer);
                                    instructionPointer += 2;
                                    WriteRegister(dst, val);
                                    break;
                                }
                            case 1:
                                {
                                    var val = memory[(int)instructionPointer];
                                    instructionPointer++;
                                    WriteHalfRegister(dst, val);
                                    break;
                                }
                            default:
                                Dump();
                                throw new InvalidOperationException($"ERROR: Unrecognized register for MOV dst: {dst}");
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

                        switch (operand.Size())
                        {
                            case 8:
                                {
                                    var loc = ReadR64Register(operand);
                                    Array.Copy(BitConverter.GetBytes(StackPop64()), 0L, memory, (long)loc, 8);
                                    break;
                                }
                            case 4:
                                {
                                    var loc = ReadExtendedRegister(operand);
                                    Array.Copy(BitConverter.GetBytes(StackPop32()), 0, memory, loc, 4);
                                    break;
                                }
                            case 2:
                                {
                                    var loc = ReadRegister(operand);
                                    Array.Copy(BitConverter.GetBytes(StackPop16()), 0, memory, loc, 2);
                                    break;
                                }
                            case 1:
                                {
                                    var loc = ReadHalfRegister(operand);
                                    memory[(int)loc] = StackPop8();
                                    break;
                                }
                            default:
                                Dump();
                                throw new InvalidOperationException("ERROR: Unrecognized register for POP");
                        }
                        break;
                    }
                case Bytecode.PUSH_REG:
                    {
                        var operand = (Register)memory[instructionPointer];
                        instructionPointer++;
                        switch (operand.Size())
                        {
                            case 8:
                                StackPush(ReadR64Register(operand));
                                break;
                            case 4:
                                StackPush(ReadExtendedRegister(operand));
                                break;
                            case 2:
                                StackPush(ReadRegister(operand));
                                break;
                            case 1:
                                StackPush(ReadHalfRegister(operand));
                                break;
                            default:
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
                case Bytecode.XOR_REG_REG:
                    {
                        var dst = (Register)memory[instructionPointer];
                        instructionPointer++;
                        var src = (Register)memory[instructionPointer];
                        instructionPointer++;

                        if (src == Register.EAX || src == Register.EBX || src == Register.ECX || src == Register.EDX)
                        {
                            var srcVal = ReadExtendedRegister(src);
                            if (dst == Register.EAX || dst == Register.EBX || dst == Register.ECX || dst == Register.EDX)
                            {
                                var dstVal = ReadExtendedRegister(dst);
                                WriteExtendedRegister(dst, dstVal ^ srcVal);
                            }
                            else if (dst == Register.AX || dst == Register.BX || dst == Register.CX || dst == Register.DX)
                                throw new InvalidOperationException("ERROR: XOR dst is a word but source is a dword");
                            else if (dst == Register.AH || dst == Register.AL
                                || dst == Register.BH || dst == Register.BL
                                || dst == Register.CH || dst == Register.CL
                                || dst == Register.DH || dst == Register.DL)
                                throw new InvalidOperationException("ERROR: XOR dst is a byte but source is a dword");
                            else
                                throw new InvalidOperationException("ERROR: Unrecognized register for XOR dst");
                        }
                        else if (src == Register.AX || src == Register.BX || src == Register.CX || src == Register.DX)
                        {
                            var srcVal = ReadRegister(src);
                            if (dst == Register.EAX || dst == Register.EBX || dst == Register.ECX || dst == Register.EDX)
                            {
                                var dstVal = ReadExtendedRegister(dst);
                                WriteExtendedRegister(dst, dstVal ^ srcVal);
                            }
                            else if (dst == Register.AX || dst == Register.BX || dst == Register.CX || dst == Register.DX)
                            {
                                var dstVal = ReadRegister(dst);
                                WriteRegister(dst, (ushort)(dstVal ^ srcVal));
                            }
                            else if (dst == Register.AH || dst == Register.AL
                                || dst == Register.BH || dst == Register.BL
                                || dst == Register.CH || dst == Register.CL
                                || dst == Register.DH || dst == Register.DL)
                                throw new InvalidOperationException("ERROR: XOR dst is a byte but source is a word");
                            else
                                throw new InvalidOperationException("ERROR: Unrecognized register for XOR dst");
                        }
                        else if (src == Register.AH || src == Register.AL
                            || src == Register.BH || src == Register.BL
                            || src == Register.CH || src == Register.CL
                            || src == Register.DH || src == Register.DL)
                        {
                            var srcVal = ReadHalfRegister(src);
                            if (dst == Register.EAX || dst == Register.EBX || dst == Register.ECX || dst == Register.EDX)
                            {
                                var dstVal = ReadExtendedRegister(dst);
                                WriteExtendedRegister(dst, dstVal ^ srcVal);
                            }
                            else if (dst == Register.AX || dst == Register.BX || dst == Register.CX || dst == Register.DX)
                            {
                                var dstVal = ReadRegister(dst);
                                WriteRegister(dst, (ushort)(dstVal ^ srcVal));
                            }
                            else if (dst == Register.AH || dst == Register.AL
                                || dst == Register.BH || dst == Register.BL
                                || dst == Register.CH || dst == Register.CL
                                || dst == Register.DH || dst == Register.DL)
                            {
                                var dstVal = ReadHalfRegister(dst);
                                WriteHalfRegister(dst, (byte)(dstVal ^ srcVal));
                            }
                            else
                                throw new InvalidOperationException("ERROR: Unrecognized register for XOR dst");
                        }
                        else
                        {
                            Dump();
                            throw new InvalidOperationException("ERROR: Unrecognized register for XOR src");
                        }

                        break;
                    }

                default:
                    Console.Error.WriteLine($"ERROR: Unknown bytecode {instruction} EIP={instructionPointer - 1}!");
                    Dump();
                    ret = -666;
                    break;
            }

            return ret;
        }
    }
}
