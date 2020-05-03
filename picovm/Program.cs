using System;
using System.Linq;

namespace agent_playground
{
    class Program
    {
        static int Main(string[] args)
        {
            Console.WriteLine("PicoVM - A tiny toy virtual machine");
            Console.WriteLine(" (c) 2020 Sean McElroy");
            Console.WriteLine(" Released under the MIT License; all rights reserved.");
            Console.WriteLine();
            Console.WriteLine(" Usage: picovm <src.asm>");

            if (args.Length == 0)
            {
                Console.Error.WriteLine("No source input file provided.");
                return -1;
            }

            if (!System.IO.File.Exists(args[0]))
            {
                Console.Error.WriteLine($"Source input file {args[0]} not found.");
                return -2;
            }

            string[] programText;
            try
            {
                programText = System.IO.File.ReadAllLines(args[0]);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error while attempting to read input file: {ex.Message}");
                return -3;
            }

            /*var programText = new string[] {
                "MOV EAX, 4294967295", // copy the value 11111111111111111111111111111111 into eax
                "MOV AX, 0", // copy the value 0000000000000000 into ax
                "MOV AH, 170", // copy the value 10101010 (0xAA) into ah
                "MOV AL, 85", // copy the value 01010101 (0x55) into al
                "MOV EBX, 5", // copy the value 5 into ebx
                "MOV EAX, EBX", // copy the value in ebx into eax
                "PUSH 4", // push 4 on the stack
                "PUSH EAX", // push eax (5) on the stack
                "PUSH 6", // push 6 on the stack
                "POP EBX", // pop stack (6) into ebx
                "POP EBX", // pop stack (5) into ebx
                "POP [EBX]", // pop stack (4) into [ebx] memory location = 5
                "ADD [EBX], 10", // add 10 to the value in [ebx] which would change 4 to 14
                "PUSH [EBX]", // push [ebx] memory location=5 value=14 onto the stack
                "END"
            };*/

            var compiler = new Compiler();
            var programCode = compiler.Compile(programText);

            var agent = new Agent(programCode.ToArray());
            int? ret;
            do
            {
                ret = agent.Tick();
                //agent.Dump();
            } while (ret == null);
            switch (ret)
            {
                case Agent.E_INVALID:
                    throw new Exception($"ERROR: Unknown bytecode!");
                case 0:
                    Console.WriteLine("Program terminated.");
                    break;
            }

            return 0;
        }
    }
}
