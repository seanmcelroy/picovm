using System;
using System.Collections.Generic;
using System.Linq;
using picovm.Assembler;

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
        // Index and pointers
        public const byte R_SI = 4;
        public const byte R_DI = 5;
        public const byte R_BP = 6;
        public const byte R_IP = 7;
        public const byte R_SP = 8;
        // Indicator
        public const byte R_FLAGS = 9;
        public const byte R_8 = 10;
        public const byte R_9 = 11;
        public const byte R_10 = 12;
        public const byte R_11 = 13;
        public const byte R_12 = 14;
        public const byte R_13 = 15;
        public const byte R_14 = 16;
        public const byte R_15 = 17;

        // Segment registers
        public const byte SR_CS = 0; // Code
        public const byte SR_DS = 1; // Data
        public const byte SR_SS = 2; // Stack
        public const byte SR_ES = 3; // Extra Data
        public const byte SR_FS = 4; // Extra Data #2
        public const byte SR_GS = 5; // Extra Data #3 
        #endregion

        protected ulong[] general_registers = new ulong[18];

        protected ushort[] segment_registers = new ushort[6];

        protected bool[] flags = new bool[2];

        protected byte[] memory = new byte[65536];

        private UInt32 instructionPointer = 0;

        public UInt32 StackPointer
        {
            get => ReadExtendedRegister(Register.SP);
            set => WriteExtendedRegister(Register.SP, value);
        }

        protected IKernel kernel { get; private set; }

        public Agent(IKernel kernel, IEnumerable<byte> program, UInt32 entryPoint) : this(kernel, program.ToArray(), entryPoint)
        {
        }

        public Agent(IKernel kernel, byte[] program, UInt32 entryPoint)
        {
            this.kernel = kernel;
            Array.Copy(program, memory, program.Length);
            StackPointer = (uint)(memory.Length - 1);
            this.instructionPointer = entryPoint;
        }

        protected Agent(IKernel kernel, byte[] program)
        {
            this.kernel = kernel;
            Array.Copy(program, memory, program.Length);
            StackPointer = (uint)(memory.Length - 1);
        }

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
                case Register.ESP:
                case Register.SP:
                    {
                        var u64 = registers[R_SP];
                        ret = (uint)(u64 & uint.MaxValue);
                    }
                    break;
                case Register.EDI:
                    {
                        var u64 = registers[R_DI];
                        ret = (uint)(u64 & uint.MaxValue);
                    }
                    break;
                case Register.ESI:
                    {
                        var u64 = registers[R_SI];
                        ret = (uint)(u64 & uint.MaxValue);
                    }
                    break;
                case Register.EBP:
                    {
                        var u64 = registers[R_BP];
                        ret = (uint)(u64 & uint.MaxValue);
                    }
                    break;
                case Register.EIP:
                    {
                        var u64 = registers[R_IP];
                        ret = (uint)(u64 & uint.MaxValue);
                    }
                    break;
                default:
                    throw new InvalidOperationException($"ERROR: Unknown extended register {reference}!");
            }
            return ret;
        }

        public uint ReadExtendedRegister(Register reference) => ReadExtendedRegister(this.general_registers, reference);

        public ushort ReadRegister(Register reference)
        {
            // 16 bits
            // We want to read the right-most 16 bits of the 64-bit value
            ushort ret;
            switch (reference)
            {
                case Register.AX:
                    ret = (ushort)(general_registers[R_A] & (ulong)ushort.MaxValue);
                    break;
                case Register.BX:
                    ret = (ushort)(general_registers[R_B] & (ulong)ushort.MaxValue);
                    break;
                case Register.CX:
                    ret = (ushort)(general_registers[R_C] & (ulong)ushort.MaxValue);
                    break;
                case Register.DX:
                    ret = (ushort)(general_registers[R_D] & (ulong)ushort.MaxValue);
                    break;
                case Register.DI:
                    ret = (ushort)(general_registers[R_DI] & (ulong)ushort.MaxValue);
                    break;
                case Register.SI:
                    ret = (ushort)(general_registers[R_SI] & (ulong)ushort.MaxValue);
                    break;
                case Register.BP:
                    ret = (ushort)(general_registers[R_BP] & (ulong)ushort.MaxValue);
                    break;
                case Register.IP:
                    ret = (ushort)(general_registers[R_IP] & (ulong)ushort.MaxValue);
                    break;
                case Register.CS:
                    ret = segment_registers[SR_CS];
                    break;
                case Register.DS:
                    ret = segment_registers[SR_DS];
                    break;
                case Register.SS:
                    ret = segment_registers[SR_SS];
                    break;
                case Register.ES:
                    ret = segment_registers[SR_ES];
                    break;
                case Register.FS:
                    ret = segment_registers[SR_FS];
                    break;
                case Register.GS:
                    ret = segment_registers[SR_GS];
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
                    ret = (byte)((general_registers[R_A] & (ulong)0xFF00) >> 8);
                    break;
                case Register.AL:
                    ret = (byte)(general_registers[R_A] & (ulong)0x00FF);
                    break;
                case Register.BH:
                    ret = (byte)((general_registers[R_B] & (ulong)0xFF00) >> 8);
                    break;
                case Register.BL:
                    ret = (byte)(general_registers[R_B] & (ulong)0x00FF);
                    break;
                case Register.CH:
                    ret = (byte)((general_registers[R_C] & (ulong)0xFF00) >> 8);
                    break;
                case Register.CL:
                    ret = (byte)(general_registers[R_C] & (ulong)0x00FF);
                    break;
                case Register.DH:
                    ret = (byte)((general_registers[R_D] & (ulong)0xFF00) >> 8);
                    break;
                case Register.DL:
                    ret = (byte)(general_registers[R_D] & (ulong)0x00FF);
                    break;
                default:
                    throw new InvalidOperationException($"ERROR: Unknown register {reference}!");
            }
            return ret;
        }

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
                case Register.ESP:
                case Register.SP:
                    registers[R_SP] = (ulong)hi << 32 | lo;
                    break;
                default:
                    throw new InvalidOperationException($"ERROR: Unknown extended register {reference}!");
            }
        }

        public void WriteExtendedRegister(Register reference, uint value) => WriteExtendedRegister(this.general_registers, reference, value);

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
                    general_registers[R_A] = general_registers[R_A] & ~((ulong)ushort.MaxValue) | (ulong)value;
                    break;
                case Register.BX:
                    general_registers[R_B] = general_registers[R_B] & ~((ulong)ushort.MaxValue) | (ulong)value;
                    break;
                case Register.CX:
                    general_registers[R_C] = general_registers[R_C] & ~((ulong)ushort.MaxValue) | (ulong)value;
                    break;
                case Register.DX:
                    general_registers[R_D] = general_registers[R_D] & ~((ulong)ushort.MaxValue) | (ulong)value;
                    break;
                case Register.DI:
                    general_registers[R_DI] = general_registers[R_DI] & ~((ulong)ushort.MaxValue) | (ulong)value;
                    break;
                case Register.SI:
                    general_registers[R_SI] = general_registers[R_SI] & ~((ulong)ushort.MaxValue) | (ulong)value;
                    break;
                case Register.BP:
                    general_registers[R_BP] = general_registers[R_BP] & ~((ulong)ushort.MaxValue) | (ulong)value;
                    break;
                case Register.IP:
                    general_registers[R_IP] = general_registers[R_IP] & ~((ulong)ushort.MaxValue) | (ulong)value;
                    break;
                case Register.CS:
                    segment_registers[SR_CS] = value;
                    break;
                case Register.DS:
                    segment_registers[SR_DS] = value;
                    break;
                case Register.SS:
                    segment_registers[SR_SS] = value;
                    break;
                case Register.ES:
                    segment_registers[SR_ES] = value;
                    break;
                case Register.FS:
                    segment_registers[SR_FS] = value;
                    break;
                case Register.GS:
                    segment_registers[SR_GS] = value;
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
                    general_registers[R_A] = general_registers[R_A] & ~((ulong)0xFF00) | ((ulong)value << 8);
                    break;
                case Register.AL:
                    general_registers[R_A] = general_registers[R_A] & ~((ulong)0x00FF) | (ulong)value;
                    break;
                case Register.BH:
                    general_registers[R_B] = general_registers[R_B] & ~((ulong)0xFF00) | ((ulong)value << 8);
                    break;
                case Register.BL:
                    general_registers[R_B] = general_registers[R_B] & ~((ulong)0x00FF) | (ulong)value;
                    break;
                case Register.CH:
                    general_registers[R_C] = general_registers[R_C] & ~((ulong)0xFF00) | ((ulong)value << 8);
                    break;
                case Register.CL:
                    general_registers[R_C] = general_registers[R_C] & ~((ulong)0x00FF) | (ulong)value;
                    break;
                case Register.DH:
                    general_registers[R_D] = general_registers[R_D] & ~((ulong)0xFF00) | ((ulong)value << 8);
                    break;
                case Register.DL:
                    general_registers[R_D] = general_registers[R_D] & ~((ulong)0x00FF) | (ulong)value;
                    break;
                default:
                    throw new InvalidOperationException($"ERROR: Unknown register {reference}!");
            }
        }

        public uint StackPeek32() => BitConverter.ToUInt32(memory, (int)ReadExtendedRegister(Register.SP));

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

        public void StackPush(uint value)
        {
            // Push is ALWAYS a 32-bit operation.  Callers convert.
            Array.Copy(BitConverter.GetBytes(value), 0, memory, StackPointer - 4, 4);
            StackPointer -= 4;
        }

        public virtual void Dump()
        {
            Console.WriteLine();
            Console.Error.Write($"EAX: 0x{ReadExtendedRegister(Register.EAX):X4} ({ReadExtendedRegister(Register.EAX).ToString().PadLeft(2)})\t");
            Console.Write($"EBX: 0x{ReadExtendedRegister(Register.EBX):X4} ({ReadExtendedRegister(Register.EBX).ToString().PadLeft(2)})\t");
            Console.Write($"ECX: 0x{ReadExtendedRegister(Register.ECX):X4} ({ReadExtendedRegister(Register.ECX).ToString().PadLeft(2)})\t");
            Console.WriteLine($"EDX: 0x{ReadExtendedRegister(Register.EDX):X4} ({ReadExtendedRegister(Register.EDX).ToString().PadLeft(2)})");
            Console.WriteLine($"EIP: 0x{instructionPointer:X4} ({instructionPointer})\tESP: 0x{StackPointer:X4} ({StackPointer})");
            Console.WriteLine("(Stack)");
            var i = (ulong)memory.Length;
            var qword = new byte[8];
            do
            {
                Array.Copy(memory, (int)i - 8, qword, 0, 8);
                var output = qword.Select(b => $"{b:X2}").Aggregate((c, n) => $"{c} {n}");
                Console.WriteLine($"{i}\t: {output}");
                i -= 8;
            } while (i > StackPointer);
            Console.WriteLine("...");
            i = instructionPointer + (8 - instructionPointer % 8);
            do
            {
                Array.Copy(memory, (uint)i - 8, qword, 0, 8);
                var output = qword.Select(b => $"{b:X2}").Aggregate((c, n) => $"{c} {n}");
                Console.WriteLine($"{i}\t: {output}");
                i -= 8;
            } while (i > 0);
        }

        public virtual int? Tick()
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
                case Bytecode.SYSCALL:
                    if (kernel.HandleInterrupt(ref general_registers, ref memory))
                        return 0;
                    break;
                case Bytecode.INT:
                    {
                        // Interrupt number
                        var interruptVector = memory[(int)instructionPointer];
                        instructionPointer++;

                        switch (interruptVector)
                        {
                            // Linux kernel interrupt
                            case 0x80:
                                if (kernel.HandleInterrupt(ref general_registers, ref memory))
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
                            case 4:
                                {
                                    var srcVal = ReadExtendedRegister(src);
                                    switch (dst.Size())
                                    {
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

                        if (operand == Register.EAX || operand == Register.EBX || operand == Register.ECX || operand == Register.EDX || operand == Register.EDI || operand == Register.ESI)
                            WriteExtendedRegister(operand, StackPop32());
                        else if (
                            operand == Register.AX || operand == Register.BX || operand == Register.CX || operand == Register.DX ||
                            operand == Register.DI || operand == Register.SI || operand == Register.BP || operand == Register.IP ||
                            operand == Register.CS || operand == Register.DS ||
                            operand == Register.SS || operand == Register.ES ||
                            operand == Register.FS || operand == Register.GS)
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

                        if (operand == Register.EAX || operand == Register.EBX || operand == Register.ECX || operand == Register.EDX || operand == Register.EDI || operand == Register.ESI)
                        {
                            var loc = ReadExtendedRegister(operand);
                            var val = BitConverter.ToUInt32(memory, (int)loc);
                            StackPush(val);
                        }
                        else if (
                            operand == Register.AX || operand == Register.BX || operand == Register.CX || operand == Register.DX ||
                            operand == Register.DI || operand == Register.SI || operand == Register.BP || operand == Register.IP ||
                            operand == Register.CS || operand == Register.DS ||
                            operand == Register.SS || operand == Register.ES ||
                            operand == Register.FS || operand == Register.GS)
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
                            if (dst == Register.EAX || dst == Register.EBX || dst == Register.ECX || dst == Register.EDX || dst == Register.EDI || dst == Register.ESI)
                            {
                                var dstVal = ReadExtendedRegister(dst);
                                WriteExtendedRegister(dst, dstVal ^ srcVal);
                            }
                            else if (
                                dst == Register.AX || dst == Register.BX || dst == Register.CX || dst == Register.DX ||
                                dst == Register.DI || dst == Register.SI || dst == Register.BP || dst == Register.IP ||
                                dst == Register.CS || dst == Register.DS ||
                                dst == Register.SS || dst == Register.ES ||
                                dst == Register.FS || dst == Register.GS)
                                throw new InvalidOperationException("ERROR: XOR dst is a word but source is a dword");
                            else if (dst == Register.AH || dst == Register.AL
                                || dst == Register.BH || dst == Register.BL
                                || dst == Register.CH || dst == Register.CL
                                || dst == Register.DH || dst == Register.DL)
                                throw new InvalidOperationException("ERROR: XOR dst is a byte but source is a dword");
                            else
                                throw new InvalidOperationException("ERROR: Unrecognized register for XOR dst");
                        }
                        else if (
                                src == Register.AX || src == Register.BX || src == Register.CX || src == Register.DX ||
                                src == Register.DI || src == Register.SI || src == Register.BP || src == Register.IP ||
                                src == Register.CS || src == Register.DS ||
                                src == Register.SS || src == Register.ES ||
                                src == Register.FS || src == Register.GS)
                        {
                            var srcVal = ReadRegister(src);
                            if (dst == Register.EAX || dst == Register.EBX || dst == Register.ECX || dst == Register.EDX || dst == Register.EDI || dst == Register.ESI)
                            {
                                var dstVal = ReadExtendedRegister(dst);
                                WriteExtendedRegister(dst, dstVal ^ srcVal);
                            }
                            else if (
                                dst == Register.AX || dst == Register.BX || dst == Register.CX || dst == Register.DX ||
                                dst == Register.DI || dst == Register.SI || dst == Register.BP || dst == Register.IP ||
                                dst == Register.CS || dst == Register.DS ||
                                dst == Register.SS || dst == Register.ES ||
                                dst == Register.FS || dst == Register.GS)
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
                            if (dst == Register.EAX || dst == Register.EBX || dst == Register.ECX || dst == Register.EDX || dst == Register.EDI || dst == Register.ESI)
                            {
                                var dstVal = ReadExtendedRegister(dst);
                                WriteExtendedRegister(dst, dstVal ^ srcVal);
                            }
                            else if (
                                dst == Register.AX || dst == Register.BX || dst == Register.CX || dst == Register.DX ||
                                dst == Register.DI || dst == Register.SI || dst == Register.BP || dst == Register.IP ||
                                dst == Register.CS || dst == Register.DS ||
                                dst == Register.SS || dst == Register.ES ||
                                dst == Register.FS || dst == Register.GS)
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
