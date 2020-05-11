# picoVM

This is a toy assembly compiler and virutal machine that can build and run 32-bit executables.

# Project summary

The project is currently a single Main() function which performs the following steps:

1. Reads in a .asm file
2. Compiles the assembly to bytecode
3. Writes out an a.out file (experimental, in the future will be divided into workflow tools)
4. Loads the bytecode into a virtual machine
5. Executes the code

# Project layout

First, source code is processed by the ```BytecodeCompiler``` class, which serves as a
lexer, parser, and bytecode compiler all in one.  This produces a ```CompilationResult```
which, if successful, contains a text section and a data section.

A primative loader allocates a byte array and copies the text and data segments, and
allocates an ```Agent```, which serves as the virtual machine.  The executing instruction
pointer within the Agent begins at 0x00000000 and runs one bytecode command per ```Tick()```
method call.

A rudimentary *nix syscall table is implemented to handle interrupts.  The sys_write
syscall, for instance, provides the Hello World program the ability to load a string and
write it to Console.Out.

A small set of unit tests are included in the picovm.Tests directory

# Project maturity

:ballot_box_with_check: .asm file parsed into bytecode
:ballot_box_with_check: 32/16/8-bit registers and MOV instruction handled
:ballot_box_with_check: Data segment with directives DB and EQU
:ballot_box_with_check: Constant value inline expansion
:ballot_box_with_check: Interrupt vectors, initial sys_write syscall
:ballot_box_with_check: BSS segment, initial sys_read syscall
:black_square_button: Separate compliation and loader