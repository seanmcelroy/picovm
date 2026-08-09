using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
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
            instructionPointer = entryPoint;
        }

        public static ulong ReadR64Register(ulong[] registers, Register reference)
        {
            return reference switch
            {
                Register.RAX => registers[R_A],
                Register.RBX => registers[R_B],
                Register.RCX => registers[R_C],
                Register.RDX => registers[R_D],
                Register.RSI => registers[R_SI],
                Register.RDI => registers[R_DI],
                Register.RBP => registers[R_BP],
                Register.RIP => registers[R_IP],
                Register.RSP or Register.SP => registers[R_SP],
                Register.R8 => registers[R_8],
                Register.R9 => registers[R_9],
                Register.R10 => registers[R_10],
                Register.R11 => registers[R_11],
                Register.R12 => registers[R_12],
                Register.R13 => registers[R_13],
                Register.R14 => registers[R_14],
                Register.R15 => registers[R_15],
                _ => throw new InvalidOperationException($"ERROR: Unknown x64 register {reference}!"),
            };
        }

        public ulong ReadR64Register(Register reference) => ReadR64Register(general_registers, reference);

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

        public void WriteR64Register(Register reference, ulong value) => WriteR64Register(general_registers, reference, value);

        public ulong StackPop64()
        {
            var ret = ReadMemoryUInt64(ReadR64Register(Register.SP));
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

        public override TickResult Tick()
        {
            var instruction = (Bytecode)memory[instructionPointer];
            instructionPointer++;

            switch (instruction)
            {
                case Bytecode.END:
                    return new TickResult(TickErrorCode.Ok, true);
                case Bytecode.ADD_REG_CON:
                    {
                        var operand1 = (Register)memory[instructionPointer];
                        instructionPointer++;

                        switch (operand1.Size())
                        {
                            case 8:
                                {
                                    var operand1value = ReadR64Register(operand1);
                                    var operand2value = ReadMemoryUInt64(instructionPointer);
                                    instructionPointer += 8;
                                    WriteR64Register(operand1, operand1value + operand2value);

                                    var result = (BigInteger)operand1value + operand2value;
                                    var operand1Signed = (long)operand1value; // Re-interpret as signed
                                    var operand2Signed = (long)operand2value; // Re-interpret as signed
                                    var resultSigned = operand1Signed + operand2Signed;

                                    WriteStatusRegister(Flag.ZERO_FLAG, resultSigned == 0);
                                    WriteStatusRegister(Flag.CARRY_FLAG, result > ulong.MaxValue);
                                    WriteStatusRegister(Flag.SIGN_FLAG, resultSigned < 0);
                                    WriteStatusRegister(Flag.OVERFLOW_FLAG, ((operand1Signed ^ operand2Signed) & (operand1Signed ^ resultSigned)) < 0);
                                    WriteStatusRegister(Flag.AUX_CARRY_FLAG, (operand1value & 0xF) + (operand2value & 0xF) > 0xF);
                                    WriteStatusRegister(Flag.PARITY_FLAG, ByteUtility.CountBits((ulong)(result & 0xFF)) % 2 == 0);

                                    break;
                                }
                            case 4:
                                {
                                    var operand1value = ReadExtendedRegister(operand1);
                                    var operand2value = ReadMemoryUInt32(instructionPointer);
                                    instructionPointer += 4;
                                    WriteExtendedRegister(operand1, operand1value + operand2value);

                                    var result = (long)operand1value + operand2value;
                                    var operand1Signed = (int)operand1value; // Re-interpret as signed
                                    var operand2Signed = (int)operand2value; // Re-interpret as signed
                                    var resultSigned = operand1Signed + operand2Signed;

                                    WriteStatusRegister(Flag.ZERO_FLAG, resultSigned == 0);
                                    WriteStatusRegister(Flag.CARRY_FLAG, result > uint.MaxValue);
                                    WriteStatusRegister(Flag.SIGN_FLAG, resultSigned < 0);
                                    WriteStatusRegister(Flag.OVERFLOW_FLAG, ((operand1Signed ^ operand2Signed) & (operand1Signed ^ resultSigned)) < 0);
                                    WriteStatusRegister(Flag.AUX_CARRY_FLAG, (operand1value & 0xF) + (operand2value & 0xF) > 0xF);
                                    WriteStatusRegister(Flag.PARITY_FLAG, ByteUtility.CountBits(result & 0xFF) % 2 == 0);

                                    break;
                                }
                            case 2:
                                {
                                    var operand1value = ReadRegister(operand1);
                                    var operand2value = BitConverter.ToUInt16(memory, (int)instructionPointer);
                                    instructionPointer += 2;
                                    WriteRegister(operand1, (ushort)(operand1value + operand2value));

                                    var result = (uint)operand1value + operand2value;
                                    var operand1Signed = (short)operand1value; // Re-interpret as signed
                                    var operand2Signed = (short)operand2value; // Re-interpret as signed
                                    var resultSigned = (short)(operand1Signed + operand2Signed);

                                    WriteStatusRegister(Flag.ZERO_FLAG, resultSigned == 0);
                                    WriteStatusRegister(Flag.CARRY_FLAG, result > ushort.MaxValue);
                                    WriteStatusRegister(Flag.SIGN_FLAG, resultSigned < 0);
                                    WriteStatusRegister(Flag.OVERFLOW_FLAG, ((operand1Signed ^ operand2Signed) & (operand1Signed ^ resultSigned)) < 0);
                                    WriteStatusRegister(Flag.AUX_CARRY_FLAG, (operand1value & 0xF) + (operand2value & 0xF) > 0xF);
                                    WriteStatusRegister(Flag.PARITY_FLAG, ByteUtility.CountBits(result & 0xFF) % 2 == 0);

                                    break;
                                }
                            case 1:
                                {
                                    var operand1value = ReadHalfRegister(operand1);
                                    var operand2value = memory[(int)instructionPointer];
                                    instructionPointer++;
                                    WriteHalfRegister(operand1, (byte)(operand1value + operand2value));

                                    var result = operand1value + operand2value;
                                    var operand1Signed = (sbyte)operand1value; // Re-interpret as signed
                                    var operand2Signed = (sbyte)operand2value; // Re-interpret as signed
                                    var resultSigned = (sbyte)(operand1Signed + operand2Signed);

                                    WriteStatusRegister(Flag.ZERO_FLAG, resultSigned == 0);
                                    WriteStatusRegister(Flag.CARRY_FLAG, result > byte.MaxValue);
                                    WriteStatusRegister(Flag.SIGN_FLAG, resultSigned < 0);
                                    WriteStatusRegister(Flag.OVERFLOW_FLAG, ((operand1Signed ^ operand2Signed) & (operand1Signed ^ resultSigned)) < 0);
                                    WriteStatusRegister(Flag.AUX_CARRY_FLAG, (operand1value & 0xF) + (operand2value & 0xF) > 0xF);
                                    WriteStatusRegister(Flag.PARITY_FLAG, ByteUtility.CountBits(result & 0xFF) % 2 == 0);

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
                                    var operand1value = ReadMemoryUInt64(loc);
                                    var operand2value = ReadMemoryUInt64(instructionPointer);
                                    instructionPointer += 8;
                                    Array.Copy(BitConverter.GetBytes(operand1value + operand2value), 0L, memory, (long)loc, 8);

                                    var result = (BigInteger)operand1value + operand2value;
                                    var operand1Signed = (long)operand1value; // Re-interpret as signed
                                    var operand2Signed = (long)operand2value; // Re-interpret as signed
                                    var resultSigned = operand1Signed + operand2Signed;

                                    WriteStatusRegister(Flag.ZERO_FLAG, resultSigned == 0);
                                    WriteStatusRegister(Flag.CARRY_FLAG, result > ulong.MaxValue);
                                    WriteStatusRegister(Flag.SIGN_FLAG, resultSigned < 0);
                                    WriteStatusRegister(Flag.OVERFLOW_FLAG, ((operand1Signed ^ operand2Signed) & (operand1Signed ^ resultSigned)) < 0);
                                    WriteStatusRegister(Flag.AUX_CARRY_FLAG, (operand1value & 0xF) + (operand2value & 0xF) > 0xF);
                                    WriteStatusRegister(Flag.PARITY_FLAG, ByteUtility.CountBits((ulong)(result & 0xFF)) % 2 == 0);
                                    break;
                                }
                            case 4:
                                {
                                    var loc = ReadExtendedRegister(operand1);
                                    var operand1value = ReadMemoryUInt32(loc);
                                    var operand2value = ReadMemoryUInt32(instructionPointer);
                                    instructionPointer += 4;
                                    Array.Copy(BitConverter.GetBytes(operand1value + operand2value), 0, memory, loc, 4);

                                    var result = (long)operand1value + operand2value;
                                    var operand1Signed = (int)operand1value; // Re-interpret as signed
                                    var operand2Signed = (int)operand2value; // Re-interpret as signed
                                    var resultSigned = operand1Signed + operand2Signed;

                                    WriteStatusRegister(Flag.ZERO_FLAG, resultSigned == 0);
                                    WriteStatusRegister(Flag.CARRY_FLAG, result > uint.MaxValue);
                                    WriteStatusRegister(Flag.SIGN_FLAG, resultSigned < 0);
                                    WriteStatusRegister(Flag.OVERFLOW_FLAG, ((operand1Signed ^ operand2Signed) & (operand1Signed ^ resultSigned)) < 0);
                                    WriteStatusRegister(Flag.AUX_CARRY_FLAG, (operand1value & 0xF) + (operand2value & 0xF) > 0xF);
                                    WriteStatusRegister(Flag.PARITY_FLAG, ByteUtility.CountBits(result & 0xFF) % 2 == 0);
                                    break;
                                }
                            case 2:
                                {
                                    var loc = ReadRegister(operand1);
                                    var operand1value = ReadMemoryUInt16(loc);
                                    var operand2value = ReadMemoryUInt16(instructionPointer);
                                    instructionPointer += 2;
                                    Array.Copy(BitConverter.GetBytes((ushort)(operand1value + operand2value)), 0, memory, loc, 2);

                                    var result = (uint)operand1value + operand2value;
                                    var operand1Signed = (short)operand1value; // Re-interpret as signed
                                    var operand2Signed = (short)operand2value; // Re-interpret as signed
                                    var resultSigned = (short)(operand1Signed + operand2Signed);

                                    WriteStatusRegister(Flag.ZERO_FLAG, resultSigned == 0);
                                    WriteStatusRegister(Flag.CARRY_FLAG, result > ushort.MaxValue);
                                    WriteStatusRegister(Flag.SIGN_FLAG, resultSigned < 0);
                                    WriteStatusRegister(Flag.OVERFLOW_FLAG, ((operand1Signed ^ operand2Signed) & (operand1Signed ^ resultSigned)) < 0);
                                    WriteStatusRegister(Flag.AUX_CARRY_FLAG, (operand1value & 0xF) + (operand2value & 0xF) > 0xF);
                                    WriteStatusRegister(Flag.PARITY_FLAG, ByteUtility.CountBits(result & 0xFF) % 2 == 0);
                                    break;
                                }
                            case 1:
                                {
                                    var loc = ReadHalfRegister(operand1);
                                    var operand1value = ReadMemoryByte(loc);
                                    var operand2value = ReadMemoryByte(instructionPointer);
                                    instructionPointer += 1;
                                    memory[loc] = (byte)(operand1value + operand2value);

                                    var result = operand1value + operand2value;
                                    var operand1Signed = (sbyte)operand1value; // Re-interpret as signed
                                    var operand2Signed = (sbyte)operand2value; // Re-interpret as signed
                                    var resultSigned = (sbyte)(operand1Signed + operand2Signed);

                                    WriteStatusRegister(Flag.ZERO_FLAG, resultSigned == 0);
                                    WriteStatusRegister(Flag.CARRY_FLAG, result > byte.MaxValue);
                                    WriteStatusRegister(Flag.SIGN_FLAG, resultSigned < 0);
                                    WriteStatusRegister(Flag.OVERFLOW_FLAG, ((operand1Signed ^ operand2Signed) & (operand1Signed ^ resultSigned)) < 0);
                                    WriteStatusRegister(Flag.AUX_CARRY_FLAG, (operand1value & 0xF) + (operand2value & 0xF) > 0xF);
                                    WriteStatusRegister(Flag.PARITY_FLAG, ByteUtility.CountBits(result & 0xFF) % 2 == 0);

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
                                    var operand2value = ReadMemoryUInt64(instructionPointer);
                                    instructionPointer += 8;
                                    var val = operand1value & operand2value;
                                    WriteR64Register(operand1, val);

                                    var result = operand1value & operand2value;
                                    var operand1Signed = (long)operand1value; // Re-interpret as signed
                                    var operand2Signed = (long)operand2value; // Re-interpret as signed
                                    var resultSigned = operand1Signed & operand2Signed;

                                    WriteStatusRegister(Flag.ZERO_FLAG, resultSigned == 0);
                                    WriteStatusRegister(Flag.CARRY_FLAG, false);
                                    WriteStatusRegister(Flag.SIGN_FLAG, resultSigned < 0);
                                    WriteStatusRegister(Flag.OVERFLOW_FLAG, false);
                                    WriteStatusRegister(Flag.AUX_CARRY_FLAG, false);
                                    WriteStatusRegister(Flag.PARITY_FLAG, ByteUtility.CountBits(result & 0xFF) % 2 == 0);

                                    break;
                                }
                            case 4:
                                {
                                    var operand1value = ReadExtendedRegister(operand1);
                                    var operand2value = ReadMemoryUInt32(instructionPointer);
                                    instructionPointer += 4;
                                    var val = operand1value & operand2value;
                                    WriteExtendedRegister(operand1, val);

                                    var result = (long)operand1value & operand2value;
                                    var operand1Signed = (int)operand1value; // Re-interpret as signed
                                    var operand2Signed = (int)operand2value; // Re-interpret as signed
                                    var resultSigned = operand1Signed & operand2Signed;

                                    WriteStatusRegister(Flag.ZERO_FLAG, resultSigned == 0);
                                    WriteStatusRegister(Flag.CARRY_FLAG, false);
                                    WriteStatusRegister(Flag.SIGN_FLAG, resultSigned < 0);
                                    WriteStatusRegister(Flag.OVERFLOW_FLAG, false);
                                    WriteStatusRegister(Flag.AUX_CARRY_FLAG, false);
                                    WriteStatusRegister(Flag.PARITY_FLAG, ByteUtility.CountBits(result & 0xFF) % 2 == 0);
                                    break;
                                }
                            case 2:
                                {
                                    var operand1value = ReadRegister(operand1);
                                    var operand2value = ReadMemoryUInt16(instructionPointer);
                                    instructionPointer += 2;
                                    var val = (ushort)(operand1value & operand2value);
                                    WriteRegister(operand1, val);

                                    var result = (uint)operand1value & operand2value;
                                    var operand1Signed = (short)operand1value; // Re-interpret as signed
                                    var operand2Signed = (short)operand2value; // Re-interpret as signed
                                    var resultSigned = (short)(operand1Signed & operand2Signed);

                                    WriteStatusRegister(Flag.ZERO_FLAG, resultSigned == 0);
                                    WriteStatusRegister(Flag.CARRY_FLAG, false);
                                    WriteStatusRegister(Flag.SIGN_FLAG, resultSigned < 0);
                                    WriteStatusRegister(Flag.OVERFLOW_FLAG, false);
                                    WriteStatusRegister(Flag.AUX_CARRY_FLAG, false);
                                    WriteStatusRegister(Flag.PARITY_FLAG, ByteUtility.CountBits(result & 0xFF) % 2 == 0);
                                    break;
                                }
                            case 1:
                                {
                                    var operand1value = ReadHalfRegister(operand1);
                                    var operand2value = ReadMemoryByte(instructionPointer);
                                    instructionPointer++;
                                    var val = (byte)(operand1value & operand2value);
                                    WriteHalfRegister(operand1, val);

                                    var result = operand1value & operand2value;
                                    var operand1Signed = (sbyte)operand1value; // Re-interpret as signed
                                    var operand2Signed = (sbyte)operand2value; // Re-interpret as signed
                                    var resultSigned = (sbyte)(operand1Signed & operand2Signed);

                                    WriteStatusRegister(Flag.ZERO_FLAG, resultSigned == 0);
                                    WriteStatusRegister(Flag.CARRY_FLAG, false);
                                    WriteStatusRegister(Flag.SIGN_FLAG, resultSigned < 0);
                                    WriteStatusRegister(Flag.OVERFLOW_FLAG, false);
                                    WriteStatusRegister(Flag.AUX_CARRY_FLAG, false);
                                    WriteStatusRegister(Flag.PARITY_FLAG, ByteUtility.CountBits(result & 0xFF) % 2 == 0);
                                    break;
                                }
                            default:
                                throw new InvalidOperationException($"ERROR: AND cannot handle the type of register targeted: {operand1}");
                        }
                        break;
                    }
                case Bytecode.CMP_REG_CON:
                    {
                        var operand1 = (Register)memory[instructionPointer];

                        switch (operand1.Size())
                        {
                            case 8:
                                {
                                    // For example: CMP RAX, imm64
                                    var operand1value = ReadR64Register(operand1);
                                    instructionPointer++;
                                    var operand2value = ReadMemoryUInt64(instructionPointer);
                                    instructionPointer += 8;

                                    var result = operand1value - operand2value;
                                    var operand1valueLong = (long)operand1value; // Re-interpret as signed
                                    var operand2valueLong = (long)operand2value; // Re-interpret as signed
                                    var resultSignedLong = operand1valueLong - operand2valueLong;

                                    WriteStatusRegister(Flag.ZERO_FLAG, result == 0);
                                    WriteStatusRegister(Flag.CARRY_FLAG, operand1value < operand2value);
                                    WriteStatusRegister(Flag.SIGN_FLAG, resultSignedLong < 0);
                                    WriteStatusRegister(Flag.OVERFLOW_FLAG, ((operand1valueLong ^ operand2valueLong) & (operand1valueLong ^ resultSignedLong)) < 0);
                                    WriteStatusRegister(Flag.AUX_CARRY_FLAG, (operand1value & 0xF) < (operand2value & 0xF));
                                    WriteStatusRegister(Flag.PARITY_FLAG, ByteUtility.CountBits(result & 0xFF) % 2 == 0);
                                    break;
                                }
                            case 4:
                                {
                                    // For example: CMP EAX, imm32
                                    var operand1value = ReadExtendedRegister(operand1);
                                    instructionPointer++;
                                    var operand2value = BitConverter.ToUInt32(memory, (int)instructionPointer);
                                    instructionPointer += 4;

                                    var result = (long)operand1value - operand2value;
                                    var operand1valueInt = (int)operand1value; // Re-interpret as signed
                                    var operand2valueInt = (int)operand2value; // Re-interpret as signed
                                    var resultSignedInt = operand1valueInt - operand2valueInt;

                                    WriteStatusRegister(Flag.ZERO_FLAG, result == 0);
                                    WriteStatusRegister(Flag.CARRY_FLAG, operand1value < operand2value);
                                    WriteStatusRegister(Flag.SIGN_FLAG, resultSignedInt < 0);
                                    WriteStatusRegister(Flag.OVERFLOW_FLAG, ((operand1valueInt ^ operand2valueInt) & (operand1valueInt ^ resultSignedInt)) < 0);
                                    WriteStatusRegister(Flag.AUX_CARRY_FLAG, (operand1value & 0xF) < (operand2value & 0xF));
                                    WriteStatusRegister(Flag.PARITY_FLAG, ByteUtility.CountBits(result & 0xFF) % 2 == 0);
                                    break;
                                }
                            case 2:
                                {
                                    // For example: CMP AX, imm16
                                    var operand1value = ReadRegister(operand1);
                                    instructionPointer++;
                                    var operand2value = BitConverter.ToUInt16(memory, (int)instructionPointer);
                                    instructionPointer += 2;

                                    var result = (int)operand1value - operand2value;
                                    var operand1valueShort = (short)operand1value; // Re-interpret as signed
                                    var operand2valueShort = (short)operand2value; // Re-interpret as signed
                                    var resultSignedShort = (short)(operand1valueShort - operand2valueShort);

                                    WriteStatusRegister(Flag.ZERO_FLAG, result == 0);
                                    WriteStatusRegister(Flag.CARRY_FLAG, operand1value < operand2value);
                                    WriteStatusRegister(Flag.SIGN_FLAG, resultSignedShort < 0);
                                    WriteStatusRegister(Flag.OVERFLOW_FLAG, ((operand1valueShort ^ operand2valueShort) & (operand1valueShort ^ resultSignedShort)) < 0);
                                    WriteStatusRegister(Flag.AUX_CARRY_FLAG, (operand1value & 0xF) < (operand2value & 0xF));
                                    WriteStatusRegister(Flag.PARITY_FLAG, ByteUtility.CountBits(result & 0xFF) % 2 == 0);
                                    break;
                                }
                            case 1:
                                {
                                    // For example: CMP AL, imm8
                                    var operand1value = ReadHalfRegister(operand1);
                                    instructionPointer++;
                                    var operand2value = memory[(int)instructionPointer];
                                    instructionPointer++;

                                    var result = (short)operand1value - operand2value;
                                    var operand1valueSbyte = (sbyte)operand1value; // Re-interpret as signed
                                    var operand2valueSbyte = (sbyte)operand2value; // Re-interpret as signed
                                    var resultSignedSbyte = (sbyte)(operand1valueSbyte - operand2valueSbyte);

                                    WriteStatusRegister(Flag.ZERO_FLAG, result == 0);
                                    WriteStatusRegister(Flag.CARRY_FLAG, operand1value < operand2value);
                                    WriteStatusRegister(Flag.SIGN_FLAG, resultSignedSbyte < 0);
                                    WriteStatusRegister(Flag.OVERFLOW_FLAG, ((operand1valueSbyte ^ operand2valueSbyte) & (operand1valueSbyte ^ resultSignedSbyte)) < 0);
                                    WriteStatusRegister(Flag.AUX_CARRY_FLAG, (operand1value & 0xF) < (operand2value & 0xF));
                                    WriteStatusRegister(Flag.PARITY_FLAG, ByteUtility.CountBits(result & 0xFF) % 2 == 0);
                                    break;
                                }
                            default:
                                throw new InvalidOperationException($"ERROR: CMP cannot handle the type of register targeted: {operand1}");
                        }
                        break;
                    }
                case Bytecode.SYSCALL:
                    // A true result from the kernel means the syscall terminated the program (sys_exit).
                    if (kernel.HandleInterrupt(ref general_registers, ref memory))
                        return new TickResult(TickErrorCode.Ok, true);
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
                                // A true result from the kernel means the syscall terminated the program (sys_exit).
                                if (kernel.HandleInterrupt(ref general_registers, ref memory))
                                    return new TickResult(TickErrorCode.Ok, true);
                                break;
                        }

                        break;
                    }
                case Bytecode.MOV_REGISTER:
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
                case Bytecode.MOV_IMMEDIATE:
                    {
                        var dst = (Register)memory[instructionPointer];
                        instructionPointer++;

                        switch (dst.Size())
                        {
                            case 8:
                                {
                                    var val = ReadMemoryUInt64(instructionPointer);
                                    instructionPointer += 8;
                                    WriteR64Register(dst, val);
                                    break;
                                }
                            case 4:
                                {
                                    var val = ReadMemoryUInt32(instructionPointer);
                                    instructionPointer += 4;
                                    WriteExtendedRegister(dst, val);
                                    break;
                                }
                            case 2:
                                {
                                    var val = ReadMemoryUInt16(instructionPointer);
                                    instructionPointer += 2;
                                    WriteRegister(dst, val);
                                    break;
                                }
                            case 1:
                                {
                                    var val = ReadMemoryByte(instructionPointer);
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
                case Bytecode.MOV_INDIRECT:
                    {
                        var dst = (Register)memory[instructionPointer];
                        instructionPointer++;
                        var src = (Register)memory[instructionPointer];
                        instructionPointer++;

                        // The source register holds the address to dereference, not a value.
                        ulong addr = src.Size() switch
                        {
                            8 => ReadR64Register(src),
                            4 => ReadExtendedRegister(src),
                            2 => ReadRegister(src),
                            1 => ReadHalfRegister(src),
                            _ => throw new InvalidOperationException($"ERROR: Unrecognized register for MOV src: {src}")
                        };

                        // The destination register's width decides how much to load.
                        switch (dst.Size())
                        {
                            case 8:
                                {
                                    var val = ReadMemoryUInt64(addr);
                                    WriteR64Register(dst, val);
                                    break;
                                }
                            case 4:
                                {
                                    var val = ReadMemoryUInt32(addr);
                                    WriteExtendedRegister(dst, val);
                                    break;
                                }
                            case 2:
                                {
                                    var val = ReadMemoryUInt16(addr);
                                    WriteRegister(dst, val);
                                    break;
                                }
                            case 1:
                                {
                                    var val = ReadMemoryByte(addr);
                                    WriteHalfRegister(dst, val);
                                    break;
                                }
                            default:
                                Dump();
                                throw new InvalidOperationException($"ERROR: Unrecognized register for MOV dst: {dst}");
                        }
                        break;
                    }
                case Bytecode.MOV_DIRECT: // aka MOV [counter], 65 ; Formerly MOV_MEM_CON
                    {
                        // Absolute destination address, always machine width, patched at link time.
                        var addr = ReadMemoryUInt64(instructionPointer);
                        instructionPointer += 8;

                        // Width of the store.  Unlike every other MOV, no operand implies it: the
                        // destination is a bare address, so the assembler states it explicitly.
                        var size = ReadMemoryByte(instructionPointer);
                        instructionPointer++;

                        if (size != 1 && size != 2 && size != 4 && size != 8)
                        {
                            Dump();
                            throw new InvalidOperationException($"ERROR: Unsupported operand size for MOV: {size}");
                        }

                        // The immediate is already little-endian in the instruction stream and the
                        // destination is raw memory, so the store is a byte copy at any width.
                        Array.Copy(memory, (long)instructionPointer, memory, (long)addr, size);
                        instructionPointer += size;
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
                            var val = memory[loc];
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
                        var _ = (Register)memory[instructionPointer];
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
                case Bytecode.JE: // Jump if equal (ZF=1)
                case Bytecode.JZ: // Jump if zero (ZF=1); these two are functionally equivilent
                    if (ReadStatusRegister(general_registers[R_FLAGS], Flag.ZERO_FLAG))
                        instructionPointer = ReadMemoryUInt64(instructionPointer);
                    else
                        instructionPointer += 8;
                    break;
                case Bytecode.JNE: // Jump if not equal (ZF=0)
                case Bytecode.JNZ: // Jump if not zero (ZF=0); these two are functionally equivilent
                    if (!ReadStatusRegister(general_registers[R_FLAGS], Flag.ZERO_FLAG))
                        instructionPointer = ReadMemoryUInt64(instructionPointer);
                    else
                        instructionPointer += 8;
                    break;
                case Bytecode.JO: // Jump if overflow (OF=1)
                    if (ReadStatusRegister(general_registers[R_FLAGS], Flag.OVERFLOW_FLAG))
                        instructionPointer = ReadMemoryUInt64(instructionPointer);
                    else
                        instructionPointer += 8;
                    break;
                case Bytecode.JNO: // Jump if not overflow (OF=0)
                    if (!ReadStatusRegister(general_registers[R_FLAGS], Flag.OVERFLOW_FLAG))
                        instructionPointer = ReadMemoryUInt64(instructionPointer);
                    else
                        instructionPointer += 8;
                    break;
                case Bytecode.JS: // Jump if sign (SF=1)
                    if (ReadStatusRegister(general_registers[R_FLAGS], Flag.SIGN_FLAG))
                        instructionPointer = ReadMemoryUInt64(instructionPointer);
                    else
                        instructionPointer += 8;
                    break;
                case Bytecode.JNS: // Jump if not sign (SF=0)
                    if (!ReadStatusRegister(general_registers[R_FLAGS], Flag.SIGN_FLAG))
                        instructionPointer = ReadMemoryUInt64(instructionPointer);
                    else
                        instructionPointer += 8;
                    break;
                case Bytecode.JB: // Jump if below (CF=1)
                case Bytecode.JNAE: // Jump if not above or equal (CF=1)
                case Bytecode.JC: // Jump if carry (CF=1)
                    if (ReadStatusRegister(general_registers[R_FLAGS], Flag.CARRY_FLAG))
                        instructionPointer = ReadMemoryUInt64(instructionPointer);
                    else
                        instructionPointer += 8;
                    break;
                case Bytecode.JNB: // Jump if not below (CF=0)
                case Bytecode.JAE: // Jump if above or equal (CF=0)
                case Bytecode.JNC: // Jump if not carry (CF=0)
                    if (!ReadStatusRegister(general_registers[R_FLAGS], Flag.CARRY_FLAG))
                        instructionPointer = ReadMemoryUInt64(instructionPointer);
                    else
                        instructionPointer += 8;
                    break;
                case Bytecode.JBE: // Jump if below or equal (CF=1 or ZF=1)
                case Bytecode.JNA: // Jump if not above (CF=1 or ZF=1)
                    if (ReadStatusRegister(general_registers[R_FLAGS], Flag.CARRY_FLAG)
                        || ReadStatusRegister(general_registers[R_FLAGS], Flag.ZERO_FLAG))
                        instructionPointer = ReadMemoryUInt64(instructionPointer);
                    else
                        instructionPointer += 8;
                    break;
                case Bytecode.JA:   // Jump if above (CF=0 and ZF=0)
                case Bytecode.JNBE: // Jump if not below or equal (CF=0 and ZF=0)
                    if (!ReadStatusRegister(general_registers[R_FLAGS], Flag.CARRY_FLAG)
                        && !ReadStatusRegister(general_registers[R_FLAGS], Flag.ZERO_FLAG))
                        instructionPointer = ReadMemoryUInt64(instructionPointer);
                    else
                        instructionPointer += 8;
                    break;
                case Bytecode.JL:   // Jump if less (SF <> OF)
                case Bytecode.JNGE: // Jump if not greater or equal (SF <> OF)
                    if (ReadStatusRegister(general_registers[R_FLAGS], Flag.SIGN_FLAG)
                        != ReadStatusRegister(general_registers[R_FLAGS], Flag.OVERFLOW_FLAG))
                        instructionPointer = ReadMemoryUInt64(instructionPointer);
                    else
                        instructionPointer += 8;
                    break;
                case Bytecode.JGE: // Jump if greater or equal (SF = OF)
                case Bytecode.JNL: // Jump if not less (SF = OF)
                    if (ReadStatusRegister(general_registers[R_FLAGS], Flag.SIGN_FLAG)
                        == ReadStatusRegister(general_registers[R_FLAGS], Flag.OVERFLOW_FLAG))
                        instructionPointer = ReadMemoryUInt64(instructionPointer);
                    else
                        instructionPointer += 8;
                    break;
                case Bytecode.JLE: // Jump if less or equal (ZF=1 or SF<>OF)
                case Bytecode.JNG: // Jump if not greater (ZF=1 or SF<>OF)
                    if (ReadStatusRegister(general_registers[R_FLAGS], Flag.ZERO_FLAG)
                        || (
                            ReadStatusRegister(general_registers[R_FLAGS], Flag.SIGN_FLAG)
                            != ReadStatusRegister(general_registers[R_FLAGS], Flag.OVERFLOW_FLAG))
                        )
                        instructionPointer = ReadMemoryUInt64(instructionPointer);
                    else
                        instructionPointer += 8;
                    break;
                case Bytecode.JG:   // Jump if greater (ZF=0 and SF=OF)
                case Bytecode.JNLE: // Jump if not less or equal (ZF=0 and SF=OF)
                    if (!ReadStatusRegister(general_registers[R_FLAGS], Flag.ZERO_FLAG)
                        && (
                            ReadStatusRegister(general_registers[R_FLAGS], Flag.SIGN_FLAG)
                            == ReadStatusRegister(general_registers[R_FLAGS], Flag.OVERFLOW_FLAG))
                        )
                        instructionPointer = ReadMemoryUInt64(instructionPointer);
                    else
                        instructionPointer += 8;
                    break;
                case Bytecode.JP:  // Jump if parity (PF=1)
                case Bytecode.JPE: // Jump if parity even (PF=1)
                    if (ReadStatusRegister(general_registers[R_FLAGS], Flag.PARITY_FLAG))
                        instructionPointer = ReadMemoryUInt64(instructionPointer);
                    else
                        instructionPointer += 8;
                    break;
                case Bytecode.JNP: // Jump if not parity (PF=0)
                case Bytecode.JPO: // Jump if parity odd (PF=0)
                    if (!ReadStatusRegister(general_registers[R_FLAGS], Flag.PARITY_FLAG))
                        instructionPointer = ReadMemoryUInt64(instructionPointer);
                    else
                        instructionPointer += 8;
                    break;
                case Bytecode.JCXZ: // Jump if %CX register is 0
                    if (ReadRegister(Register.CX) == 0)
                        instructionPointer = ReadMemoryUInt64(instructionPointer);
                    else
                        instructionPointer += 8;
                    break;
                case Bytecode.JECXZ: // Jump if %ECX register is 0
                    if (ReadExtendedRegister(Register.ECX) == 0)
                        instructionPointer = ReadMemoryUInt64(instructionPointer);
                    else
                        instructionPointer += 8;
                    break;
                case Bytecode.XOR_REG_REG:
                    {
                        var dst = (Register)memory[instructionPointer];
                        instructionPointer++;
                        var src = (Register)memory[instructionPointer];
                        instructionPointer++;

                        switch (src)
                        {
                            case Register.EAX:
                            case Register.EBX:
                            case Register.ECX:
                            case Register.EDX:
                                {
                                    var operand2value = ReadExtendedRegister(src);
                                    if (dst == Register.EAX || dst == Register.EBX || dst == Register.ECX || dst == Register.EDX || dst == Register.EDI || dst == Register.ESI)
                                    {
                                        var operand1value = ReadExtendedRegister(dst);
                                        WriteExtendedRegister(dst, operand1value ^ operand2value);

                                        var result = (long)operand1value ^ operand2value;
                                        var operand1Signed = (int)operand1value; // Re-interpret as signed
                                        var operand2Signed = (int)operand2value; // Re-interpret as signed
                                        var resultSigned = operand1Signed ^ operand2Signed;

                                        WriteStatusRegister(Flag.ZERO_FLAG, resultSigned == 0);
                                        WriteStatusRegister(Flag.CARRY_FLAG, false);
                                        WriteStatusRegister(Flag.SIGN_FLAG, resultSigned < 0);
                                        WriteStatusRegister(Flag.OVERFLOW_FLAG, false);
                                        WriteStatusRegister(Flag.AUX_CARRY_FLAG, false);
                                        WriteStatusRegister(Flag.PARITY_FLAG, ByteUtility.CountBits(result & 0xFF) % 2 == 0);

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
                                    break;
                                }

                            case Register.AX:
                            case Register.BX:
                            case Register.CX:
                            case Register.DX:
                            case Register.DI:
                            case Register.SI:
                            case Register.BP:
                            case Register.IP:
                            case Register.CS:
                            case Register.DS:
                            case Register.SS:
                            case Register.ES:
                            case Register.FS:
                            case Register.GS:
                                {
                                    var operand2value = ReadRegister(src);
                                    if (dst == Register.EAX || dst == Register.EBX || dst == Register.ECX || dst == Register.EDX || dst == Register.EDI || dst == Register.ESI)
                                    {
                                        var operand1value = ReadExtendedRegister(dst);
                                        WriteExtendedRegister(dst, operand1value ^ operand2value);

                                        var result = (int)operand1value ^ operand2value;

                                        WriteStatusRegister(Flag.ZERO_FLAG, result == 0);
                                        WriteStatusRegister(Flag.CARRY_FLAG, false);
                                        WriteStatusRegister(Flag.SIGN_FLAG, result < 0);
                                        WriteStatusRegister(Flag.OVERFLOW_FLAG, false);
                                        WriteStatusRegister(Flag.AUX_CARRY_FLAG, false);
                                        WriteStatusRegister(Flag.PARITY_FLAG, ByteUtility.CountBits(result & 0xFF) % 2 == 0);

                                    }
                                    else if (
                                        dst == Register.AX || dst == Register.BX || dst == Register.CX || dst == Register.DX ||
                                        dst == Register.DI || dst == Register.SI || dst == Register.BP || dst == Register.IP ||
                                        dst == Register.CS || dst == Register.DS ||
                                        dst == Register.SS || dst == Register.ES ||
                                        dst == Register.FS || dst == Register.GS)
                                    {
                                        var operand1value = ReadRegister(dst);
                                        WriteRegister(dst, (ushort)(operand1value ^ operand2value));

                                        var result = operand1value ^ operand2value;

                                        WriteStatusRegister(Flag.ZERO_FLAG, result == 0);
                                        WriteStatusRegister(Flag.CARRY_FLAG, false);
                                        WriteStatusRegister(Flag.SIGN_FLAG, result < 0);
                                        WriteStatusRegister(Flag.OVERFLOW_FLAG, false);
                                        WriteStatusRegister(Flag.AUX_CARRY_FLAG, false);
                                        WriteStatusRegister(Flag.PARITY_FLAG, ByteUtility.CountBits(result & 0xFF) % 2 == 0);
                                    }
                                    else if (dst == Register.AH || dst == Register.AL
                                        || dst == Register.BH || dst == Register.BL
                                        || dst == Register.CH || dst == Register.CL
                                        || dst == Register.DH || dst == Register.DL)
                                        throw new InvalidOperationException("ERROR: XOR dst is a byte but source is a word");
                                    else
                                        throw new InvalidOperationException("ERROR: Unrecognized register for XOR dst");
                                    break;
                                }

                            case Register.AH:
                            case Register.AL:
                            case Register.BH:
                            case Register.BL:
                            case Register.CH:
                            case Register.CL:
                            case Register.DH:
                            case Register.DL:
                                {
                                    var operand2value = ReadHalfRegister(src);
                                    switch (dst)
                                    {
                                        case Register.EAX:
                                        case Register.EBX:
                                        case Register.ECX:
                                        case Register.EDX:
                                        case Register.EDI:
                                        case Register.ESI:
                                            {
                                                var operand1value = ReadExtendedRegister(dst);
                                                WriteExtendedRegister(dst, operand1value ^ operand2value);

                                                var result = (int)operand1value ^ operand2value; // dst's full 32 bits preserved; src (byte) zero-extends

                                                WriteStatusRegister(Flag.ZERO_FLAG, result == 0);
                                                WriteStatusRegister(Flag.CARRY_FLAG, false);
                                                WriteStatusRegister(Flag.SIGN_FLAG, result < 0);
                                                WriteStatusRegister(Flag.OVERFLOW_FLAG, false);
                                                WriteStatusRegister(Flag.AUX_CARRY_FLAG, false);
                                                WriteStatusRegister(Flag.PARITY_FLAG, ByteUtility.CountBits(result & 0xFF) % 2 == 0);
                                                break;
                                            }

                                        case Register.AX:
                                        case Register.BX:
                                        case Register.CX:
                                        case Register.DX:
                                        case Register.DI:
                                        case Register.SI:
                                        case Register.BP:
                                        case Register.IP:
                                        case Register.CS:
                                        case Register.DS:
                                        case Register.SS:
                                        case Register.ES:
                                        case Register.FS:
                                        case Register.GS:
                                            {
                                                var operand1value = ReadRegister(dst);
                                                WriteRegister(dst, (ushort)(operand1value ^ operand2value));

                                                var result = (short)operand1value ^ operand2value; // dst's full 16 bits preserved; src (byte) zero-extends

                                                WriteStatusRegister(Flag.ZERO_FLAG, result == 0);
                                                WriteStatusRegister(Flag.CARRY_FLAG, false);
                                                WriteStatusRegister(Flag.SIGN_FLAG, result < 0);
                                                WriteStatusRegister(Flag.OVERFLOW_FLAG, false);
                                                WriteStatusRegister(Flag.AUX_CARRY_FLAG, false);
                                                WriteStatusRegister(Flag.PARITY_FLAG, ByteUtility.CountBits(result & 0xFF) % 2 == 0);
                                                break;
                                            }

                                        case Register.AH:
                                        case Register.AL:
                                        case Register.BH:
                                        case Register.BL:
                                        case Register.CH:
                                        case Register.CL:
                                        case Register.DH:
                                        case Register.DL:
                                            {
                                                var operand1value = ReadHalfRegister(dst);
                                                WriteHalfRegister(dst, (byte)(operand1value ^ operand2value));

                                                var result = (sbyte)operand1value ^ (sbyte)operand2value;

                                                WriteStatusRegister(Flag.ZERO_FLAG, result == 0);
                                                WriteStatusRegister(Flag.CARRY_FLAG, false);
                                                WriteStatusRegister(Flag.SIGN_FLAG, result < 0);
                                                WriteStatusRegister(Flag.OVERFLOW_FLAG, false);
                                                WriteStatusRegister(Flag.AUX_CARRY_FLAG, false);
                                                WriteStatusRegister(Flag.PARITY_FLAG, ByteUtility.CountBits(result & 0xFF) % 2 == 0);
                                                break;
                                            }

                                        default:
                                            throw new InvalidOperationException("ERROR: Unrecognized register for XOR dst");
                                    }
                                    break;
                                }

                            default:
                                Dump();
                                throw new InvalidOperationException("ERROR: Unrecognized register for XOR src");
                        }

                        break;
                    }

                default:
                    return new TickResult(
                        TickErrorCode.UnknownBytecode,
                        true,
                        new ExecutionError(
                            $"Unknown bytecode {instruction} EIP={instructionPointer - 1}!",
                            null,
                            null,
                            null));
            }

            return new TickResult(TickErrorCode.Ok, false);
        }

        protected UInt64 ReadMemoryUInt64(ulong address)
        {
            if (address > AddressSpaceSize - 8)
                throw new MemoryAccessViolationException(address, 8, instructionPointer, isWrite: false);

            return BitConverter.ToUInt64(memory, (int)address);
        }
    }
}
