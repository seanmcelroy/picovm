using System;
using System.Collections.Generic;
using System.Linq;
using picovm.Assembler;

namespace picovm.VM
{

    public class Agent64 : Agent
    {
        private ulong instructionPointer = 0;

        public new UInt64 StackPointer
        {
            get => ReadR64Register(Register.SP);
            set => WriteR64Register(Register.SP, value);
        }

        public Agent64(IKernel kernel, IEnumerable<byte> program, UInt64 entryPoint) : this(kernel, program.ToArray(), entryPoint)
        {
        }

        public Agent64(IKernel kernel, byte[] program, UInt64 entryPoint) : base(kernel, program)
        {
            StackPointer = (uint)(memory.Length - 1);
            this.instructionPointer = entryPoint;
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
                case Register.RSI:
                    return registers[R_SI];
                case Register.RDI:
                    return registers[R_DI];
                case Register.RBP:
                    return registers[R_BP];
                case Register.RIP:
                    return registers[R_IP];
                case Register.RSP:
                case Register.SP:
                    return registers[R_SP];
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
                default:
                    throw new InvalidOperationException($"ERROR: Unknown x64 register {reference}!");
            }
        }

        public ulong ReadR64Register(Register reference) => ReadR64Register(this.general_registers, reference);

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
                case Register.SP:
                    registers[R_SP] = value;
                    break;
                case Register.RDI:
                    registers[R_DI] = value;
                    break;
                case Register.RSI:
                    registers[R_SI] = value;
                    break;
                case Register.RBP:
                    registers[R_BP] = value;
                    break;
                case Register.RIP:
                    registers[R_IP] = value;
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

        public void WriteR64Register(Register reference, ulong value) => WriteR64Register(this.general_registers, reference, value);

        public ulong StackPop64()
        {
            var ret = BitConverter.ToUInt64(memory, (int)ReadR64Register(Register.SP));
            StackPointer += 8;
            return ret;
        }

        public void StackPush(ulong value)
        {
            // Push is ALWAYS a 32-bit operation.  Callers convert.
            Array.Copy(BitConverter.GetBytes(value), 0L, memory, (long)(StackPointer - 8), 8);
            StackPointer -= 8;
        }

        public override void Dump()
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

        public override int? Tick()
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
