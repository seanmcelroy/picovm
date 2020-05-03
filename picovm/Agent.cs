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

        internal byte[] SliceMemory(uint startIndex, uint length)
        {
            var ret = new byte[length];
            Array.Copy(memory, startIndex, ret, 0, length);
            return ret;
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
            Console.WriteLine($"EAX: 0x{ReadExtendedRegister(Register.EAX):X4} ({ReadExtendedRegister(Register.EAX).ToString().PadLeft(2)})\tEBX: 0x{ReadExtendedRegister(Register.EBX):X4} ({ReadExtendedRegister(Register.EBX)})");
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

                        if (operand1 == Register.EAX || operand1 == Register.EBX)
                        {
                            var operand1value = ReadExtendedRegister(operand1);
                            instructionPointer++;
                            var operand2value = BitConverter.ToUInt32(memory, (int)instructionPointer);
                            instructionPointer += 4;
                            WriteExtendedRegister(operand1, operand1value + operand2value);
                        }
                        else if (operand1 == Register.AX)
                        {
                            var operand1value = ReadRegister(operand1);
                            instructionPointer++;
                            var operand2value = BitConverter.ToUInt16(memory, (int)instructionPointer);
                            instructionPointer += 2;
                            WriteRegister(operand1, ((ushort)(operand1value + operand2value)));
                        }
                        else if (operand1 == Register.AH || operand1 == Register.AL)
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

                        if (operand1 == Register.EAX || operand1 == Register.EBX)
                        {
                            var loc = ReadExtendedRegister(operand1);
                            var operand1value = BitConverter.ToUInt32(memory, (int)loc);
                            var operand2value = BitConverter.ToUInt32(memory, (int)instructionPointer);
                            instructionPointer += 4;
                            Array.Copy(BitConverter.GetBytes(operand1value + operand2value), 0, memory, loc, 4);
                        }
                        else if (operand1 == Register.AX)
                        {
                            var loc = ReadRegister(operand1);
                            var operand1value = BitConverter.ToUInt16(memory, (int)loc);
                            var operand2value = BitConverter.ToUInt16(memory, (int)instructionPointer);
                            instructionPointer += 2;
                            Array.Copy(BitConverter.GetBytes((ushort)(operand1value + operand2value)), 0, memory, loc, 2);
                        }
                        else if (operand1 == Register.AH || operand1 == Register.AL)
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
                case Bytecode.MOV_REG_REG:
                    {
                        var dst = (Register)memory[instructionPointer];
                        instructionPointer++;
                        var src = (Register)memory[instructionPointer];
                        instructionPointer++;

                        if (src == Register.EAX || src == Register.EBX)
                        {
                            var srcVal = ReadExtendedRegister(src);
                            if (src == Register.EAX || src == Register.EBX)
                                WriteExtendedRegister(dst, srcVal);
                            else if (src == Register.AX)
                                throw new InvalidOperationException("ERROR: MOV dst is a word but source is a dword");
                            else if (src == Register.AH || src == Register.AL)
                                throw new InvalidOperationException("ERROR: MOV dst is a byte but source is a dword");
                            else
                                throw new InvalidOperationException("ERROR: Unrecognized register for MOV dst");
                        }
                        else if (src == Register.AX)
                        {
                            var srcVal = ReadRegister(src);
                            if (src == Register.EAX || src == Register.EBX)
                                WriteExtendedRegister(dst, srcVal);
                            else if (src == Register.AX)
                                WriteRegister(dst, srcVal);
                            else if (src == Register.AH || src == Register.AL)
                                throw new InvalidOperationException("ERROR: MOV dst is a byte but source is a word");
                            else
                                throw new InvalidOperationException("ERROR: Unrecognized register for MOV dst");
                        }
                        else if (src == Register.AH || src == Register.AL)
                        {
                            var srcVal = ReadHalfRegister(src);
                            if (src == Register.EAX || src == Register.EBX)
                                WriteExtendedRegister(dst, srcVal);
                            else if (src == Register.AX)
                                WriteRegister(dst, srcVal);
                            else if (src == Register.AH || src == Register.AL)
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

                        var src = (Register)memory[instructionPointer];
                        instructionPointer++;

                        if (src == Register.EAX || src == Register.EBX)
                        {
                            var loc = ReadExtendedRegister(src);
                            var val = BitConverter.ToUInt32(memory, (int)loc);
                            WriteExtendedRegister(dst, val);
                        }
                        else if (src == Register.AX)
                        {
                            var loc = ReadRegister(src);
                            var val = BitConverter.ToUInt16(memory, (int)loc);
                            WriteRegister(dst, val);
                        }
                        else if (src == Register.AH || src == Register.AL)
                        {
                            var loc = ReadHalfRegister(src);
                            var val = memory[(int)loc];
                            WriteHalfRegister(dst, val);
                        }
                        else
                        {
                            Dump();
                            throw new InvalidOperationException("ERROR: Unrecognized register for MOV src");
                        }

                        break;
                    }
                case Bytecode.MOV_REG_CON:
                    {
                        var dst = (Register)memory[instructionPointer];
                        instructionPointer++;

                        if (dst == Register.EAX || dst == Register.EBX)
                        {
                            var val = BitConverter.ToUInt32(memory, (int)instructionPointer);
                            instructionPointer += 4;
                            WriteExtendedRegister(dst, val);
                        }
                        else if (dst == Register.AX)
                        {
                            var val = BitConverter.ToUInt16(memory, (int)instructionPointer);
                            instructionPointer += 2;
                            WriteRegister(dst, val);
                        }
                        else if (dst == Register.AH || dst == Register.AL)
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

                        if (operand == Register.EAX || operand == Register.EBX)
                            WriteExtendedRegister(operand, StackPop32());
                        else if (operand == Register.AX)
                            WriteRegister(operand, StackPop16());
                        else if (operand == Register.AH || operand == Register.AL)
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

                        if (operand == Register.EAX || operand == Register.EBX)
                        {
                            var loc = ReadExtendedRegister(operand);
                            Array.Copy(BitConverter.GetBytes(StackPop32()), 0, memory, loc, 4);
                        }
                        else if (operand == Register.AX)
                        {
                            var loc = ReadRegister(operand);
                            Array.Copy(BitConverter.GetBytes(StackPop16()), 0, memory, loc, 2);
                        }
                        else if (operand == Register.AH || operand == Register.AL)
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
                        if (operand == Register.EAX || operand == Register.EBX)
                            StackPush(ReadExtendedRegister(operand));
                        else if (operand == Register.AX)
                            StackPush(ReadRegister(operand));
                        else if (operand == Register.AH || operand == Register.AL)
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

                        if (operand == Register.EAX || operand == Register.EBX)
                        {
                            var loc = ReadExtendedRegister(operand);
                            var val = BitConverter.ToUInt32(memory, (int)loc);
                            StackPush(val);
                        }
                        else if (operand == Register.AX)
                        {
                            var loc = ReadRegister(operand);
                            var val = BitConverter.ToUInt16(memory, (int)loc);
                            StackPush(val);
                        }
                        else if (operand == Register.AH || operand == Register.AL)
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
                default:
                    Console.WriteLine($"ERROR: Unknown bytecode {instruction} EIP={instructionPointer - 1}!");
                    ret = E_INVALID;
                    break;
            }

            return ret;
        }
    }
}
