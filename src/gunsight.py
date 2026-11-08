"""Battle for Naboo: RS-style gunsight spacing + flyer laser convergence.

usage: python gunsight.py on|off|build [exe]
  on    : backs up exe to bfn.before-gunsight.exe (once) and applies the patch
  off   : restores bytes from that backup (only the patched ranges)
  build : assemble only, print the cave bytes
Needs G:\\msys64\\mingw64\\bin\\as.exe / objcopy.exe / nm.exe.
"""
import os, struct, subprocess, sys, tempfile, shutil

HERE = os.path.dirname(os.path.abspath(__file__))
BIN = r"G:\msys64\mingw64\bin"
GAME = r"G:\Game Files\LucasArts\Star Wars Battle For Naboo\data_pc"
BASE = 0x403000
CAVE_MAX = 0x1000
IMAGE = 0x400000

def v2f(v):  # .text: VA 0x401000 -> file 0x1000; .rdata 0x51A000 -> 0x11A000 (same delta)
    return v - IMAGE

def build():
    tmp = tempfile.mkdtemp()
    obj = os.path.join(tmp, "g.o"); raw = os.path.join(tmp, "g.bin")
    subprocess.check_call([os.path.join(BIN, "as.exe"), "--32", f"--defsym=BASE={BASE:#x}",
                           "-o", obj, os.path.join(HERE, "gunsight.s")])
    subprocess.check_call([os.path.join(BIN, "objcopy.exe"), "-O", "binary", "-j", ".text", obj, raw])
    syms = {}
    for line in subprocess.check_output([os.path.join(BIN, "nm.exe"), obj], text=True).splitlines():
        p = line.split()
        if len(p) == 3: syms[p[2]] = int(p[0], 16)
    code = open(raw, "rb").read()
    shutil.rmtree(tmp)
    assert len(code) <= CAVE_MAX, hex(len(code))
    return code, syms

def rel32(src, dst, size=5):
    return struct.pack("<i", dst - (src + size))

def patches(code, syms):
    hud = BASE + syms["hud_cave"]; aim = BASE + syms["aim_cave"]
    p = []
    p.append((BASE, b"\xCC" * len(code), code))
    # far-ring start: fld [esp+0x10] / fmul [ebx+0xc]  ->  call hud_cave ; nop ; nop
    # (two HUD branches, [hud+5] set / clear, identical layout)
    for a in (0x4E2D94, 0x4E302C):
        p.append((a, bytes.fromhex("d9442410d84b0c"), b"\xE8" + rel32(a, hud) + b"\x90\x90"))
    # hide the big (near) ring: skip inserting its sprite into the draw list.
    # Its position/alpha maths still runs (branch 2's small ring copies that alpha).
    p.append((0x4E2EFC, bytes.fromhex("8b0e8d47288b510889108b0e894108"), b"\xEB\x0D" + b"\x90" * 13))
    p.append((0x4E3169, bytes.fromhex("8b0e8d87080200008b510889108b0e894108"), b"\xEB\x10" + b"\x90" * 16))
    # primary fire aim step 0x490179..0x490197 -> jmp aim_cave
    old = bytes.fromhex("8b86fc0200003dffff000074128b4c24108d542420518d4c2430e8df14f7ff")
    new = b"\xE9" + rel32(0x490179, aim) + b"\x90" * (len(old) - 5)
    p.append((0x490179, old, new))
    # crosshair colour (colour fn 0x4E3850, two paths): stock blue 56 91 FB -> COLOUR
    r, g, b = COLOUR
    for base in (0x4E3869, 0x4E389C):
        if base == 0x4E3869:
            rs, gs, bs = base, 0x4E3870, 0x4E3878
        else:
            rs, gs, bs = base, 0x4E38A1, 0x4E38A5
        p.append((rs, bytes.fromhex("c60656"), bytes([0xC6, 0x06, r])))
        p.append((gs, bytes.fromhex("c6460191"), bytes([0xC6, 0x46, 0x01, g])))
        p.append((bs, bytes.fromhex("c64602fb"), bytes([0xC6, 0x46, 0x02, b])))
    return p

# crosshair colour R, G, B (stock 0x56, 0x91, 0xFB blue; lock-on marker is FF D2 00 yellow)
COLOUR = (0xFF, 0x80, 0x00)

# Flyer target finder (types 4,6,7,10,11): only used to give the convergence a target
# DEPTH - the aim is always forced onto the crosshair ray, so a wider cone is not assist.
# Cone degrees 0x52B73C (stock 1.2,1.2,1.0,2.5,0.5), range 0x52B6DC (stock 400,350,300,0,300).
CONES = [(0x52B73C + 4 * t, v) for t, v in ((4, 3.0), (6, 3.0), (7, 3.0), (10, 2.5), (11, 3.0))]
CONES += [(0x52B6DC + 4 * t, 900.0) for t in (4, 6, 7, 11)]

def main():
    mode = sys.argv[1] if len(sys.argv) > 1 else "build"
    exe = sys.argv[2] if len(sys.argv) > 2 else os.path.join(GAME, "BFN.exe")
    code, syms = build()
    if mode == "build":
        print({k: hex(BASE + v) for k, v in syms.items()}); print(code.hex()); return
    backup = os.path.join(os.path.dirname(exe), "bfn.before-gunsight.exe")
    data = bytearray(open(exe, "rb").read())
    if mode == "on":
        if not os.path.exists(backup):
            shutil.copy2(exe, backup)
        for va, old, new in patches(code, syms):
            f = v2f(va); cur = bytes(data[f:f + len(new)])
            if cur == new: continue
            if va == BASE:  # cave may hold an older build: must be ours or padding
                ref = open(backup, "rb").read()[f:f + len(new)]
                assert ref == old, "cave area not free in backup"
            else:
                assert cur == old, f"unexpected bytes at {va:#x}: {cur.hex()}"
            data[f:f + len(new)] = new
        for va, val in CONES:
            data[v2f(va):v2f(va) + 4] = struct.pack("<f", val)
    elif mode == "off":
        ref = open(backup, "rb").read()
        for va, old, new in patches(code, syms):
            f = v2f(va); data[f:f + len(new)] = ref[f:f + len(new)]
        for va, val in CONES:
            data[v2f(va):v2f(va) + 4] = ref[v2f(va):v2f(va) + 4]
    open(exe, "wb").write(data)
    print(mode, "->", exe)

if __name__ == "__main__":
    main()
