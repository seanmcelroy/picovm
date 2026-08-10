using System;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using picovm.Assembler;

namespace picovm.VM
{

    public class Agent64 : Agent
    {
        private UInt64 InstructionPointer
        {
            get => ReadR64Register(Register.RIP);
            set => WriteR64Register(Register.RIP, value);
        }

        public new UInt64 StackPointer
        {
            get => ReadR64Register(Register.SP);
            private set => WriteR64Register(Register.SP, value);
        }

        public Agent64(IKernel kernel, ReadOnlySpan<byte> program, UInt64 entryPoint) : this(kernel, program.ToArray(), entryPoint)
        {
        }

        public Agent64(IKernel kernel, byte[] program, UInt64 entryPoint) : base(kernel, program)
        {
            StackPointer = (uint)(memory.Length - 1);
            InstructionPointer = entryPoint;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        private static void WriteR64Register(ulong[] registers, Register reference, ulong value)
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
                case Register.RSP:
                    registers[R_SP] = value;
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

        private void WriteR64Register(Register reference, ulong value) => WriteR64Register(general_registers, reference, value);

        public ulong StackPop64()
        {
            var ret = ReadMemoryUInt64(ReadR64Register(Register.SP));
            StackPointer += 8;
            return ret;
        }

        public void StackPush(ulong value)
        {
            if (StackPointer < 8)
                throw new MemoryAccessViolationException(StackPointer, 8, InstructionPointer, isWrite: true);

            BinaryPrimitives.WriteUInt64LittleEndian(memory.AsSpan((int)(StackPointer - 8), 8), value);
            StackPointer -= 8;
        }

        public override void Dump()
        {
            Console.WriteLine();
            Console.Error.Write($"RAX: 0x{ReadR64Register(Register.RAX):X8} ({ReadR64Register(Register.RAX),2})\t");
            Console.Write($"RBX: 0x{ReadR64Register(Register.RBX):X8} ({ReadR64Register(Register.RBX),2})\t");
            Console.Write($"RCX: 0x{ReadR64Register(Register.RCX):X8} ({ReadR64Register(Register.RCX),2})\t");
            Console.WriteLine($"RDX: 0x{ReadR64Register(Register.RDX):X8} ({ReadR64Register(Register.RDX),2})");
            Console.WriteLine($"RIP: 0x{InstructionPointer:X8} ({InstructionPointer})\tRSP: 0x{StackPointer:X8} ({StackPointer,2})");
            Console.WriteLine($"RSI: 0x{ReadR64Register(Register.RSI):X8} ({ReadR64Register(Register.RSI),2})\tRDI: 0x{ReadR64Register(Register.RDI):X8} ({ReadR64Register(Register.RDI),2})");
            Console.WriteLine($"RBP: 0x{ReadR64Register(Register.RBP):X8} ({ReadR64Register(Register.RBP),2})");
            Console.WriteLine($"R8 : 0x{ReadR64Register(Register.R8):X8} ({ReadR64Register(Register.R8),2})\tR9 : 0x{ReadR64Register(Register.R9):X8} ({ReadR64Register(Register.R9),2})");
            Console.WriteLine($"R10: 0x{ReadR64Register(Register.R10):X8} ({ReadR64Register(Register.R10),2})\tR11: 0x{ReadR64Register(Register.R11):X8} ({ReadR64Register(Register.R11),2})");
            Console.WriteLine($"R12: 0x{ReadR64Register(Register.R12):X8} ({ReadR64Register(Register.R12),2})\tR13: 0x{ReadR64Register(Register.R13):X8} ({ReadR64Register(Register.R13),2})");
            Console.WriteLine($"R14: 0x{ReadR64Register(Register.R14):X8} ({ReadR64Register(Register.R14),2})\tR15: 0x{ReadR64Register(Register.R15):X8} ({ReadR64Register(Register.R15),2})");
            Console.WriteLine("(Stack)");
            var i = (ulong)memory.Length;
            var qword = new byte[8];
            do
            {
                Array.Copy(memory, (int)i - 8, qword, 0, 8);
                Console.WriteLine($"{i}\t: {Convert.ToHexStringLower(qword)}");
                i -= 8;
            } while (i > StackPointer);
            Console.WriteLine("...");
            i = InstructionPointer + (8 - InstructionPointer % 8);
            do
            {
                Array.Copy(memory, (uint)i - 8, qword, 0, 8);
                Console.WriteLine($"{i}\t: {Convert.ToHexStringLower(qword)}");
                i -= 8;
            } while (i > 0);
        }

        public override TickResult Tick()
        {
            var instruction = (Bytecode)memory[InstructionPointer];
            InstructionPointer++;

            switch (instruction)
            {
                case Bytecode.END:
                    return new TickResult(TickErrorCode.Ok, true);
                case Bytecode.ADD_REG_CON:
                    {
                        var operand1 = (Register)memory[InstructionPointer];
                        InstructionPointer++;

                        switch (operand1.Size())
                        {
                            case 8:
                                {
                                    var operand1value = ReadR64Register(operand1);
                                    var operand2value = ReadMemoryUInt64(InstructionPointer);
                                    InstructionPointer += 8;
                                    WriteR64Register(operand1, operand1value + operand2value);

                                    var result = (BigInteger)operand1value + operand2value;
                                    var operand1Signed = (long)operand1value; // Re-interpret as signed
                                    var operand2Signed = (long)operand2value; // Re-interpret as signed
                                    var resultSigned = operand1Signed + operand2Signed;

                                    WriteArithmeticFlags(
                                        result > ulong.MaxValue, // CARRY_FLAG
                                        ByteUtility.CountBits((ulong)(result & 0xFF)) % 2 == 0, // PARITY_FLAG
                                        (operand1value & 0xF) + (operand2value & 0xF) > 0xF, // AUX_CARRY_FLAG
                                        resultSigned == 0, // ZERO_FLAG
                                        resultSigned < 0, // SIGN_FLAG
                                        ((operand1Signed ^ operand2Signed) & (operand1Signed ^ resultSigned)) < 0 // OVERFLOW_FLAG
                                    );
                                    break;
                                }
                            case 4:
                                {
                                    var operand1value = ReadExtendedRegister(operand1);
                                    var operand2value = ReadMemoryUInt32(InstructionPointer);
                                    InstructionPointer += 4;
                                    WriteExtendedRegister(operand1, operand1value + operand2value);

                                    var result = (long)operand1value + operand2value;
                                    var operand1Signed = (int)operand1value; // Re-interpret as signed
                                    var operand2Signed = (int)operand2value; // Re-interpret as signed
                                    var resultSigned = operand1Signed + operand2Signed;

                                    WriteArithmeticFlags(
                                        result > uint.MaxValue, // CARRY_FLAG
                                        ByteUtility.CountBits(result & 0xFF) % 2 == 0, // PARITY_FLAG
                                        (operand1value & 0xF) + (operand2value & 0xF) > 0xF, // AUX_CARRY_FLAG
                                        resultSigned == 0, // ZERO_FLAG
                                        resultSigned < 0, // SIGN_FLAG
                                        ((operand1Signed ^ operand2Signed) & (operand1Signed ^ resultSigned)) < 0 // OVERFLOW_FLAG
                                    );
                                    break;
                                }
                            case 2:
                                {
                                    var operand1value = ReadRegister(operand1);
                                    var operand2value = ReadMemoryUInt16(InstructionPointer);
                                    InstructionPointer += 2;
                                    WriteRegister(operand1, (ushort)(operand1value + operand2value));

                                    var result = (uint)operand1value + operand2value;
                                    var operand1Signed = (short)operand1value; // Re-interpret as signed
                                    var operand2Signed = (short)operand2value; // Re-interpret as signed
                                    var resultSigned = (short)(operand1Signed + operand2Signed);

                                    WriteArithmeticFlags(
                                        result > ushort.MaxValue, // CARRY_FLAG
                                        ByteUtility.CountBits(result & 0xFF) % 2 == 0, // PARITY_FLAG
                                        (operand1value & 0xF) + (operand2value & 0xF) > 0xF, // AUX_CARRY_FLAG
                                        resultSigned == 0, // ZERO_FLAG
                                        resultSigned < 0, // SIGN_FLAG
                                        ((operand1Signed ^ operand2Signed) & (operand1Signed ^ resultSigned)) < 0 // OVERFLOW_FLAG
                                    );
                                    break;
                                }
                            case 1:
                                {
                                    var operand1value = ReadHalfRegister(operand1);
                                    var operand2value = memory[(int)InstructionPointer];
                                    InstructionPointer++;
                                    WriteHalfRegister(operand1, (byte)(operand1value + operand2value));

                                    var result = operand1value + operand2value;
                                    var operand1Signed = (sbyte)operand1value; // Re-interpret as signed
                                    var operand2Signed = (sbyte)operand2value; // Re-interpret as signed
                                    var resultSigned = (sbyte)(operand1Signed + operand2Signed);

                                    WriteArithmeticFlags(
                                        result > byte.MaxValue, // CARRY_FLAG
                                        ByteUtility.CountBits(result & 0xFF) % 2 == 0, // PARITY_FLAG
                                        (operand1value & 0xF) + (operand2value & 0xF) > 0xF, // AUX_CARRY_FLAG
                                        resultSigned == 0, // ZERO_FLAG
                                        resultSigned < 0, // SIGN_FLAG
                                        ((operand1Signed ^ operand2Signed) & (operand1Signed ^ resultSigned)) < 0 // OVERFLOW_FLAG
                                    );
                                    break;
                                }
                            default:
                                throw new InvalidOperationException($"ERROR: ADD cannot handle the type of register targeted: {operand1}");
                        }
                        break;
                    }
                case Bytecode.ADD_MEM_CON:
                    {
                        var operand1 = (Register)memory[InstructionPointer];
                        InstructionPointer++;

                        switch (operand1.Size())
                        {
                            case 8:
                                {
                                    var loc = ReadR64Register(operand1);
                                    var operand1value = ReadMemoryUInt64(loc);
                                    var operand2value = ReadMemoryUInt64(InstructionPointer);
                                    InstructionPointer += 8;
                                    BinaryPrimitives.WriteUInt64LittleEndian(memory.AsSpan((int)loc, 8), operand1value + operand2value);

                                    var result = (BigInteger)operand1value + operand2value;
                                    var operand1Signed = (long)operand1value; // Re-interpret as signed
                                    var operand2Signed = (long)operand2value; // Re-interpret as signed
                                    var resultSigned = operand1Signed + operand2Signed;

                                    WriteArithmeticFlags(
                                        result > ulong.MaxValue, // CARRY_FLAG
                                        ByteUtility.CountBits((ulong)(result & 0xFF)) % 2 == 0, // PARITY_FLAG
                                        (operand1value & 0xF) + (operand2value & 0xF) > 0xF, // AUX_CARRY_FLAG
                                        resultSigned == 0, // ZERO_FLAG
                                        resultSigned < 0, // SIGN_FLAG
                                        ((operand1Signed ^ operand2Signed) & (operand1Signed ^ resultSigned)) < 0 // OVERFLOW_FLAG
                                    );
                                    break;
                                }
                            case 4:
                                {
                                    var loc = ReadExtendedRegister(operand1);
                                    var operand1value = ReadMemoryUInt32(loc);
                                    var operand2value = ReadMemoryUInt32(InstructionPointer);
                                    InstructionPointer += 4;
                                    BinaryPrimitives.WriteUInt32LittleEndian(memory.AsSpan((int)loc, 4), operand1value + operand2value);

                                    var result = (long)operand1value + operand2value;
                                    var operand1Signed = (int)operand1value; // Re-interpret as signed
                                    var operand2Signed = (int)operand2value; // Re-interpret as signed
                                    var resultSigned = operand1Signed + operand2Signed;

                                    WriteArithmeticFlags(
                                        result > uint.MaxValue, // CARRY_FLAG
                                        ByteUtility.CountBits(result & 0xFF) % 2 == 0, // PARITY_FLAG
                                        (operand1value & 0xF) + (operand2value & 0xF) > 0xF, // AUX_CARRY_FLAG
                                        resultSigned == 0, // ZERO_FLAG
                                        resultSigned < 0, // SIGN_FLAG
                                        ((operand1Signed ^ operand2Signed) & (operand1Signed ^ resultSigned)) < 0 // OVERFLOW_FLAG
                                    );
                                    break;
                                }
                            case 2:
                                {
                                    var loc = ReadRegister(operand1);
                                    var operand1value = ReadMemoryUInt16(loc);
                                    var operand2value = ReadMemoryUInt16(InstructionPointer);
                                    InstructionPointer += 2;
                                    BinaryPrimitives.WriteUInt16LittleEndian(memory.AsSpan(loc, 2), (ushort)(operand1value + operand2value));

                                    var result = (uint)operand1value + operand2value;
                                    var operand1Signed = (short)operand1value; // Re-interpret as signed
                                    var operand2Signed = (short)operand2value; // Re-interpret as signed
                                    var resultSigned = (short)(operand1Signed + operand2Signed);

                                    WriteArithmeticFlags(
                                        result > ushort.MaxValue, // CARRY_FLAG
                                        ByteUtility.CountBits(result & 0xFF) % 2 == 0, // PARITY_FLAG
                                        (operand1value & 0xF) + (operand2value & 0xF) > 0xF, // AUX_CARRY_FLAG
                                        resultSigned == 0, // ZERO_FLAG
                                        resultSigned < 0, // SIGN_FLAG
                                        ((operand1Signed ^ operand2Signed) & (operand1Signed ^ resultSigned)) < 0 // OVERFLOW_FLAG
                                    );
                                    break;
                                }
                            case 1:
                                {
                                    var loc = ReadHalfRegister(operand1);
                                    var operand1value = ReadMemoryByte(loc);
                                    var operand2value = ReadMemoryByte(InstructionPointer);
                                    InstructionPointer += 1;
                                    memory[loc] = (byte)(operand1value + operand2value);

                                    var result = operand1value + operand2value;
                                    var operand1Signed = (sbyte)operand1value; // Re-interpret as signed
                                    var operand2Signed = (sbyte)operand2value; // Re-interpret as signed
                                    var resultSigned = (sbyte)(operand1Signed + operand2Signed);

                                    WriteArithmeticFlags(
                                        result > byte.MaxValue, // CARRY_FLAG
                                        ByteUtility.CountBits(result & 0xFF) % 2 == 0, // PARITY_FLAG
                                        (operand1value & 0xF) + (operand2value & 0xF) > 0xF, // AUX_CARRY_FLAG
                                        resultSigned == 0, // ZERO_FLAG
                                        resultSigned < 0, // SIGN_FLAG
                                        ((operand1Signed ^ operand2Signed) & (operand1Signed ^ resultSigned)) < 0 // OVERFLOW_FLAG
                                    );
                                    break;
                                }
                            default:
                                throw new InvalidOperationException($"ERROR: ADD cannot handle the type of register targeted: {operand1}");
                        }
                        break;
                    }
                case Bytecode.AND_REG_CON:
                    {
                        var operand1 = (Register)memory[InstructionPointer];
                        InstructionPointer++;

                        switch (operand1.Size())
                        {
                            case 8:
                                {
                                    var operand1value = ReadR64Register(operand1);
                                    var operand2value = ReadMemoryUInt64(InstructionPointer);
                                    InstructionPointer += 8;
                                    var val = operand1value & operand2value;
                                    WriteR64Register(operand1, val);

                                    var result = operand1value & operand2value;
                                    var operand1Signed = (long)operand1value; // Re-interpret as signed
                                    var operand2Signed = (long)operand2value; // Re-interpret as signed
                                    var resultSigned = operand1Signed & operand2Signed;

                                    WriteLogicFlags(
                                        ByteUtility.CountBits(result & 0xFF) % 2 == 0, // PARITY_FLAG
                                        resultSigned == 0, // ZERO_FLAG
                                        resultSigned < 0 // SIGN_FLAG
                                    );
                                    break;
                                }
                            case 4:
                                {
                                    var operand1value = ReadExtendedRegister(operand1);
                                    var operand2value = ReadMemoryUInt32(InstructionPointer);
                                    InstructionPointer += 4;
                                    var val = operand1value & operand2value;
                                    WriteExtendedRegister(operand1, val);

                                    var result = (long)operand1value & operand2value;
                                    var operand1Signed = (int)operand1value; // Re-interpret as signed
                                    var operand2Signed = (int)operand2value; // Re-interpret as signed
                                    var resultSigned = operand1Signed & operand2Signed;

                                    WriteLogicFlags(
                                        ByteUtility.CountBits(result & 0xFF) % 2 == 0, // PARITY_FLAG
                                        resultSigned == 0, // ZERO_FLAG
                                        resultSigned < 0 // SIGN_FLAG
                                    );
                                    break;
                                }
                            case 2:
                                {
                                    var operand1value = ReadRegister(operand1);
                                    var operand2value = ReadMemoryUInt16(InstructionPointer);
                                    InstructionPointer += 2;
                                    var val = (ushort)(operand1value & operand2value);
                                    WriteRegister(operand1, val);

                                    var result = (uint)operand1value & operand2value;
                                    var operand1Signed = (short)operand1value; // Re-interpret as signed
                                    var operand2Signed = (short)operand2value; // Re-interpret as signed
                                    var resultSigned = (short)(operand1Signed & operand2Signed);

                                    WriteLogicFlags(
                                        ByteUtility.CountBits(result & 0xFF) % 2 == 0, // PARITY_FLAG
                                        resultSigned == 0, // ZERO_FLAG
                                        resultSigned < 0 // SIGN_FLAG
                                    );
                                    break;
                                }
                            case 1:
                                {
                                    var operand1value = ReadHalfRegister(operand1);
                                    var operand2value = ReadMemoryByte(InstructionPointer);
                                    InstructionPointer++;
                                    var val = (byte)(operand1value & operand2value);
                                    WriteHalfRegister(operand1, val);

                                    var result = operand1value & operand2value;
                                    var operand1Signed = (sbyte)operand1value; // Re-interpret as signed
                                    var operand2Signed = (sbyte)operand2value; // Re-interpret as signed
                                    var resultSigned = (sbyte)(operand1Signed & operand2Signed);

                                    WriteLogicFlags(
                                        ByteUtility.CountBits(result & 0xFF) % 2 == 0, // PARITY_FLAG
                                        resultSigned == 0, // ZERO_FLAG
                                        resultSigned < 0 // SIGN_FLAG
                                    );
                                    break;
                                }
                            default:
                                throw new InvalidOperationException($"ERROR: AND cannot handle the type of register targeted: {operand1}");
                        }
                        break;
                    }
                case Bytecode.CMP_REG_CON:
                    {
                        var operand1 = (Register)memory[InstructionPointer];

                        switch (operand1.Size())
                        {
                            case 8:
                                {
                                    // For example: CMP RAX, imm64
                                    var operand1value = ReadR64Register(operand1);
                                    InstructionPointer++;
                                    var operand2value = ReadMemoryUInt64(InstructionPointer);
                                    InstructionPointer += 8;

                                    var result = operand1value - operand2value;
                                    var operand1Signed = (long)operand1value; // Re-interpret as signed
                                    var operand2Signed = (long)operand2value; // Re-interpret as signed
                                    var resultSigned = operand1Signed - operand2Signed;

                                    WriteArithmeticFlags(
                                        operand1value < operand2value, // CARRY_FLAG
                                        ByteUtility.CountBits(result & 0xFF) % 2 == 0, // PARITY_FLAG
                                        (operand1value & 0xF) < (operand2value & 0xF), // AUX_CARRY_FLAG
                                        resultSigned == 0, // ZERO_FLAG
                                        resultSigned < 0, // SIGN_FLAG
                                        ((operand1Signed ^ operand2Signed) & (operand1Signed ^ resultSigned)) < 0 // OVERFLOW_FLAG
                                    );
                                    break;
                                }
                            case 4:
                                {
                                    // For example: CMP EAX, imm32
                                    var operand1value = ReadExtendedRegister(operand1);
                                    InstructionPointer++;
                                    var operand2value = ReadMemoryUInt32(InstructionPointer);
                                    InstructionPointer += 4;

                                    var result = (long)operand1value - operand2value;
                                    var operand1Signed = (int)operand1value; // Re-interpret as signed
                                    var operand2Signed = (int)operand2value; // Re-interpret as signed
                                    var resultSigned = operand1Signed - operand2Signed;

                                    WriteArithmeticFlags(
                                        operand1value < operand2value, // CARRY_FLAG
                                        ByteUtility.CountBits(result & 0xFF) % 2 == 0, // PARITY_FLAG
                                        (operand1value & 0xF) < (operand2value & 0xF), // AUX_CARRY_FLAG
                                        resultSigned == 0, // ZERO_FLAG
                                        resultSigned < 0, // SIGN_FLAG
                                        ((operand1Signed ^ operand2Signed) & (operand1Signed ^ resultSigned)) < 0 // OVERFLOW_FLAG
                                    );
                                    break;
                                }
                            case 2:
                                {
                                    // For example: CMP AX, imm16
                                    var operand1value = ReadRegister(operand1);
                                    InstructionPointer++;
                                    var operand2value = ReadMemoryUInt16(InstructionPointer);
                                    InstructionPointer += 2;

                                    var result = (int)operand1value - operand2value;
                                    var operand1Signed = (short)operand1value; // Re-interpret as signed
                                    var operand2Signed = (short)operand2value; // Re-interpret as signed
                                    var resultSigned = (short)(operand1Signed - operand2Signed);

                                    WriteArithmeticFlags(
                                        operand1value < operand2value, // CARRY_FLAG
                                        ByteUtility.CountBits(result & 0xFF) % 2 == 0, // PARITY_FLAG
                                        (operand1value & 0xF) < (operand2value & 0xF), // AUX_CARRY_FLAG
                                        resultSigned == 0, // ZERO_FLAG
                                        resultSigned < 0, // SIGN_FLAG
                                        ((operand1Signed ^ operand2Signed) & (operand1Signed ^ resultSigned)) < 0 // OVERFLOW_FLAG
                                    );
                                    break;
                                }
                            case 1:
                                {
                                    // For example: CMP AL, imm8
                                    var operand1value = ReadHalfRegister(operand1);
                                    InstructionPointer++;
                                    var operand2value = memory[(int)InstructionPointer];
                                    InstructionPointer++;

                                    var result = (short)operand1value - operand2value;
                                    var operand1Signed = (sbyte)operand1value; // Re-interpret as signed
                                    var operand2Signed = (sbyte)operand2value; // Re-interpret as signed
                                    var resultSigned = (sbyte)(operand1Signed - operand2Signed);

                                    WriteArithmeticFlags(
                                        operand1value < operand2value, // CARRY_FLAG
                                        ByteUtility.CountBits(result & 0xFF) % 2 == 0, // PARITY_FLAG
                                        (operand1value & 0xF) < (operand2value & 0xF), // AUX_CARRY_FLAG
                                        resultSigned == 0, // ZERO_FLAG
                                        resultSigned < 0, // SIGN_FLAG
                                        ((operand1Signed ^ operand2Signed) & (operand1Signed ^ resultSigned)) < 0 // OVERFLOW_FLAG
                                    );
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
                        var interruptVector = memory[(int)InstructionPointer];
                        InstructionPointer++;

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
                        var dst = (Register)memory[InstructionPointer];
                        InstructionPointer++;
                        var src = (Register)memory[InstructionPointer];
                        InstructionPointer++;

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
                                throw new InvalidOperationException("ERROR: Unrecognized register for MOV src");
                        }
                        break;
                    }
                case Bytecode.MOV_IMMEDIATE:
                    {
                        var dst = (Register)memory[InstructionPointer];
                        InstructionPointer++;

                        switch (dst.Size())
                        {
                            case 8:
                                {
                                    var val = ReadMemoryUInt64(InstructionPointer);
                                    InstructionPointer += 8;
                                    WriteR64Register(dst, val);
                                    break;
                                }
                            case 4:
                                {
                                    var val = ReadMemoryUInt32(InstructionPointer);
                                    InstructionPointer += 4;
                                    WriteExtendedRegister(dst, val);
                                    break;
                                }
                            case 2:
                                {
                                    var val = ReadMemoryUInt16(InstructionPointer);
                                    InstructionPointer += 2;
                                    WriteRegister(dst, val);
                                    break;
                                }
                            case 1:
                                {
                                    var val = ReadMemoryByte(InstructionPointer);
                                    InstructionPointer++;
                                    WriteHalfRegister(dst, val);
                                    break;
                                }
                            default:
                                throw new InvalidOperationException($"ERROR: Unrecognized register for MOV dst: {dst}");
                        }
                        break;
                    }
                case Bytecode.MOV_INDIRECT:
                    {
                        var dst = (Register)memory[InstructionPointer];
                        InstructionPointer++;
                        var src = (Register)memory[InstructionPointer];
                        InstructionPointer++;

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
                                WriteR64Register(dst, ReadMemoryUInt64(addr));
                                break;
                            case 4:
                                WriteExtendedRegister(dst, ReadMemoryUInt32(addr));
                                break;
                            case 2:
                                WriteRegister(dst, ReadMemoryUInt16(addr));
                                break;
                            case 1:
                                WriteHalfRegister(dst, ReadMemoryByte(addr));
                                break;
                            default:
                                Dump();
                                throw new InvalidOperationException($"ERROR: Unrecognized register for MOV dst: {dst}");
                        }
                        break;
                    }
                case Bytecode.MOV_DIRECT: // aka MOV [counter], 65 ; Formerly MOV_MEM_CON
                    {
                        // Absolute destination address, always machine width, patched at link time.
                        var addr = ReadMemoryUInt64(InstructionPointer);
                        InstructionPointer += 8;

                        // Width of the store.  Unlike every other MOV, no operand implies it: the
                        // destination is a bare address, so the assembler states it explicitly.
                        var size = ReadMemoryByte(InstructionPointer);
                        InstructionPointer++;

                        if (size != 1 && size != 2 && size != 4 && size != 8)
                        {
                            Dump();
                            throw new InvalidOperationException($"ERROR: Unsupported operand size for MOV: {size}");
                        }

                        // The immediate is already little-endian in the instruction stream and the
                        // destination is raw memory, so the store is a byte copy at any width.
                        Array.Copy(memory, (long)InstructionPointer, memory, (long)addr, size);
                        InstructionPointer += size;
                        break;
                    }
                case Bytecode.CALL_REGISTER:
                    {
                        var operand = (Register)ReadMemoryByte(InstructionPointer);
                        InstructionPointer++;

                        if (operand.Size() != 8)
                            throw new InvalidOperationException("ERROR: CALL needs a 64-bit register to read an 8-byte address");

                        var loc = ReadR64Register(operand);

                        // Push RIP onto the stack, which will be the offset of the instruction following the call.
                        StackPush(InstructionPointer);
                        InstructionPointer = loc;
                        break;
                    }
                case Bytecode.CALL_IMMEDIATE:
                    {
                        var returnAddress = InstructionPointer + 8;
                        var loc = ReadMemoryUInt64(InstructionPointer);
                        StackPush(returnAddress);
                        InstructionPointer = loc;
                        break;
                    }
                case Bytecode.RET:
                    InstructionPointer = StackPop64();
                    break;
                case Bytecode.POP_REG:
                    {
                        var operand = (Register)memory[InstructionPointer];
                        InstructionPointer++;

                        switch (operand.Size())
                        {
                            case 8:
                                WriteR64Register(operand, StackPop64());
                                break;
                            case 4:
                                WriteExtendedRegister(operand, StackPop32());
                                break;
                            case 2:
                                WriteRegister(operand, StackPop16());
                                break;
                            case 1:
                                WriteHalfRegister(operand, StackPop8());
                                break;
                            default:
                                throw new InvalidOperationException("ERROR: Unrecognized register for POP");
                        }

                        break;
                    }
                case Bytecode.POP_MEM:
                    {
                        var operand = (Register)memory[InstructionPointer];
                        InstructionPointer++;

                        switch (operand.Size())
                        {
                            case 8:
                                BinaryPrimitives.WriteUInt64LittleEndian(memory.AsSpan((int)ReadR64Register(operand), 8), StackPop64());
                                break;
                            case 4:
                                BinaryPrimitives.WriteUInt32LittleEndian(memory.AsSpan((int)ReadExtendedRegister(operand), 4), StackPop32());
                                break;
                            case 2:
                                BinaryPrimitives.WriteUInt16LittleEndian(memory.AsSpan(ReadRegister(operand), 2), StackPop16());
                                break;
                            case 1:
                                memory[ReadHalfRegister(operand)] = StackPop8();
                                break;
                            default:
                                throw new InvalidOperationException("ERROR: Unrecognized register for POP");
                        }
                        break;
                    }
                case Bytecode.PUSH_REG:
                    {
                        var operand = (Register)memory[InstructionPointer];
                        InstructionPointer++;
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
                        var operand = (Register)memory[InstructionPointer];
                        InstructionPointer++;

                        switch (operand.Size())
                        {
                            case 8:
                                StackPush(BinaryPrimitives.ReadUInt64LittleEndian(memory.AsSpan((int)ReadR64Register(operand), 8)));
                                break;
                            case 4:
                                StackPush(BinaryPrimitives.ReadUInt32LittleEndian(memory.AsSpan((int)ReadExtendedRegister(operand), 4)));
                                break;
                            case 2:
                                StackPush(BinaryPrimitives.ReadUInt16LittleEndian(memory.AsSpan(ReadRegister(operand), 2)));
                                break;
                            case 1:
                                StackPush(memory[ReadHalfRegister(operand)]);
                                break;
                            default:
                                Dump();
                                throw new InvalidOperationException("ERROR: Unrecognized register for PUSH");
                        }

                        break;
                    }
                case Bytecode.PUSH_CON:
                    {
                        var _ = (Register)memory[InstructionPointer];
                        var val = BinaryPrimitives.ReadUInt64LittleEndian(memory.AsSpan((int)InstructionPointer, 8));
                        InstructionPointer += 8;
                        StackPush(val);

                        break;
                    }
                case Bytecode.JMP:
                    {
                        var loc = BinaryPrimitives.ReadUInt64LittleEndian(memory.AsSpan((int)InstructionPointer, 8));
                        InstructionPointer = loc;
                        break;
                    }
                case Bytecode.JE: // Jump if equal (ZF=1)
                case Bytecode.JZ: // Jump if zero (ZF=1); these two are functionally equivilent
                    if (ReadStatusRegister(general_registers[R_FLAGS], Flag.ZERO_FLAG))
                        InstructionPointer = ReadMemoryUInt64(InstructionPointer);
                    else
                        InstructionPointer += 8;
                    break;
                case Bytecode.JNE: // Jump if not equal (ZF=0)
                case Bytecode.JNZ: // Jump if not zero (ZF=0); these two are functionally equivilent
                    if (!ReadStatusRegister(general_registers[R_FLAGS], Flag.ZERO_FLAG))
                        InstructionPointer = ReadMemoryUInt64(InstructionPointer);
                    else
                        InstructionPointer += 8;
                    break;
                case Bytecode.JO: // Jump if overflow (OF=1)
                    if (ReadStatusRegister(general_registers[R_FLAGS], Flag.OVERFLOW_FLAG))
                        InstructionPointer = ReadMemoryUInt64(InstructionPointer);
                    else
                        InstructionPointer += 8;
                    break;
                case Bytecode.JNO: // Jump if not overflow (OF=0)
                    if (!ReadStatusRegister(general_registers[R_FLAGS], Flag.OVERFLOW_FLAG))
                        InstructionPointer = ReadMemoryUInt64(InstructionPointer);
                    else
                        InstructionPointer += 8;
                    break;
                case Bytecode.JS: // Jump if sign (SF=1)
                    if (ReadStatusRegister(general_registers[R_FLAGS], Flag.SIGN_FLAG))
                        InstructionPointer = ReadMemoryUInt64(InstructionPointer);
                    else
                        InstructionPointer += 8;
                    break;
                case Bytecode.JNS: // Jump if not sign (SF=0)
                    if (!ReadStatusRegister(general_registers[R_FLAGS], Flag.SIGN_FLAG))
                        InstructionPointer = ReadMemoryUInt64(InstructionPointer);
                    else
                        InstructionPointer += 8;
                    break;
                case Bytecode.JB: // Jump if below (CF=1)
                case Bytecode.JNAE: // Jump if not above or equal (CF=1)
                case Bytecode.JC: // Jump if carry (CF=1)
                    if (ReadStatusRegister(general_registers[R_FLAGS], Flag.CARRY_FLAG))
                        InstructionPointer = ReadMemoryUInt64(InstructionPointer);
                    else
                        InstructionPointer += 8;
                    break;
                case Bytecode.JNB: // Jump if not below (CF=0)
                case Bytecode.JAE: // Jump if above or equal (CF=0)
                case Bytecode.JNC: // Jump if not carry (CF=0)
                    if (!ReadStatusRegister(general_registers[R_FLAGS], Flag.CARRY_FLAG))
                        InstructionPointer = ReadMemoryUInt64(InstructionPointer);
                    else
                        InstructionPointer += 8;
                    break;
                case Bytecode.JBE: // Jump if below or equal (CF=1 or ZF=1)
                case Bytecode.JNA: // Jump if not above (CF=1 or ZF=1)
                    if (ReadStatusRegister(general_registers[R_FLAGS], Flag.CARRY_FLAG)
                        || ReadStatusRegister(general_registers[R_FLAGS], Flag.ZERO_FLAG))
                        InstructionPointer = ReadMemoryUInt64(InstructionPointer);
                    else
                        InstructionPointer += 8;
                    break;
                case Bytecode.JA:   // Jump if above (CF=0 and ZF=0)
                case Bytecode.JNBE: // Jump if not below or equal (CF=0 and ZF=0)
                    if (!ReadStatusRegister(general_registers[R_FLAGS], Flag.CARRY_FLAG)
                        && !ReadStatusRegister(general_registers[R_FLAGS], Flag.ZERO_FLAG))
                        InstructionPointer = ReadMemoryUInt64(InstructionPointer);
                    else
                        InstructionPointer += 8;
                    break;
                case Bytecode.JL:   // Jump if less (SF <> OF)
                case Bytecode.JNGE: // Jump if not greater or equal (SF <> OF)
                    if (ReadStatusRegister(general_registers[R_FLAGS], Flag.SIGN_FLAG)
                        != ReadStatusRegister(general_registers[R_FLAGS], Flag.OVERFLOW_FLAG))
                        InstructionPointer = ReadMemoryUInt64(InstructionPointer);
                    else
                        InstructionPointer += 8;
                    break;
                case Bytecode.JGE: // Jump if greater or equal (SF = OF)
                case Bytecode.JNL: // Jump if not less (SF = OF)
                    if (ReadStatusRegister(general_registers[R_FLAGS], Flag.SIGN_FLAG)
                        == ReadStatusRegister(general_registers[R_FLAGS], Flag.OVERFLOW_FLAG))
                        InstructionPointer = ReadMemoryUInt64(InstructionPointer);
                    else
                        InstructionPointer += 8;
                    break;
                case Bytecode.JLE: // Jump if less or equal (ZF=1 or SF<>OF)
                case Bytecode.JNG: // Jump if not greater (ZF=1 or SF<>OF)
                    if (ReadStatusRegister(general_registers[R_FLAGS], Flag.ZERO_FLAG)
                        || (
                            ReadStatusRegister(general_registers[R_FLAGS], Flag.SIGN_FLAG)
                            != ReadStatusRegister(general_registers[R_FLAGS], Flag.OVERFLOW_FLAG))
                        )
                        InstructionPointer = ReadMemoryUInt64(InstructionPointer);
                    else
                        InstructionPointer += 8;
                    break;
                case Bytecode.JG:   // Jump if greater (ZF=0 and SF=OF)
                case Bytecode.JNLE: // Jump if not less or equal (ZF=0 and SF=OF)
                    if (!ReadStatusRegister(general_registers[R_FLAGS], Flag.ZERO_FLAG)
                        && (
                            ReadStatusRegister(general_registers[R_FLAGS], Flag.SIGN_FLAG)
                            == ReadStatusRegister(general_registers[R_FLAGS], Flag.OVERFLOW_FLAG))
                        )
                        InstructionPointer = ReadMemoryUInt64(InstructionPointer);
                    else
                        InstructionPointer += 8;
                    break;
                case Bytecode.JP:  // Jump if parity (PF=1)
                case Bytecode.JPE: // Jump if parity even (PF=1)
                    if (ReadStatusRegister(general_registers[R_FLAGS], Flag.PARITY_FLAG))
                        InstructionPointer = ReadMemoryUInt64(InstructionPointer);
                    else
                        InstructionPointer += 8;
                    break;
                case Bytecode.JNP: // Jump if not parity (PF=0)
                case Bytecode.JPO: // Jump if parity odd (PF=0)
                    if (!ReadStatusRegister(general_registers[R_FLAGS], Flag.PARITY_FLAG))
                        InstructionPointer = ReadMemoryUInt64(InstructionPointer);
                    else
                        InstructionPointer += 8;
                    break;
                case Bytecode.JCXZ: // Jump if %CX register is 0
                    if (ReadRegister(Register.CX) == 0)
                        InstructionPointer = ReadMemoryUInt64(InstructionPointer);
                    else
                        InstructionPointer += 8;
                    break;
                case Bytecode.JECXZ: // Jump if %ECX register is 0
                    if (ReadExtendedRegister(Register.ECX) == 0)
                        InstructionPointer = ReadMemoryUInt64(InstructionPointer);
                    else
                        InstructionPointer += 8;
                    break;
                case Bytecode.XOR_REG_REG:
                    {
                        var dst = (Register)memory[InstructionPointer];
                        InstructionPointer++;
                        var src = (Register)memory[InstructionPointer];
                        InstructionPointer++;

                        switch (src.Size())
                        {
                            case 8:
                                {
                                    var operand2value = ReadR64Register(src);
                                    switch (dst.Size())
                                    {
                                        case 8:
                                            var operand1value = ReadR64Register(dst);
                                            WriteR64Register(dst, operand1value ^ operand2value);

                                            var result = operand1value ^ operand2value;
                                            var operand1Signed = (long)operand1value; // Re-interpret as signed
                                            var operand2Signed = (long)operand2value; // Re-interpret as signed
                                            var resultSigned = operand1Signed ^ operand2Signed;

                                            WriteLogicFlags(
                                                ByteUtility.CountBits(result & 0xFF) % 2 == 0, // PARITY_FLAG
                                                resultSigned == 0, // ZERO_FLAG
                                                resultSigned < 0 // SIGN_FLAG
                                            );
                                            break;
                                        case 4:
                                            throw new InvalidOperationException("ERROR: XOR dst is a dword but source is a qword");
                                        case 2:
                                            throw new InvalidOperationException("ERROR: XOR dst is a word but source is a qword");
                                        case 1:
                                            throw new InvalidOperationException("ERROR: XOR dst is a byte but source is a qword");
                                        default:
                                            throw new InvalidOperationException("ERROR: Unrecognized register for XOR dst");
                                    }
                                    break;
                                }

                            case 4:
                                {
                                    var operand2value = ReadExtendedRegister(src);
                                    switch (dst.Size())
                                    {
                                        case 8:
                                            throw new InvalidOperationException("ERROR: XOR dst is a qword but source is a dword");
                                        case 4:
                                            var operand1value = ReadExtendedRegister(dst);
                                            WriteExtendedRegister(dst, operand1value ^ operand2value);

                                            var result = (long)operand1value ^ operand2value;
                                            var operand1Signed = (int)operand1value; // Re-interpret as signed
                                            var operand2Signed = (int)operand2value; // Re-interpret as signed
                                            var resultSigned = operand1Signed ^ operand2Signed;

                                            WriteLogicFlags(
                                                ByteUtility.CountBits(result & 0xFF) % 2 == 0, // PARITY_FLAG
                                                resultSigned == 0, // ZERO_FLAG
                                                resultSigned < 0 // SIGN_FLAG
                                            );
                                            break;
                                        case 2:
                                            throw new InvalidOperationException("ERROR: XOR dst is a word but source is a dword");
                                        case 1:
                                            throw new InvalidOperationException("ERROR: XOR dst is a byte but source is a dword");
                                        default:
                                            throw new InvalidOperationException("ERROR: Unrecognized register for XOR dst");
                                    }
                                    break;
                                }

                            case 2:
                                {
                                    var operand2value = ReadRegister(src);
                                    switch (dst.Size())
                                    {
                                        case 8:
                                            {
                                                var operand1value = ReadR64Register(dst);
                                                WriteR64Register(dst, operand1value ^ operand2value);

                                                var result = operand1value ^ operand2value;
                                                WriteLogicFlags(
                                                    ByteUtility.CountBits(result & 0xFF) % 2 == 0, // PARITY_FLAG
                                                    result == 0, // ZERO_FLAG
                                                    result < 0 // SIGN_FLAG
                                                );
                                                break;
                                            }
                                        case 4:
                                            {
                                                var operand1value = ReadExtendedRegister(dst);
                                                WriteExtendedRegister(dst, operand1value ^ operand2value);

                                                var result = (int)operand1value ^ operand2value;
                                                WriteLogicFlags(
                                                    ByteUtility.CountBits(result & 0xFF) % 2 == 0, // PARITY_FLAG
                                                    result == 0, // ZERO_FLAG
                                                    result < 0 // SIGN_FLAG
                                                );
                                                break;
                                            }
                                        case 2:
                                            {
                                                var operand1value = ReadRegister(dst);
                                                WriteRegister(dst, (ushort)(operand1value ^ operand2value));

                                                var result = operand1value ^ operand2value;
                                                WriteLogicFlags(
                                                    ByteUtility.CountBits(result & 0xFF) % 2 == 0, // PARITY_FLAG
                                                    result == 0, // ZERO_FLAG
                                                    result < 0 // SIGN_FLAG
                                                );
                                                break;
                                            }
                                        case 1:
                                            throw new InvalidOperationException("ERROR: XOR dst is a byte but source is a word");

                                        default:
                                            throw new InvalidOperationException("ERROR: Unrecognized register for XOR dst");
                                    }
                                    break;
                                }
                            case 1:
                                {
                                    var operand2value = ReadHalfRegister(src);
                                    switch (dst.Size())
                                    {
                                        case 8:
                                            {
                                                var operand1value = ReadR64Register(dst);
                                                WriteR64Register(dst, operand1value ^ operand2value);

                                                var result = operand1value ^ operand2value; // dst's full 64 bits preserved; src (byte) zero-extends
                                                WriteLogicFlags(
                                                    ByteUtility.CountBits(result & 0xFF) % 2 == 0, // PARITY_FLAG
                                                    result == 0, // ZERO_FLAG
                                                    result < 0 // SIGN_FLAG
                                                );
                                                break;
                                            }
                                        case 4:
                                            {
                                                var operand1value = ReadExtendedRegister(dst);
                                                WriteExtendedRegister(dst, operand1value ^ operand2value);

                                                var result = (int)operand1value ^ operand2value; // dst's full 32 bits preserved; src (byte) zero-extends
                                                WriteLogicFlags(
                                                    ByteUtility.CountBits(result & 0xFF) % 2 == 0, // PARITY_FLAG
                                                    result == 0, // ZERO_FLAG
                                                    result < 0 // SIGN_FLAG
                                                );
                                                break;
                                            }

                                        case 2:
                                            {
                                                var operand1value = ReadRegister(dst);
                                                WriteRegister(dst, (ushort)(operand1value ^ operand2value));

                                                var result = (short)operand1value ^ operand2value; // dst's full 16 bits preserved; src (byte) zero-extends

                                                WriteLogicFlags(
                                                    ByteUtility.CountBits(result & 0xFF) % 2 == 0, // PARITY_FLAG
                                                    result == 0, // ZERO_FLAG
                                                    result < 0 // SIGN_FLAG
                                                );
                                                break;
                                            }

                                        case 1:
                                            {
                                                var operand1value = ReadHalfRegister(dst);
                                                WriteHalfRegister(dst, (byte)(operand1value ^ operand2value));

                                                var result = (sbyte)operand1value ^ (sbyte)operand2value;

                                                WriteLogicFlags(
                                                    ByteUtility.CountBits(result & 0xFF) % 2 == 0, // PARITY_FLAG
                                                    result == 0, // ZERO_FLAG
                                                    result < 0 // SIGN_FLAG
                                                );
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
                            $"Unknown bytecode {instruction} EIP={InstructionPointer - 1}!",
                            null,
                            null,
                            null));
            }

            return new TickResult(TickErrorCode.Ok, false);
        }

        protected UInt64 ReadMemoryUInt64(ulong address)
        {
            if (address > AddressSpaceSize - 8)
                throw new MemoryAccessViolationException(address, 8, InstructionPointer, isWrite: false);

            return BinaryPrimitives.ReadUInt64LittleEndian(memory.AsSpan((int)address, 8));
        }
    }
}
