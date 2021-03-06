# picoVM

This is a toy assembly compiler and virutal machine that can build and run 32-bit executables.

# Usage

## asm
To compile assembly code into an output binary, use the 'asm' command.
```picovm asm OUTPUT TYPE FORMAT INPUT```
* OUTPUT - resulting .elf output file
* TYPE - elf32 and elf64 is the only supported package types
* FORMAT - pico is the only supported code/text format
* INPUT- source .asm assembly file

## inspect
To inspect a binary, similar to the GNU 'readelf' command, use the 'inspect' command.
```picovm inspect EXECUTABLE```
* EXECUTABLE - file to parse

## run
To run a binary in the virtual machine, use the 'run' command.
```picovm run EXECUTABLE```
* EXECUTABLE - file to run in the virtual machine

## asmrun
To compile and immediately run a binary in one command, use 'asmrun'.
```picovm asmrun OUTPUT TYPE FORMAT INPUT```
_(Same parameters as asm)_

# Project summary

The project is currently a single Main() function which performs the following steps:

1. Reads in a .asm file
2. Assembles the assembly code to bytecode
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

* :ballot_box_with_check: .asm file parsed into bytecode
* :ballot_box_with_check: 32/16/8-bit registers and MOV instruction handled
* :ballot_box_with_check: Data segment with directives DB and EQU
* :ballot_box_with_check: Constant value inline expansion
* :ballot_box_with_check: Interrupt vectors, initial sys_write syscall
* :ballot_box_with_check: BSS segment, initial sys_read syscall
* :ballot_box_with_check: Separate compliation and loader
* :ballot_box_with_check: Output syntactically correct ELF64 binary
* :ballot_box_with_check: Read ELF64 binary
* :black_square_button: x64 support
* :black_square_button: Create x64-compatible bytecode
* :black_square_button: Floating-point unit (FPU)
