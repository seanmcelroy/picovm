section	.text
    global _start       ; must be declared for linker (ld)

_start:                 ; tells linker entry point
    MOV EAX, 4294967295 ; copy the value 11111111111111111111111111111111 into eax
    MOV AX, 0           ; copy the value 0000000000000000 into ax
    MOV AH, 170         ; copy the value 10101010 (0xAA) into ah
    MOV AL, 85          ; copy the value 01010101 (0x55) into al
    MOV EBX, 5          ; copy the value 5 into ebx
    MOV EAX, EBX        ; copy the value in ebx into eax
    PUSH 4              ; push 4 on the stack
    PUSH EAX            ; push eax (5) on the stack
    PUSH 6              ; push 6 on the stack
    POP EBX             ; pop stack (6) into ebx
    POP EBX             ; pop stack (5) into ebx
    POP [EBX]           ; pop stack (4) into [ebx] memory location = 5
    ADD [EBX], 10       ; add 10 to the value in [ebx] which would change 4 to 14
    PUSH [EBX]          ; push [ebx] memory location=5 value=14 onto the stack
    END
