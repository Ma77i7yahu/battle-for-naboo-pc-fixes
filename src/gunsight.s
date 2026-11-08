# Battle for Naboo - RS-style gunsight spacing + flyer laser convergence
# Assembled at BASE (0x403000, unused int3 padding after the thunk table).
# GNU as, 32-bit, Intel syntax.  Build: gunsight.py
        .intel_syntax noprefix
        .code32
        .text
start:

# --- hud_cave: called from 0x4E2D94 (replaces fld [esp+0x10] / fmul [ebx+0xc])
# Caller has just built the near-ring point N = pos + fwd*16 at [esp+0x2c..0x34]
# (ebx = sight matrix: pos +0, fwd +0xC; k = far multiplier 2.0/3.0/3.2 at [esp+0x10]).
# Rescale it to pos + fwd*16*k so the near ring sits at 1/3 of the far ring
# (48*k), like Rogue Squadron.  +4 on stack offsets for our return address.
hud_cave:
        fld     dword ptr [esp+0x30]
        fsub    dword ptr [ebx]
        fmul    dword ptr [esp+0x14]
        fadd    dword ptr [ebx]
        fstp    dword ptr [esp+0x30]
        fld     dword ptr [esp+0x34]
        fsub    dword ptr [ebx+4]
        fmul    dword ptr [esp+0x14]
        fadd    dword ptr [ebx+4]
        fstp    dword ptr [esp+0x34]
        fld     dword ptr [esp+0x38]
        fsub    dword ptr [ebx+8]
        fmul    dword ptr [esp+0x14]
        fadd    dword ptr [ebx+8]
        fstp    dword ptr [esp+0x38]
        fld     dword ptr [esp+0x14]            # displaced: fld [esp+0x10]
        fmul    dword ptr [ebx+0xc]             # displaced: fmul [ebx+0xc]
        ret

        .balign 16
# --- aim_cave: jmp from 0x490179 (primary fire), returns to 0x490198
# edi = craft, esi = craft state [craft+0x6C]: pos +0, fwd +0xC, target id +0x2FC
# [esp+0x10] fraction, [esp+0x20] aim point copy, [esp+0x2C] bolt matrix
aim_cave:
        mov     eax, dword ptr [edi+0x118]      # craft type
        cmp     eax, 11
        ja      orig
        mov     ecx, 0xCD0                      # flyers: 4,6,7,10,11
        bt      ecx, eax
        jnc     orig

        xor     edx, edx                        # edx = 1: target locked
        cmp     dword ptr [esi+0x2fc], 0xffff
        je      target
        inc     edx
        jmp     target

# Target locked: aim along the CAMERA ray through the ring centre, at the
# target's camera depth, so whatever sits in the ring is hit dead centre.
# View matrix V = [[0x5438D8]+0x24]: rows r0..r2 at +0,+0xC,+0x18 (z = depth),
# translation T at +0x24 (HUD 0x4E2DEA / transform 0x40F4E0).
#   Rw = C + F*f,  Rc = V*Rw + T,  Pz = r2.P + Tz,  Ac = Rc * Pz/Rc.z
#   Aw = sum_j r_j * (Ac_j - T_j) / |r_j|^2      (rows orthogonal)
# Falls back to the nose-line point if no camera or depths < 1.
        .macro ROWDOT row, src
        fld     dword ptr [\src]
        fmul    dword ptr [ebx+\row*12]
        fld     dword ptr [\src+4]
        fmul    dword ptr [ebx+\row*12+4]
        fadd    st, st(1)
        fstp    st(1)
        fld     dword ptr [\src+8]
        fmul    dword ptr [ebx+\row*12+8]
        fadd    st, st(1)
        fstp    st(1)
        fadd    dword ptr [ebx+0x24+\row*4]
        .endm
target:
        push    ebx
        mov     ebx, dword ptr [0x5438d8]
        test    ebx, ebx
        jz      cam_none
        add     ebx, 0x24
        sub     esp, 0x20                       # locals; caller [esp+X] -> [esp+X+0x24]
        fld     dword ptr [BASE+0x710+eax*4]    # f
        fld     st(0)
        fmul    dword ptr [esi+0xc]
        fadd    dword ptr [esi]
        fstp    dword ptr [esp]                 # Rw.x
        fld     st(0)
        fmul    dword ptr [esi+0x10]
        fadd    dword ptr [esi+4]
        fstp    dword ptr [esp+4]
        fmul    dword ptr [esi+0x14]
        fadd    dword ptr [esi+8]
        fstp    dword ptr [esp+8]               # Rw.z
        ROWDOT  0, esp
        fstp    dword ptr [esp+0xc]             # Rc.x
        ROWDOT  1, esp
        fstp    dword ptr [esp+0x10]            # Rc.y
        ROWDOT  2, esp
        fstp    dword ptr [esp+0x14]            # Rc.z
        test    edx, edx
        jz      1f
        ROWDOT  2, esp+0x44                     # Pz = target camera depth
        jmp     2f
1:      fld     dword ptr [esp+0x14]            # no target: depth of the ring
        fmul    dword ptr [BASE+0x708]          # point pushed out to DFAR
        fdiv    dword ptr [BASE+0x710+eax*4]    # (Rc.z * DFAR / f)
2:
        fld     dword ptr [esp+0x14]            # Rc.z, Pz
        fcom    dword ptr [0x51a71c]            # 1.0
        fnstsw  ax
        test    ah, 0x41
        jnz     cam_bad2
        fxch    st(1)                           # Pz, Rc.z
        fcom    dword ptr [0x51a71c]
        fnstsw  ax
        test    ah, 0x41
        jnz     cam_bad2
        fdiv    st, st(1)                       # k = Pz/Rc.z, Rc.z
        fstp    st(1)                           # k
        .irp j, 0, 1, 2
        fld     dword ptr [esp+0xc+\j*4]
        fmul    st, st(1)                       # Ac_j, k
        fsub    dword ptr [ebx+0x24+\j*4]       # D_j, k
        fld     dword ptr [ebx+\j*12]
        fmul    st, st(0)
        fld     dword ptr [ebx+\j*12+4]
        fmul    st, st(0)
        fadd    st, st(1)
        fstp    st(1)
        fld     dword ptr [ebx+\j*12+8]
        fmul    st, st(0)
        fadd    st, st(1)
        fstp    st(1)                           # n_j, D_j, k
        fdivr   st, st(1)                       # D_j/n_j, D_j, k
        fstp    st(1)                           # D_j/n_j, k
        fstp    dword ptr [esp+\j*4]            # k
        .endr
        fstp    st(0)                           # empty
        .irp i, 0, 1, 2
        fld     dword ptr [esp]
        fmul    dword ptr [ebx+\i*4]
        fld     dword ptr [esp+4]
        fmul    dword ptr [ebx+12+\i*4]
        fadd    st, st(1)
        fstp    st(1)
        fld     dword ptr [esp+8]
        fmul    dword ptr [ebx+24+\i*4]
        fadd    st, st(1)
        fstp    st(1)
        fstp    dword ptr [esp+0x44+\i*4]       # aim point (caller [esp+0x20+4i])
        .endr
        test    edx, edx
        jnz     3f
        # no target: lead the ring by the craft's motion during the bolt's
        # flight, t = DFAR / s  (s = caller [esp+8], x4 on level 0x14)
        fld     dword ptr [BASE+0x708]
        fdiv    dword ptr [esp+0x2c]
        cmp     byte ptr [0x542d34], 0x14
        jne     4f
        fdiv    dword ptr [0x51a880]            # 4.0
4:
        .irp i, 0, 1, 2
        fld     dword ptr [esi+0x24+\i*4]
        fmul    st, st(1)
        fadd    dword ptr [esp+0x44+\i*4]
        fstp    dword ptr [esp+0x44+\i*4]
        .endr
        fstp    st(0)
3:
        add     esp, 0x20
        pop     ebx
        jmp     callturn
cam_bad2:
        fstp    st(0)
        fstp    st(0)
        add     esp, 0x20
cam_none:
        pop     ebx
        test    edx, edx
        jnz     nosetarget
        mov     eax, dword ptr [edi+0x118]
        fld     dword ptr [BASE+0x710+eax*4]    # f
        jmp     notarget
nosetarget:
        # d = (P - C) . F
        fld     dword ptr [esp+0x20]
        fsub    dword ptr [esi]
        fmul    dword ptr [esi+0xc]
        fld     dword ptr [esp+0x24]
        fsub    dword ptr [esi+4]
        fmul    dword ptr [esi+0x10]
        faddp   st(1), st
        fld     dword ptr [esp+0x28]
        fsub    dword ptr [esi+8]
        fmul    dword ptr [esi+0x14]
        faddp   st(1), st
        fcom    dword ptr [BASE+0x704]          # MIN
        fnstsw  ax
        test    ah, 1                           # C0: d < MIN
        jz      have_d
        fstp    st(0)
        fld     dword ptr [BASE+0x704]
have_d:
        # aim = C + F*d
        fld     st(0)
        fmul    dword ptr [esi+0xc]
        fadd    dword ptr [esi]
        fstp    dword ptr [esp+0x20]
        fld     st(0)
        fmul    dword ptr [esi+0x10]
        fadd    dword ptr [esi+4]
        fstp    dword ptr [esp+0x24]
        fmul    dword ptr [esi+0x14]
        fadd    dword ptr [esi+8]
        fstp    dword ptr [esp+0x28]
callturn:
        lea     edx, [esp+0x20]
        push    0x3f800000                      # turn fully onto the aim point
        lea     ecx, [esp+0x30]
        .byte 0xE8
        .long 0x401677 - BASE - (. - start) - 4  # bolt turn (0x49C1C0)
        jmp     back

# No target: converge where the ring WILL be when the bolts arrive.
# Bolts fly at fixed world speed s (not inheriting craft velocity V), the ring
# rides f ahead of the craft.  Solve |F*f + V*t| = s*t:
#   a = s^2 - |V|^2,  vf = V.F,  t = f*(vf + sqrt(vf^2 + a)) / a
#   aim = C + F*f + V*t
# s = [esp+8] (same table value the spawn call passes as bolt speed),
# x4 on level 0x14 like the bolt init (0x49FF2E).  V = state+0x24.
notarget:                                       # st: f
        fld     dword ptr [esp+8]               # s, f
        cmp     byte ptr [0x542d34], 0x14
        jne     1f
        fmul    dword ptr [0x51a880]            # 4.0
1:
        fmul    st, st(0)                       # s2, f
        fld     dword ptr [esi+0x24]
        fmul    st, st(0)
        fld     dword ptr [esi+0x28]
        fmul    st, st(0)
        fadd    st, st(1)
        fstp    st(1)
        fld     dword ptr [esi+0x2c]
        fmul    st, st(0)
        fadd    st, st(1)
        fstp    st(1)                           # v2, s2, f
        fsubr   st, st(1)                       # a = s2 - v2, s2, f
        fstp    st(1)                           # a, f
        fcom    dword ptr [0x51a71c]            # 1.0
        fnstsw  ax
        test    ah, 0x41                        # a <= 1 (or NaN-ish): no compensation
        jnz     nocomp
        fld     dword ptr [esi+0x24]
        fmul    dword ptr [esi+0xc]
        fld     dword ptr [esi+0x28]
        fmul    dword ptr [esi+0x10]
        fadd    st, st(1)
        fstp    st(1)
        fld     dword ptr [esi+0x2c]
        fmul    dword ptr [esi+0x14]
        fadd    st, st(1)
        fstp    st(1)                           # vf, a, f
        fld     st(0)
        fmul    st, st(0)                       # vf2, vf, a, f
        fadd    st, st(2)                       # vf2+a, vf, a, f
        fsqrt                                   # r, vf, a, f
        fadd    st, st(1)                       # vf+r, vf, a, f
        fmul    st, st(3)                       # f*(vf+r), vf, a, f
        fdiv    st, st(2)                       # t, vf, a, f
        fstp    st(1)                           # t, a, f
        fstp    st(1)                           # t, f
        # x
        fld     dword ptr [esi+0xc]
        fmul    st, st(2)                       # Fx*f, t, f
        fld     dword ptr [esi+0x24]
        fmul    st, st(2)                       # Vx*t, Fx*f, t, f
        fadd    st, st(1)
        fadd    dword ptr [esi]
        fstp    dword ptr [esp+0x20]
        fstp    st(0)                           # t, f
        # y
        fld     dword ptr [esi+0x10]
        fmul    st, st(2)
        fld     dword ptr [esi+0x28]
        fmul    st, st(2)
        fadd    st, st(1)
        fadd    dword ptr [esi+4]
        fstp    dword ptr [esp+0x24]
        fstp    st(0)
        # z
        fld     dword ptr [esi+0x14]
        fmul    st, st(2)
        fld     dword ptr [esi+0x2c]
        fmul    st, st(2)
        fadd    st, st(1)
        fadd    dword ptr [esi+8]
        fstp    dword ptr [esp+0x28]
        fstp    st(0)                           # t, f
        fstp    st(0)
        fstp    st(0)                           # empty
        jmp     callturn
nocomp:                                         # a, f
        fstp    st(0)
        jmp     have_d

orig:                                           # stock behaviour for other craft
        mov     eax, dword ptr [esi+0x2fc]
        cmp     eax, 0xffff
        je      back
        mov     ecx, dword ptr [esp+0x10]
        lea     edx, [esp+0x20]
        push    ecx
        lea     ecx, [esp+0x30]
        .byte 0xE8
        .long 0x401677 - BASE - (. - start) - 4
back:
        .byte 0xE9
        .long 0x490198 - BASE - (. - start) - 4

        .org 0x704
mind:      .float 10.0
dfar:      .float 400.0                         # unlocked shots: aim distance
        .org 0x710
# far ring distance per craft type = 48 * k (HUD 0x4E2CC7..0x4E2D2A)
fartab: .float 96, 96, 96, 96, 144, 96, 144, 153.6, 96, 96, 96, 144
end:
