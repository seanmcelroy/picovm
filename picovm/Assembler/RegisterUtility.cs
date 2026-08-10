using System;
using System.Runtime.CompilerServices;

namespace picovm.Assembler
{
    public static class RegisterUtility
    {
        // Register is a byte-valued enum; max value is 215 (R15).
        // Zero entries mean "unknown register" and fall into the throw path.
        private static readonly byte[] sizes = BuildSizeTable();

        private static byte[] BuildSizeTable()
        {
            var t = new byte[256];
            ReadOnlySpan<Register> r8 = [
                Register.RAX, Register.RBX, Register.RCX, Register.RDX, Register.RSP,
                Register.RSI, Register.RDI, Register.RBP, Register.RIP,
                Register.R8,  Register.R9,  Register.R10, Register.R11,
                Register.R12, Register.R13, Register.R14, Register.R15,
            ];
            ReadOnlySpan<Register> r4 = [
                Register.EAX, Register.EBX, Register.ECX, Register.EDX,
                Register.ESP, Register.ESI, Register.EDI, Register.EBP, Register.EIP,
            ];
            ReadOnlySpan<Register> r2 = [
                Register.AX, Register.BX, Register.CX, Register.DX,
                Register.SP, Register.SI, Register.DI, Register.BP, Register.IP,
                Register.CS, Register.DS, Register.SS, Register.ES, Register.FS, Register.GS,
            ];
            ReadOnlySpan<Register> r1 = [
                Register.AH, Register.AL, Register.BH, Register.BL,
                Register.CH, Register.CL, Register.DH, Register.DL,
            ];
            foreach (var r in r8) t[(byte)r] = 8;
            foreach (var r in r4) t[(byte)r] = 4;
            foreach (var r in r2) t[(byte)r] = 2;
            foreach (var r in r1) t[(byte)r] = 1;
            return t;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Size(this Register register)
        {
            var s = sizes[(byte)register];
            if (s == 0) ThrowUnknown(register);
            return s;
        }

        private static void ThrowUnknown(Register r) =>
            throw new InvalidOperationException($"Unknown register size: {r}");
    }
}
