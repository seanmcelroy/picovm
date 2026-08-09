using System;

namespace picovm.VM
{
    public class MemoryAccessViolationException(UInt64 address, int width, UInt64 instructionPointer, bool isWrite = false) : Exception
    {
        public readonly UInt64 Address = address;
        public readonly int Width = width;
        public readonly UInt64 InstructionPointer = instructionPointer;
        public readonly bool IsWrite = isWrite;
    }
}