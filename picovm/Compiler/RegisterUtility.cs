using System;

namespace picovm.Compiler
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
                case Register.R8:
                case Register.R9:
                case Register.R10:
                case Register.R11:
                case Register.R12:
                case Register.R13:
                case Register.R14:
                case Register.R15:
                    return 8;
                case Register.EAX:
                case Register.EBX:
                case Register.ECX:
                case Register.EDX:
                case Register.SP:
                    return 4;
                case Register.AX:
                case Register.BX:
                case Register.CX:
                case Register.DX:
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