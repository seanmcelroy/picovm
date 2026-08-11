using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using picovm.Assembler;

namespace picovm.VM
{
    public class Agent
    {
        #region General Purpose Registers
        // General registers
        /// <summary>
        /// Accumulator register (AX). Used in arithmetic operations. Opcodes combining constants into accumulator are 1-byte.
        /// </summary>
        public const byte R_A = 0;
        /// <summary>
        /// Base register (BX). Used as a pointer to data (located in segment register DS, when in segmented mode).
        /// </summary>
        public const byte R_B = 1;
        /// <summary>
        /// Counter register (CX). Used in shift/rotate instructions and loops.
        /// </summary>
        public const byte R_C = 2;
        /// <summary>
        /// Data register (DX). Used in arithmetic operations and I/O operations.
        /// </summary>
        public const byte R_D = 3;

        // Index and pointers
        /// <summary>
        /// Source Index register (SI). Used as a pointer to a source in stream operations.
        /// </summary>
        public const byte R_SI = 4;
        /// <summary>
        /// Destination Index register (DI). Used as a pointer to a destination in stream operations.
        /// </summary>
        public const byte R_DI = 5;
        /// <summary>
        /// Stack Base Pointer register (BP). Used to point to the base of the stack.
        /// </summary>
        public const byte R_BP = 7;
        /// <summary>
        /// Stack Pointer register (SP). Pointer to the top of the stack.
        /// </summary>
        public const byte R_SP = 8;
        #endregion

        #region Pointer Registers
        public const byte R_IP = 9; // Instruciton Pionter
        #endregion 

        public const byte R_FLAGS = 10; // Status register

        /// <summary>
        /// Flag masks used for the R_FLAGS status register
        /// </summary>
        private const ulong ALU_FLAGS_MASK =
              (1ul << (int)Flag.CARRY_FLAG)
            | (1ul << (int)Flag.PARITY_FLAG)
            | (1ul << (int)Flag.AUX_CARRY_FLAG)
            | (1ul << (int)Flag.ZERO_FLAG)
            | (1ul << (int)Flag.SIGN_FLAG)
            | (1ul << (int)Flag.OVERFLOW_FLAG);

        public const byte R_8 = 11;
        public const byte R_9 = 12;
        public const byte R_10 = 13;
        public const byte R_11 = 14;
        public const byte R_12 = 15;
        public const byte R_13 = 16;
        public const byte R_14 = 17;
        public const byte R_15 = 18;

        #region Segment registers
        /// <summary>
        /// Code Segment (CS). Pointer to the code ('C' stands for 'Code').
        /// </summary>
        public const byte SR_CS = 0; // Code
        /// <summary>
        /// Data Segment (DS). Pointer to the data ('D' stands for 'Data').
        /// </summary>
        public const byte SR_DS = 1; // Data
        /// <summary>
        /// Stack Segment (SS). Pointer to the stack ('S' stands for 'Stack').
        /// </summary>
        public const byte SR_SS = 2; // Stack
        /// <summary>
        /// Extra Segment (ES). Pointer to extra data ('E' stands for 'Extra'; 'E' comes after 'D').
        /// </summary>
        public const byte SR_ES = 3; // Extra Data
        /// <summary>
        /// F Segment (FS). Pointer to more extra data ('F' comes after 'E').
        /// </summary>
        public const byte SR_FS = 4; // Extra Data #2
        /// <summary>
        /// G Segment (GS). Pointer to still more extra data ('G' comes after 'F').
        /// </summary>
        public const byte SR_GS = 5; // Extra Data #3 
        #endregion

        protected ulong[] general_registers = new ulong[19];

        protected ushort[] segment_registers = new ushort[6];

        /// <summary>
        /// The size of the flat address space. Any access at or beyond this faults.
        /// </summary>
        public const int AddressSpaceSize = 65536;

        protected byte[] memory = new byte[AddressSpaceSize];

        private UInt32 InstructionPointer
        {
            get => ReadExtendedRegister(Register.EIP);
            set => WriteExtendedRegister(Register.EIP, value);
        }

        public UInt32 StackPointer
        {
            get => ReadExtendedRegister(Register.SP);
            set => WriteExtendedRegister(Register.SP, value);
        }

        protected IKernel kernel { get; private set; }

        public Agent(IKernel kernel, ReadOnlySpan<byte> program, UInt32 entryPoint) : this(kernel, program.ToArray(), entryPoint)
        {
        }

        public Agent(IKernel kernel, byte[] program, UInt32 entryPoint)
        {
            this.kernel = kernel;
            Array.Copy(program, memory, program.Length);
            StackPointer = (uint)(memory.Length - 1);
            InstructionPointer = entryPoint;
        }

        protected Agent(IKernel kernel, byte[] program)
        {
            this.kernel = kernel;
            Array.Copy(program, memory, program.Length);
            StackPointer = (uint)(memory.Length - 1);
        }

        public static bool ReadStatusRegister(ulong flagsRegister, Flag flag)
        {
            var status32 = (uint)(flagsRegister & uint.MaxValue);
            return (status32 & (1u << (int)flag)) != 0;
        }

        protected internal void WriteStatusRegister(Flag flag, bool value) => WriteStatusRegister(general_registers, flag, value);

        private static void WriteStatusRegister(ulong[] registers, Flag flag, bool value)
        {
            registers[R_FLAGS] = value
                ? registers[R_FLAGS] | (1ul << (int)flag)
                : registers[R_FLAGS] & ~(1ul << (int)flag);
        }

        /// <summary>
        /// Sets all the ALU-related status register flags in one go
        /// to maximize performance
        /// </summary>
        /// <param name="cf">See <see cref="Flag.CARRY_FLAG"/></param>
        /// <param name="pf">See <see cref="Flag.PARITY_FLAG"/></param>
        /// <param name="af">See <see cref="Flag.AUX_CARRY_FLAG"/></param>
        /// <param name="zf">See <see cref="Flag.ZERO_FLAG"/></param>
        /// <param name="sf">See <see cref="Flag.SIGN_FLAG"/></param>
        /// <param name="of">See <see cref="Flag.OVERFLOW_FLAG"/></param>
        /// <remarks>
        /// Notes on the design:
        /// * ref ulong r avoids re-indexing general_registers[R_FLAGS] multiple times
        /// * [Flags]-mask constant is folded at JIT time — the ~ALU_FLAGS_MASK becomes an immediate.
        /// * Named args at callsites are the reason to take 6 bools instead of packing into a single ulong at the caller. The JIT should inline this away.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal void WriteArithmeticFlags(bool cf, bool pf, bool af, bool zf, bool sf, bool of)
        {
            // Each ternary lowers to a branchless setcc on x64/ARM64.
            ulong bits =
                  ((cf ? 1ul : 0ul) << (int)Flag.CARRY_FLAG)
                | ((pf ? 1ul : 0ul) << (int)Flag.PARITY_FLAG)
                | ((af ? 1ul : 0ul) << (int)Flag.AUX_CARRY_FLAG)
                | ((zf ? 1ul : 0ul) << (int)Flag.ZERO_FLAG)
                | ((sf ? 1ul : 0ul) << (int)Flag.SIGN_FLAG)
                | ((of ? 1ul : 0ul) << (int)Flag.OVERFLOW_FLAG);

            ref ulong r = ref general_registers[R_FLAGS];
            r = (r & ~ALU_FLAGS_MASK) | bits;
        }

        /// <summary>
        /// This is a pared down version of the ALU-related flag set-method.
        /// Logic command slike AND/OR/XOR always clear CF/OF/AF and only
        /// set ZF/SF/PF.
        /// </summary>
        /// <param name="pf">See <see cref="Flag.PARITY_FLAG"/></param>
        /// <param name="zf">See <see cref="Flag.ZERO_FLAG"/></param>
        /// <param name="sf">See <see cref="Flag.SIGN_FLAG"/></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal void WriteLogicFlags(bool pf, bool zf, bool sf)
        {
            ulong bits = ((pf ? 1ul : 0ul) << (int)Flag.PARITY_FLAG)
                    | ((zf ? 1ul : 0ul) << (int)Flag.ZERO_FLAG)
                    | ((sf ? 1ul : 0ul) << (int)Flag.SIGN_FLAG);
            ref ulong r = ref general_registers[R_FLAGS];
            r = (r & ~ALU_FLAGS_MASK) | bits;   // implicitly zeros CF/AF/OF
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ReadExtendedRegister(ulong[] registers, Register reference)
        {
            // http://www.cs.virginia.edu/~evans/cs216/guides/x86.html

            // https://stackoverflow.com/questions/1209439/what-is-the-best-way-to-combine-two-uints-into-a-ulong-in-c-sharp
            return reference switch
            {
                Register.EAX => (uint)(registers[R_A] & uint.MaxValue),
                Register.EBX => (uint)(registers[R_B] & uint.MaxValue),
                Register.ECX => (uint)(registers[R_C] & uint.MaxValue),
                Register.EDX => (uint)(registers[R_D] & uint.MaxValue),
                Register.ESP => (uint)(registers[R_SP] & uint.MaxValue),
                Register.SP => (uint)(registers[R_SP] & uint.MaxValue),
                Register.EDI => (uint)(registers[R_DI] & uint.MaxValue),
                Register.ESI => (uint)(registers[R_SI] & uint.MaxValue),
                Register.EBP => (uint)(registers[R_BP] & uint.MaxValue),
                Register.EIP => (uint)(registers[R_IP] & uint.MaxValue),
                _ => throw new InvalidOperationException($"ERROR: Unknown extended register {reference}!")
            };
        }

        public uint ReadExtendedRegister(Register reference) => ReadExtendedRegister(general_registers, reference);

        public ushort ReadRegister(Register reference)
        {
            // 16 bits
            // We want to read the right-most 16 bits of the 64-bit value
            var ret = reference switch
            {
                Register.AX => (ushort)(general_registers[R_A] & (ulong)ushort.MaxValue),
                Register.BX => (ushort)(general_registers[R_B] & (ulong)ushort.MaxValue),
                Register.CX => (ushort)(general_registers[R_C] & (ulong)ushort.MaxValue),
                Register.DX => (ushort)(general_registers[R_D] & (ulong)ushort.MaxValue),
                Register.DI => (ushort)(general_registers[R_DI] & (ulong)ushort.MaxValue),
                Register.SI => (ushort)(general_registers[R_SI] & (ulong)ushort.MaxValue),
                Register.BP => (ushort)(general_registers[R_BP] & (ulong)ushort.MaxValue),
                Register.IP => (ushort)(general_registers[R_IP] & (ulong)ushort.MaxValue),
                Register.CS => segment_registers[SR_CS],
                Register.DS => segment_registers[SR_DS],
                Register.SS => segment_registers[SR_SS],
                Register.ES => segment_registers[SR_ES],
                Register.FS => segment_registers[SR_FS],
                Register.GS => segment_registers[SR_GS],
                _ => throw new InvalidOperationException($"ERROR: Unknown register {reference}!"),
            };
            return ret;
        }

        /// <summary>
        /// Reads the right-most 8-bits (half of a 16 bit register)
        /// </summary>
        /// <param name="reference">The register to read</param>
        /// <returns>The byte representing the right-most 8-bits of the reference register</returns>
        /// <exception cref="InvalidOperationException"></exception>
        public byte ReadHalfRegister(Register reference) => reference switch
        {
            Register.AH => (byte)((general_registers[R_A] & 0xFF00ul) >> 8),
            Register.AL => (byte)(general_registers[R_A] & 0x00FFul),
            Register.BH => (byte)((general_registers[R_B] & 0xFF00ul) >> 8),
            Register.BL => (byte)(general_registers[R_B] & 0x00FFul),
            Register.CH => (byte)((general_registers[R_C] & 0xFF00ul) >> 8),
            Register.CL => (byte)(general_registers[R_C] & 0x00FFul),
            Register.DH => (byte)((general_registers[R_D] & 0xFF00ul) >> 8),
            Register.DL => (byte)(general_registers[R_D] & 0x00FFul),
            _ => throw new InvalidOperationException($"ERROR: Unknown register {reference}!"),
        };

        internal static void WriteExtendedRegister(ulong[] registers, Register reference, uint value)
        {
            switch (reference)
            {
                case Register.EAX:
                    registers[R_A] = value;
                    break;
                case Register.EBX:
                    registers[R_B] = value;
                    break;
                case Register.ECX:
                    registers[R_C] = value;
                    break;
                case Register.EDX:
                    registers[R_D] = value;
                    break;
                case Register.ESP:
                case Register.SP:
                    registers[R_SP] = value;
                    break;
                case Register.ESI:
                    registers[R_SI] = value;
                    break;
                case Register.EDI:
                    registers[R_DI] = value;
                    break;
                case Register.EBP:
                    registers[R_BP] = value;
                    break;
                case Register.EIP:
                    registers[R_IP] = value;
                    break;
                default:
                    throw new InvalidOperationException($"ERROR: Unknown extended register {reference}!");
            }
        }

        protected internal void WriteExtendedRegister(Register reference, uint value) => WriteExtendedRegister(general_registers, reference, value);

        internal static void WriteExtendedRegister(ulong[] registers, Register reference, int value)
        {
            switch (reference)
            {
                case Register.EAX:
                    registers[R_A] = (ulong)value;
                    break;
                case Register.EBX:
                    registers[R_B] = (ulong)value;
                    break;
                case Register.ECX:
                    registers[R_C] = (ulong)value;
                    break;
                case Register.EDX:
                    registers[R_D] = (ulong)value;
                    break;
                case Register.ESP:
                case Register.SP:
                    registers[R_SP] = (ulong)value;
                    break;
                case Register.ESI:
                    registers[R_SI] = (ulong)value;
                    break;
                case Register.EDI:
                    registers[R_DI] = (ulong)value;
                    break;
                case Register.EBP:
                    registers[R_BP] = (ulong)value;
                    break;
                case Register.EIP:
                    registers[R_IP] = (ulong)value;
                    break;
                default:
                    throw new InvalidOperationException($"ERROR: Unknown extended register {reference}!");
            }
        }

        protected internal void WriteRegister(Register reference, ushort value)
        {
            // 16 bits
            // We want to overwrite the right-most 8 bits of the 64-bit value
            // reg_data = (reg_data & (~bit_mask)) | (new_value << 5)
            // https://stackoverflow.com/questions/5925755/how-to-replace-bits-in-a-bitfield-without-affecting-other-bits-using-c

            switch (reference)
            {
                case Register.AX:
                    general_registers[R_A] = general_registers[R_A] & ~(ulong)ushort.MaxValue | (ulong)value;
                    break;
                case Register.BX:
                    general_registers[R_B] = general_registers[R_B] & ~(ulong)ushort.MaxValue | (ulong)value;
                    break;
                case Register.CX:
                    general_registers[R_C] = general_registers[R_C] & ~(ulong)ushort.MaxValue | (ulong)value;
                    break;
                case Register.DX:
                    general_registers[R_D] = general_registers[R_D] & ~(ulong)ushort.MaxValue | (ulong)value;
                    break;
                case Register.DI:
                    general_registers[R_DI] = general_registers[R_DI] & ~(ulong)ushort.MaxValue | (ulong)value;
                    break;
                case Register.SI:
                    general_registers[R_SI] = general_registers[R_SI] & ~(ulong)ushort.MaxValue | (ulong)value;
                    break;
                case Register.BP:
                    general_registers[R_BP] = general_registers[R_BP] & ~(ulong)ushort.MaxValue | (ulong)value;
                    break;
                case Register.IP:
                    general_registers[R_IP] = general_registers[R_IP] & ~(ulong)ushort.MaxValue | (ulong)value;
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

        protected internal void WriteHalfRegister(Register reference, byte value)
        {
            // 8 bits / 1 byte
            switch (reference)
            {
                case Register.AH:
                    general_registers[R_A] = general_registers[R_A] & ~0xFF00ul | ((ulong)value << 8);
                    break;
                case Register.AL:
                    general_registers[R_A] = general_registers[R_A] & ~0x00FFul | (ulong)value;
                    break;
                case Register.BH:
                    general_registers[R_B] = general_registers[R_B] & ~0xFF00ul | ((ulong)value << 8);
                    break;
                case Register.BL:
                    general_registers[R_B] = general_registers[R_B] & ~0x00FFul | (ulong)value;
                    break;
                case Register.CH:
                    general_registers[R_C] = general_registers[R_C] & ~0xFF00ul | ((ulong)value << 8);
                    break;
                case Register.CL:
                    general_registers[R_C] = general_registers[R_C] & ~0x00FFul | (ulong)value;
                    break;
                case Register.DH:
                    general_registers[R_D] = general_registers[R_D] & ~0xFF00ul | ((ulong)value << 8);
                    break;
                case Register.DL:
                    general_registers[R_D] = general_registers[R_D] & ~0x00FFul | (ulong)value;
                    break;
                default:
                    throw new InvalidOperationException($"ERROR: Unknown register {reference}!");
            }
        }

        /// <summary>
        /// Copies <paramref name="length"/> bytes of the agent's memory starting at
        /// <paramref name="address"/>.  Intended for inspection and for asserting the effect
        /// of stores; the copy keeps callers from mutating the running agent.
        /// </summary>
        public ReadOnlySpan<byte> PeekMemory(ulong address, int length)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(length);
            if (address + (ulong)length > (ulong)memory.Length)
                throw new ArgumentOutOfRangeException(nameof(length), $"Read of {length} bytes at 0x{address:X} runs past the end of the {memory.Length} byte address space.");

            return memory.AsSpan((int)address, length);
        }

        public uint StackPeek32() => ReadMemoryUInt32(ReadExtendedRegister(Register.SP));

        public uint StackPop32()
        {
            var ret = ReadMemoryUInt32(StackPointer);
            StackPointer += 4;
            return ret;
        }

        public ushort StackPop16()
        {
            var ret = ReadMemoryUInt16(StackPointer);
            StackPointer += 2;
            return ret;
        }

        public byte StackPop8()
        {
            var ret = ReadMemoryByte(StackPointer);
            StackPointer++;
            return ret;
        }

        public void StackPush(uint value)
        {
            if (StackPointer < 4)
                throw new MemoryAccessViolationException(StackPointer, 4, InstructionPointer, isWrite: true);

            // Push is ALWAYS a 32-bit operation.  Callers convert.
            WriteMemoryUInt32(StackPointer - 4, value);
            StackPointer -= 4;
        }

        public virtual void Dump()
        {
            Console.Error.WriteLine();
            Console.Error.Write($"EAX: 0x{ReadExtendedRegister(Register.EAX):X4} ({ReadExtendedRegister(Register.EAX).ToString().PadLeft(2)})\t");
            Console.Error.Write($"EBX: 0x{ReadExtendedRegister(Register.EBX):X4} ({ReadExtendedRegister(Register.EBX).ToString().PadLeft(2)})\t");
            Console.Error.Write($"ECX: 0x{ReadExtendedRegister(Register.ECX):X4} ({ReadExtendedRegister(Register.ECX).ToString().PadLeft(2)})\t");
            Console.Error.WriteLine($"EDX: 0x{ReadExtendedRegister(Register.EDX):X4} ({ReadExtendedRegister(Register.EDX).ToString().PadLeft(2)})");
            Console.Error.WriteLine($"EIP: 0x{InstructionPointer:X4} ({InstructionPointer})\tESP: 0x{StackPointer:X4} ({StackPointer})");
            Console.Error.WriteLine("(Stack)");
            var i = (ulong)memory.Length;
            var qword = new byte[8];
            do
            {
                memory.AsSpan((int)i - 8, 8).CopyTo(qword.AsSpan(0, 8));
                Console.Error.WriteLine($"{i}\t: {Convert.ToHexStringLower(qword)}");
                i -= 8;
            } while (i > StackPointer);
            Console.Error.WriteLine("...");
            i = InstructionPointer + (8 - InstructionPointer % 8);
            do
            {
                memory.AsSpan((int)i - 8, 8).CopyTo(qword.AsSpan(0, 8));
                Console.Error.WriteLine($"{i}\t: {Convert.ToHexStringLower(qword)}");
                i -= 8;
            } while (i > 0);
        }

        public virtual TickResult Tick()
        {
            var instruction = (Bytecode)ReadMemoryByte(InstructionPointer);
            InstructionPointer++;

            switch (instruction)
            {
                case Bytecode.END:
                    return new TickResult(TickErrorCode.Ok, true);
                case Bytecode.ADD_REGISTER:
                    {
                        var operand1 = (Register)ReadMemoryByte(InstructionPointer);
                        InstructionPointer++;
                        var operand2 = (Register)ReadMemoryByte(InstructionPointer);
                        InstructionPointer++;

                        switch (operand1.Size())
                        {
                            case 4:
                                switch (operand2.Size())
                                {
                                    case 4:
                                        var operand1value = ReadExtendedRegister(operand1);
                                        var operand2value = ReadExtendedRegister(operand2);
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
                                    default:
                                        throw new InvalidOperationException($"ERROR: ADD must compare registers of same size, but {operand1} and {operand2} were provided.");
                                }
                                break;
                            case 2:
                                switch (operand2.Size())
                                {
                                    case 2:
                                        var operand1value = ReadRegister(operand1);
                                        var operand2value = ReadRegister(operand2);
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
                                    default:
                                        throw new InvalidOperationException($"ERROR: ADD must compare registers of same size, but {operand1} and {operand2} were provided.");
                                }
                                break;
                            case 1:
                                switch (operand2.Size())
                                {
                                    case 1:
                                        var operand1value = ReadHalfRegister(operand1);
                                        var operand2value = ReadHalfRegister(operand2);
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
                                    default:
                                        throw new InvalidOperationException($"ERROR: ADD must compare registers of same size, but {operand1} and {operand2} were provided.");
                                }
                                break;
                            default:
                                throw new InvalidOperationException($"ERROR: ADD must compare registers of same size, but {operand1} and {operand2} were provided.");
                        }
                        break;
                    }
                case Bytecode.ADD_IMMEDIATE:
                    {
                        var operand1 = (Register)ReadMemoryByte(InstructionPointer);
                        InstructionPointer++;

                        switch (operand1.Size())
                        {
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
                                    var operand2value = ReadMemoryByte(InstructionPointer);
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
                case Bytecode.ADD_INDIRECT_REGISTER: // ADD EAX, [EBX]
                    {
                        var operand1 = (Register)ReadMemoryByte(InstructionPointer);
                        InstructionPointer++;
                        var operand2 = (Register)ReadMemoryByte(InstructionPointer);
                        InstructionPointer++;

                        switch (operand1.Size())
                        {
                            case 4:
                                {
                                    var operand1value = ReadExtendedRegister(operand1);
                                    var loc = ReadRegisterAsPointer(operand2);
                                    var operand2value = ReadMemoryUInt32(loc);
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
                                    var loc = ReadRegisterAsPointer(operand2);
                                    var operand2value = ReadMemoryUInt16(loc);
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
                                    var loc = ReadRegisterAsPointer(operand2);
                                    var operand2value = ReadMemoryByte(loc);
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
                case Bytecode.ADD_INDIRECT_MEMORY_REGISTER: // ADD [EAX], EBX
                    {
                        var operand1 = (Register)ReadMemoryByte(InstructionPointer);
                        InstructionPointer++;
                        var operand2 = (Register)ReadMemoryByte(InstructionPointer);
                        InstructionPointer++;

                        var loc = ReadRegisterAsPointer(operand1);

                        switch (operand2.Size())
                        {
                            case 4:
                                {
                                    var operand1value = ReadMemoryUInt32(loc);
                                    var operand2value = ReadExtendedRegister(operand2);
                                    WriteMemoryUInt32(loc, operand1value + operand2value);

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
                                    var operand1value = ReadMemoryUInt16(loc);
                                    var operand2value = ReadRegister(operand2);
                                    WriteMemoryUInt16(loc, (ushort)(operand1value + operand2value));

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
                                    var operand1value = ReadMemoryByte(loc);
                                    var operand2value = ReadHalfRegister(operand2);
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
                case Bytecode.ADD_INDIRECT_MEMORY_IMMEDIATE: // ADD [EAX], 2344
                    {
                        var operand1 = (Register)ReadMemoryByte(InstructionPointer);
                        InstructionPointer++;

                        switch (operand1.Size())
                        {
                            case 4:
                                {
                                    var loc = ReadRegisterAsPointer(operand1);
                                    var operand1value = ReadMemoryUInt32(loc);
                                    var operand2value = ReadMemoryUInt32(InstructionPointer);
                                    InstructionPointer += 4;
                                    WriteMemoryUInt32(loc, operand1value + operand2value);

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
                                    var loc = ReadRegisterAsPointer(operand1);
                                    var operand1value = ReadMemoryUInt16(loc);
                                    var operand2value = ReadMemoryUInt16(InstructionPointer);
                                    InstructionPointer += 2;
                                    WriteMemoryUInt16(loc, (ushort)(operand1value + operand2value));

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
                                    var loc = ReadRegisterAsPointer(operand1);
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
                case Bytecode.AND_REGISTER:
                    {
                        var operand1 = (Register)ReadMemoryByte(InstructionPointer);
                        InstructionPointer++;
                        var operand2 = (Register)ReadMemoryByte(InstructionPointer);
                        InstructionPointer++;
                        var o1Size = operand1.Size();
                        if (o1Size != operand2.Size())
                            throw new Exception($"ERROR: Source operand {operand2} is not the same size as the destination operand {operand1}");

                        switch (o1Size)
                        {
                            case 4:
                                {
                                    var result = ReadExtendedRegister(operand1) & ReadExtendedRegister(operand2);
                                    WriteExtendedRegister(operand1, result);
                                    WriteLogicFlags(
                                        ByteUtility.CountBits(result & 0xFF) % 2 == 0, // PARITY_FLAG
                                        result == 0, // ZERO_FLAG
                                        (result & 0x80000000u) != 0 // SIGN_FLAG
                                    );
                                    break;
                                }
                            case 2:
                                {
                                    var result = (ushort)(ReadRegister(operand1) & ReadRegister(operand2));
                                    WriteRegister(operand1, result);
                                    WriteLogicFlags(
                                        ByteUtility.CountBits(result & 0xFF) % 2 == 0, // PARITY_FLAG
                                        result == 0, // ZERO_FLAG
                                        (result & 0x8000) != 0 // SIGN_FLAG
                                    );
                                    break;
                                }
                            case 1:
                                {
                                    var result = (byte)(ReadHalfRegister(operand1) & ReadHalfRegister(operand2));
                                    WriteHalfRegister(operand1, result);
                                    WriteLogicFlags(
                                        ByteUtility.CountBits(result & 0xFF) % 2 == 0, // PARITY_FLAG
                                        result == 0, // ZERO_FLAG
                                        (result & 0x80) != 0 // SIGN_FLAG
                                    );
                                    break;
                                }
                            default:
                                throw new InvalidOperationException($"ERROR: AND cannot handle the type of register targeted: {operand1}");
                        }
                        break;
                    }
                case Bytecode.AND_IMMEDIATE:
                    {
                        var operand1 = (Register)ReadMemoryByte(InstructionPointer);
                        InstructionPointer++;

                        switch (operand1.Size())
                        {
                            case 4:
                                {
                                    var operand1value = ReadExtendedRegister(operand1);
                                    var operand2value = ReadMemoryUInt32(InstructionPointer);
                                    InstructionPointer += 4;
                                    var result = operand1value & operand2value;
                                    WriteExtendedRegister(operand1, result);
                                    WriteLogicFlags(
                                        ByteUtility.CountBits(result & 0xFF) % 2 == 0, // PARITY_FLAG
                                        result == 0, // ZERO_FLAG
                                        (result & 0x80000000u) != 0 // SIGN_FLAG
                                    );
                                    break;
                                }
                            case 2:
                                {
                                    var operand1value = ReadRegister(operand1);
                                    var operand2value = ReadMemoryUInt16(InstructionPointer);
                                    InstructionPointer += 2;
                                    var result = (ushort)(operand1value & operand2value);
                                    WriteRegister(operand1, result);
                                    WriteLogicFlags(
                                        ByteUtility.CountBits(result & 0xFF) % 2 == 0, // PARITY_FLAG
                                        result == 0, // ZERO_FLAG
                                        (result & 0x8000) != 0 // SIGN_FLAG
                                    );
                                    break;
                                }
                            case 1:
                                {
                                    var operand1value = ReadHalfRegister(operand1);
                                    var operand2value = ReadMemoryByte(InstructionPointer);
                                    InstructionPointer++;
                                    var result = (byte)(operand1value & operand2value);
                                    WriteHalfRegister(operand1, result);
                                    WriteLogicFlags(
                                        ByteUtility.CountBits(result & 0xFF) % 2 == 0, // PARITY_FLAG
                                        result == 0, // ZERO_FLAG
                                        (result & 0x80) != 0 // SIGN_FLAG
                                    );
                                    break;
                                }
                            default:
                                throw new InvalidOperationException($"ERROR: AND cannot handle the type of register targeted: {operand1}");
                        }
                        break;
                    }
                case Bytecode.TEST_REGISTER:
                    {
                        var operand1 = (Register)ReadMemoryByte(InstructionPointer);
                        InstructionPointer++;
                        var operand2 = (Register)ReadMemoryByte(InstructionPointer);
                        InstructionPointer++;
                        var o1Size = operand1.Size();
                        if (o1Size != operand2.Size())
                            throw new Exception($"ERROR: Source operand {operand2} is not the same size as the destination operand {operand1}");

                        switch (o1Size)
                        {
                            case 4:
                                {
                                    var result = ReadExtendedRegister(operand1) & ReadExtendedRegister(operand2);
                                    WriteLogicFlags(
                                        ByteUtility.CountBits(result & 0xFF) % 2 == 0, // PARITY_FLAG
                                        result == 0, // ZERO_FLAG
                                        (result & 0x80000000u) != 0 // SIGN_FLAG
                                    );
                                    break;
                                }
                            case 2:
                                {
                                    var result = ReadRegister(operand1) & ReadRegister(operand2);
                                    WriteLogicFlags(
                                        ByteUtility.CountBits(result & 0xFF) % 2 == 0, // PARITY_FLAG
                                        result == 0, // ZERO_FLAG
                                        (result & 0x8000) != 0 // SIGN_FLAG
                                    );
                                    break;
                                }
                            case 1:
                                {
                                    var result = ReadHalfRegister(operand1) & ReadHalfRegister(operand2);
                                    WriteLogicFlags(
                                        ByteUtility.CountBits(result & 0xFF) % 2 == 0, // PARITY_FLAG
                                        result == 0, // ZERO_FLAG
                                        (result & 0x80) != 0 // SIGN_FLAG
                                    );
                                    break;
                                }
                            default:
                                throw new InvalidOperationException($"ERROR: TEST cannot handle the type of register targeted: {operand1}");
                        }
                        break;
                    }
                case Bytecode.TEST_IMMEDIATE:
                    {
                        var operand1 = (Register)ReadMemoryByte(InstructionPointer);
                        InstructionPointer++;

                        switch (operand1.Size())
                        {
                            case 4:
                                {
                                    var result = ReadExtendedRegister(operand1) & ReadMemoryUInt32(InstructionPointer);
                                    InstructionPointer += 4;
                                    WriteLogicFlags(
                                        ByteUtility.CountBits(result & 0xFF) % 2 == 0, // PARITY_FLAG
                                        result == 0, // ZERO_FLAG
                                        (result & 0x80000000u) != 0 // SIGN_FLAG
                                    );
                                    break;
                                }
                            case 2:
                                {
                                    var result = ReadRegister(operand1) & ReadMemoryUInt16(InstructionPointer);
                                    InstructionPointer += 2;
                                    WriteLogicFlags(
                                        ByteUtility.CountBits(result & 0xFF) % 2 == 0, // PARITY_FLAG
                                        result == 0, // ZERO_FLAG
                                        (result & 0x8000) != 0 // SIGN_FLAG
                                    );
                                    break;
                                }
                            case 1:
                                {
                                    var result = ReadHalfRegister(operand1) & ReadMemoryByte(InstructionPointer);
                                    InstructionPointer++;
                                    WriteLogicFlags(
                                        ByteUtility.CountBits(result & 0xFF) % 2 == 0, // PARITY_FLAG
                                        result == 0, // ZERO_FLAG
                                        (result & 0x80) != 0 // SIGN_FLAG
                                    );
                                    break;
                                }
                            default:
                                throw new InvalidOperationException($"ERROR: TEST cannot handle the type of register targeted: {operand1}");
                        }
                        break;
                    }
                case Bytecode.CMP_REGISTER:
                    {
                        var operand1 = (Register)ReadMemoryByte(InstructionPointer);
                        InstructionPointer++;
                        var operand2 = (Register)ReadMemoryByte(InstructionPointer);
                        InstructionPointer++;

                        switch (operand1.Size())
                        {
                            case 4:
                                switch (operand2.Size())
                                {
                                    case 4:
                                        var operand1value = ReadExtendedRegister(operand1);
                                        var operand2value = ReadExtendedRegister(operand2);
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
                                    default:
                                        throw new InvalidOperationException($"ERROR: CMP must compare registers of same size, but {operand1} and {operand2} were provided.");
                                }
                                break;
                            case 2:
                                switch (operand2.Size())
                                {
                                    case 2:
                                        var operand1value = ReadRegister(operand1);
                                        var operand2value = ReadRegister(operand2);

                                        var result = operand1value - operand2value;
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
                                    default:
                                        throw new InvalidOperationException($"ERROR: CMP must compare registers of same size, but {operand1} and {operand2} were provided.");
                                }
                                break;
                            case 1:
                                switch (operand2.Size())
                                {
                                    case 1:
                                        var operand1value = ReadHalfRegister(operand1);
                                        var operand2value = ReadHalfRegister(operand2);

                                        var result = operand1value - operand2value;
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
                                    default:
                                        throw new InvalidOperationException($"ERROR: CMP must compare registers of same size, but {operand1} and {operand2} were provided.");
                                }
                                break;
                            default:
                                throw new InvalidOperationException($"ERROR: CMP must compare registers of same size, but {operand1} and {operand2} were provided.");
                        }
                        break;
                    }
                case Bytecode.CMP_IMMEDIATE:
                    {
                        var operand1 = (Register)ReadMemoryByte(InstructionPointer);

                        switch (operand1.Size())
                        {
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

                                    var result = operand1value - operand2value;
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

                                    var result = operand1value - operand2value;
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
                case Bytecode.MOV_REGISTER: // aka MOV EAX, EBX
                    {
                        var dst = (Register)ReadMemoryByte(InstructionPointer);
                        InstructionPointer++;
                        var src = (Register)ReadMemoryByte(InstructionPointer);
                        InstructionPointer++;

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
                case Bytecode.MOV_IMMEDIATE: // aka MOV EAX, 65 (or) MOV EAX, counter
                    {
                        var dst = (Register)ReadMemoryByte(InstructionPointer);
                        InstructionPointer++;

                        switch (dst.Size())
                        {
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
                                    var val = memory[(int)InstructionPointer];
                                    InstructionPointer++;
                                    WriteHalfRegister(dst, val);
                                    break;
                                }
                            default:
                                Dump();
                                throw new InvalidOperationException($"ERROR: Unrecognized register for MOV dst: {dst}");
                        }
                        break;
                    }
                case Bytecode.MOV_INDIRECT_LOAD: // aka MOV EAX, [EBX]
                    {
                        var dst = (Register)ReadMemoryByte(InstructionPointer);
                        InstructionPointer++;
                        var src = (Register)ReadMemoryByte(InstructionPointer);
                        InstructionPointer++;

                        // The source register holds the address to dereference, not a value.
                        uint addr = ReadRegisterAsPointer(src);

                        // The destination register's width decides how much to load.
                        switch (dst.Size())
                        {
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
                case Bytecode.MOV_INDIRECT_STORE: // MOV [EBX], EAX
                    {
                        var dst = (Register)ReadMemoryByte(InstructionPointer);
                        InstructionPointer++;
                        var src = (Register)ReadMemoryByte(InstructionPointer);
                        InstructionPointer++;

                        // The destination register holds the address to dereference, not a value.
                        uint addr = ReadRegisterAsPointer(dst);

                        // The source register's width decides how much to store.
                        switch (src.Size())
                        {
                            case 4:
                                WriteMemoryUInt32(addr, ReadExtendedRegister(src));
                                break;
                            case 2:
                                WriteMemoryUInt16(addr, ReadRegister(src));
                                break;
                            case 1:
                                WriteMemoryByte(addr, ReadHalfRegister(src));
                                break;
                            default:
                                throw new InvalidOperationException($"ERROR: Unrecognized register for MOV src: {src}");
                        }
                        break;
                    }
                case Bytecode.MOV_DIRECT_STORE: // aka MOV [counter], EAX
                    {
                        // Absolute destination address, always machine width, patched at link time.
                        var addr = ReadMemoryUInt32(InstructionPointer);
                        InstructionPointer += 4;

                        var src = (Register)ReadMemoryByte(InstructionPointer);
                        InstructionPointer++;

                        switch (src.Size())
                        {
                            case 4:
                                WriteMemoryUInt32(addr, ReadExtendedRegister(src));
                                break;
                            case 2:
                                WriteMemoryUInt16(addr, ReadRegister(src));
                                break;
                            case 1:
                                WriteMemoryByte(addr, ReadHalfRegister(src));
                                break;
                            default:
                                throw new InvalidOperationException($"ERROR: Unrecognized register for MOV src: {src}");
                        }
                        break;
                    }
                case Bytecode.MOV_DIRECT_IMMEDIATE: // aka MOV [counter], 65
                    {
                        // Absolute destination address, always machine width, patched at link time.
                        var addr = ReadMemoryUInt32(InstructionPointer);
                        InstructionPointer += 4;

                        // Width of the store.  Unlike every other MOV, no operand implies it: the
                        // destination is a bare address, so the assembler states it explicitly.
                        var size = ReadMemoryByte(InstructionPointer);
                        InstructionPointer++;

                        if (size != 1 && size != 2 && size != 4)
                        {
                            Dump();
                            throw new InvalidOperationException($"ERROR: Unsupported operand size for MOV: {size}");
                        }

                        // The immediate is already little-endian in the instruction stream and the
                        // destination is raw memory, so the store is a byte copy at any width.
                        memory.AsSpan((int)InstructionPointer, size).CopyTo(memory.AsSpan((int)addr, size));
                        InstructionPointer += size;

                        break;
                    }
                case Bytecode.CALL_REGISTER:
                    {
                        var operand = (Register)ReadMemoryByte(InstructionPointer);
                        InstructionPointer++;

                        if (operand.Size() != 4)
                            throw new InvalidOperationException("ERROR: CALL needs a 32-bit register to read a 4-byte address");

                        var address = ReadExtendedRegister(operand);

                        // Push EIP onto the stack, which will be the offset of the instruction following the call.
                        StackPush(InstructionPointer);
                        InstructionPointer = address;
                        break;
                    }
                case Bytecode.CALL_IMMEDIATE:
                    {
                        var returnAddress = InstructionPointer + 4;
                        var loc = ReadMemoryUInt32(InstructionPointer);
                        StackPush(returnAddress);
                        InstructionPointer = loc;
                        break;
                    }
                case Bytecode.RET:
                    InstructionPointer = StackPop32();
                    break;
                case Bytecode.POP_REG:
                    {
                        var operand = (Register)ReadMemoryByte(InstructionPointer);
                        InstructionPointer++;

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
                        var operand = (Register)ReadMemoryByte(InstructionPointer);
                        InstructionPointer++;

                        var loc = ReadRegisterAsPointer(operand);

                        switch (operand.Size())
                        {
                            case 4:
                                WriteMemoryUInt32(loc, StackPop32());
                                break;
                            case 2:
                                WriteMemoryUInt16(loc, StackPop16());
                                break;
                            case 1:
                                WriteMemoryByte(loc, StackPop8());
                                break;
                            default:
                                Dump();
                                throw new InvalidOperationException("ERROR: Unrecognized register for POP");
                        }
                        break;
                    }
                case Bytecode.PUSH_REG:
                    {
                        var operand = (Register)ReadMemoryByte(InstructionPointer);
                        InstructionPointer++;
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
                        var operand = (Register)ReadMemoryByte(InstructionPointer);
                        InstructionPointer++;

                        var loc = ReadRegisterAsPointer(operand);

                        switch (operand.Size())
                        {
                            case 4:
                                StackPush(ReadMemoryUInt32(loc));
                                break;
                            case 2:
                                StackPush(ReadMemoryUInt16(loc));
                                break;
                            case 1:
                                StackPush(ReadMemoryByte(loc));
                                break;
                            default:
                                throw new InvalidOperationException("ERROR: Unrecognized register for PUSH");
                        }

                        break;
                    }
                case Bytecode.PUSH_CON:
                    {
                        // Push is ALWAYS a 32-bit operation
                        var _ = (Register)ReadMemoryByte(InstructionPointer);
                        var val = ReadMemoryUInt32(InstructionPointer);
                        InstructionPointer += 4;
                        StackPush(val);

                        break;
                    }
                case Bytecode.JMP:
                    {
                        var loc = ReadMemoryUInt32(InstructionPointer);
                        InstructionPointer = loc;
                        break;
                    }
                case Bytecode.JE: // Jump if equal (ZF=1)
                case Bytecode.JZ: // Jump if zero (ZF=1); these two are functionally equivilent
                    if (ReadStatusRegister(general_registers[R_FLAGS], Flag.ZERO_FLAG))
                        InstructionPointer = ReadMemoryUInt32(InstructionPointer);
                    else
                        InstructionPointer += 4;
                    break;
                case Bytecode.JNE: // Jump if not equal (ZF=0)
                case Bytecode.JNZ: // Jump if not zero (ZF=0); these two are functionally equivilent
                    if (!ReadStatusRegister(general_registers[R_FLAGS], Flag.ZERO_FLAG))
                        InstructionPointer = ReadMemoryUInt32(InstructionPointer);
                    else
                        InstructionPointer += 4;
                    break;
                case Bytecode.JO: // Jump if overflow (OF=1)
                    if (ReadStatusRegister(general_registers[R_FLAGS], Flag.OVERFLOW_FLAG))
                        InstructionPointer = ReadMemoryUInt32(InstructionPointer);
                    else
                        InstructionPointer += 4;
                    break;
                case Bytecode.JNO: // Jump if not overflow (OF=0)
                    if (!ReadStatusRegister(general_registers[R_FLAGS], Flag.OVERFLOW_FLAG))
                        InstructionPointer = ReadMemoryUInt32(InstructionPointer);
                    else
                        InstructionPointer += 4;
                    break;
                case Bytecode.JS: // Jump if sign (SF=1)
                    if (ReadStatusRegister(general_registers[R_FLAGS], Flag.SIGN_FLAG))
                        InstructionPointer = ReadMemoryUInt32(InstructionPointer);
                    else
                        InstructionPointer += 4;
                    break;
                case Bytecode.JNS: // Jump if not sign (SF=0)
                    if (!ReadStatusRegister(general_registers[R_FLAGS], Flag.SIGN_FLAG))
                        InstructionPointer = ReadMemoryUInt32(InstructionPointer);
                    else
                        InstructionPointer += 4;
                    break;
                case Bytecode.JB: // Jump if below (CF=1)
                case Bytecode.JNAE: // Jump if not above or equal (CF=1)
                case Bytecode.JC: // Jump if carry (CF=1)
                    if (ReadStatusRegister(general_registers[R_FLAGS], Flag.CARRY_FLAG))
                        InstructionPointer = ReadMemoryUInt32(InstructionPointer);
                    else
                        InstructionPointer += 4;
                    break;
                case Bytecode.JNB: // Jump if not below (CF=0)
                case Bytecode.JAE: // Jump if above or equal (CF=0)
                case Bytecode.JNC: // Jump if not carry (CF=0)
                    if (!ReadStatusRegister(general_registers[R_FLAGS], Flag.CARRY_FLAG))
                        InstructionPointer = ReadMemoryUInt32(InstructionPointer);
                    else
                        InstructionPointer += 4;
                    break;
                case Bytecode.JBE: // Jump if below or equal (CF=1 or ZF=1)
                case Bytecode.JNA: // Jump if not above (CF=1 or ZF=1)
                    if (ReadStatusRegister(general_registers[R_FLAGS], Flag.CARRY_FLAG)
                        || ReadStatusRegister(general_registers[R_FLAGS], Flag.ZERO_FLAG))
                        InstructionPointer = ReadMemoryUInt32(InstructionPointer);
                    else
                        InstructionPointer += 4;
                    break;
                case Bytecode.JA:   // Jump if above (CF=0 and ZF=0)
                case Bytecode.JNBE: // Jump if not below or equal (CF=0 and ZF=0)
                    if (!ReadStatusRegister(general_registers[R_FLAGS], Flag.CARRY_FLAG)
                        && !ReadStatusRegister(general_registers[R_FLAGS], Flag.ZERO_FLAG))
                        InstructionPointer = ReadMemoryUInt32(InstructionPointer);
                    else
                        InstructionPointer += 4;
                    break;
                case Bytecode.JL:   // Jump if less (SF <> OF)
                case Bytecode.JNGE: // Jump if not greater or equal (SF <> OF)
                    if (ReadStatusRegister(general_registers[R_FLAGS], Flag.SIGN_FLAG)
                        != ReadStatusRegister(general_registers[R_FLAGS], Flag.OVERFLOW_FLAG))
                        InstructionPointer = ReadMemoryUInt32(InstructionPointer);
                    else
                        InstructionPointer += 4;
                    break;
                case Bytecode.JGE: // Jump if greater or equal (SF = OF)
                case Bytecode.JNL: // Jump if not less (SF = OF)
                    if (ReadStatusRegister(general_registers[R_FLAGS], Flag.SIGN_FLAG)
                        == ReadStatusRegister(general_registers[R_FLAGS], Flag.OVERFLOW_FLAG))
                        InstructionPointer = ReadMemoryUInt32(InstructionPointer);
                    else
                        InstructionPointer += 4;
                    break;
                case Bytecode.JLE: // Jump if less or equal (ZF=1 or SF<>OF)
                case Bytecode.JNG: // Jump if not greater (ZF=1 or SF<>OF)
                    if (ReadStatusRegister(general_registers[R_FLAGS], Flag.ZERO_FLAG)
                        || (
                            ReadStatusRegister(general_registers[R_FLAGS], Flag.SIGN_FLAG)
                            != ReadStatusRegister(general_registers[R_FLAGS], Flag.OVERFLOW_FLAG))
                        )
                        InstructionPointer = ReadMemoryUInt32(InstructionPointer);
                    else
                        InstructionPointer += 4;
                    break;
                case Bytecode.JG:   // Jump if greater (ZF=0 and SF=OF)
                case Bytecode.JNLE: // Jump if not less or equal (ZF=0 and SF=OF)
                    if (!ReadStatusRegister(general_registers[R_FLAGS], Flag.ZERO_FLAG)
                        && (
                            ReadStatusRegister(general_registers[R_FLAGS], Flag.SIGN_FLAG)
                            == ReadStatusRegister(general_registers[R_FLAGS], Flag.OVERFLOW_FLAG))
                        )
                        InstructionPointer = ReadMemoryUInt32(InstructionPointer);
                    else
                        InstructionPointer += 4;
                    break;
                case Bytecode.JP:  // Jump if parity (PF=1)
                case Bytecode.JPE: // Jump if parity even (PF=1)
                    if (ReadStatusRegister(general_registers[R_FLAGS], Flag.PARITY_FLAG))
                        InstructionPointer = ReadMemoryUInt32(InstructionPointer);
                    else
                        InstructionPointer += 4;
                    break;
                case Bytecode.JNP: // Jump if not parity (PF=0)
                case Bytecode.JPO: // Jump if parity odd (PF=0)
                    if (!ReadStatusRegister(general_registers[R_FLAGS], Flag.PARITY_FLAG))
                        InstructionPointer = ReadMemoryUInt32(InstructionPointer);
                    else
                        InstructionPointer += 4;
                    break;
                case Bytecode.JCXZ: // Jump if %CX register is 0
                    if (ReadRegister(Register.CX) == 0)
                        InstructionPointer = ReadMemoryUInt32(InstructionPointer);
                    else
                        InstructionPointer += 4;
                    break;
                case Bytecode.JECXZ: // Jump if %ECX register is 0
                    if (ReadExtendedRegister(Register.ECX) == 0)
                        InstructionPointer = ReadMemoryUInt32(InstructionPointer);
                    else
                        InstructionPointer += 4;
                    break;
                case Bytecode.XOR_REG_REG:
                    {
                        var dst = (Register)ReadMemoryByte(InstructionPointer);
                        InstructionPointer++;
                        var src = (Register)ReadMemoryByte(InstructionPointer);
                        InstructionPointer++;

                        switch (src.Size())
                        {
                            case 4:
                                {
                                    var operand2value = ReadExtendedRegister(src);
                                    switch (dst.Size())
                                    {
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

        protected UInt32 ReadMemoryUInt32(ulong address)
        {
            if (address > AddressSpaceSize - 4)
                throw new MemoryAccessViolationException(address, 4, InstructionPointer, isWrite: false);

            return BinaryPrimitives.ReadUInt32LittleEndian(memory.AsSpan((int)address, 4));
        }

        protected UInt16 ReadMemoryUInt16(ulong address)
        {
            if (address > AddressSpaceSize - 2)
                throw new MemoryAccessViolationException(address, 2, InstructionPointer, isWrite: false);

            return BinaryPrimitives.ReadUInt16LittleEndian(memory.AsSpan((int)address, 2));
        }

        protected byte ReadMemoryByte(ulong address)
        {
            if (address > AddressSpaceSize - 1)
                throw new MemoryAccessViolationException(address, 1, InstructionPointer, isWrite: false);

            return memory[address];
        }

        protected void WriteMemoryUInt32(ulong address, UInt32 value)
        {
            if (address > AddressSpaceSize - 4)
                throw new MemoryAccessViolationException(address, 4, InstructionPointer, isWrite: true);

            BinaryPrimitives.WriteUInt32LittleEndian(memory.AsSpan((int)address, 4), value);
        }

        protected void WriteMemoryUInt16(ulong address, UInt16 value)
        {
            if (address > AddressSpaceSize - 2)
                throw new MemoryAccessViolationException(address, 2, InstructionPointer, isWrite: true);

            BinaryPrimitives.WriteUInt16LittleEndian(memory.AsSpan((int)address, 2), value);
        }

        protected void WriteMemoryByte(ulong address, byte value)
        {
            if (address > AddressSpaceSize - 1)
                throw new MemoryAccessViolationException(address, 1, InstructionPointer, isWrite: true);

            memory[address] = value;
        }

        private uint ReadRegisterAsPointer(Register reg) => reg.Size() switch
        {
            4 => ReadExtendedRegister(reg),
            2 => ReadRegister(reg),
            1 => ReadHalfRegister(reg),
            _ => throw new InvalidOperationException($"Register {reg} cannot be used as a pointer."),
        };
    }
}
