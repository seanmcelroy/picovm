section .data                           ; Data segment
   userMsg db 'Please enter a number: ' ; Ask the user to enter a number
   lenUserMsg equ $ - userMsg           ; The length of the message
   dispMsg db 'You have entered: '
   lenDispMsg equ $ - dispMsg

section .bss                            ; Uninitialized data
   num resb 5

section .text                           ; Code Segment
   global _start

_start:                                 ; User prompt
   mov rax, 4
   mov rbx, 1
   mov rcx, userMsg
   mov rdx, lenUserMsg
   int 80h

   ; Read and store the user input
   mov rax, 3
   mov rbx, 0
   mov rcx, num
   mov rdx, 5                           ; 5 bytes (numeric, 1 for sign) of that information
   int 80h

   ; Output the message 'The entered number is: '
   mov rax, 4
   mov rbx, 1
   mov rcx, dispMsg
   mov rdx, lenDispMsg
   int 80h

   ; Output the number entered
   mov rax, 4
   mov rbx, 1
   mov rcx, num
   mov rdx, 5
   int 80h

   ; Exit code
   mov rax, 1
   mov rbx, 0
   int 80h