.CODE

GetFilters PROC 
    mov esi, dword ptr [rsp + 40] ; Load the double value from stack

    ; First value
    movd mm0, ecx       ; Move the first byte to MM0
    movd mm1, esi
    psllq mm0, 16       ; Shift left by 16 bits
    psllq mm1, 16

    ; Second value
    movd mm2, edx       ; Use additional temporary value
    por mm0, mm2        ; Or with the second value
    movd mm2, esi
    por mm1, mm2
    psllq mm0, 16       ; Shift left by 16 bits
    psllq mm1, 16

    ; Third value
    movd mm2, r8d       ; Use additional temporary value
    por mm0, mm2        ; Or with the third value
    movd mm2, esi
    por mm1, mm2
    psllq mm0, 16       ; Shift left by 16 bits
    psllq mm1, 16

    ; Forth value
    movd mm2, r9d       ; Use additional temporary value
    por mm0, mm2        ; Or with the third value
    movd mm2, esi
    por mm1, mm2

    pmullw mm0, mm1     ; Multiply MM0 and MM1, and store result in MM0

    movq rax, mm0       ; Move MM0 to accumulator and return value
    ret

GetFilters ENDP
END