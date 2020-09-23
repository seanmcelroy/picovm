using System;

namespace picovm.Assembler
{
    public static class RegisterUtility
    {
        public static byte Size(this Register register)
        {
            switch (register)
            {
                case Register.RAX:
                case Register.RBX:
                case Register.RCX:
                case Register.RDX:
                case Register.RSP:
                case Register.R8:
                case Register.R9:
                case Register.R10:
                case Register.R11:
                case Register.R12:
                case Register.R13:
                case Register.R14:
                case Register.R15:
                case Register.RSI:
                case Register.RDI:
                case Register.RBP:
                case Register.RIP:
                    return 8;
                case Register.EAX:
                case Register.EBX:
                case Register.ECX:
                case Register.EDX:
                case Register.ESP:
                case Register.ESI:
                case Register.EDI:
                case Register.EBP:
                case Register.EIP:
                    return 4;
                case Register.AX:
                case Register.BX:
                case Register.CX:
                case Register.DX:
                case Register.SP:
                case Register.SI:
                case Register.DI:
                case Register.BP:
                case Register.IP:
                case Register.CS:
                case Register.DS:
                case Register.SS:
                case Register.ES:
                case Register.FS:
                case Register.GS:
                    return 2;
                case Register.AH:
                case Register.AL:
                case Register.BH:
                case Register.BL:
                case Register.CH:
                case Register.CL:
                case Register.DH:
                case Register.DL:
                    return 1;
                default:
                    throw new InvalidOperationException($"Unknown register size: {register}");
            }
        }

    }
}