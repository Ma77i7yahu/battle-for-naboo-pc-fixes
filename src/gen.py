"""Builds the patch tables for the Battle for Naboo mod installer.

Every patch is taken from the hand-patched exes in the game's data_pc folder
(diffed against the untouched bfn.exe), so the installer reproduces exactly
the bytes that were tested in game.  Outputs:
  PatchData.cs        - embedded tables for Installer.cs
  ../dist/PATCHES.txt - full byte listing + disassembly of every change
"""
import hashlib, os, struct, sys

GAME = sys.argv[1] if len(sys.argv) > 1 else r"G:\Game Files\LucasArts\Star Wars Battle For Naboo\data_pc"
HERE = os.path.dirname(os.path.abspath(__file__))
ORIG_MD5 = "64f8b9c0f356c31d64c51455a2b0cf23"
DATA_MD5 = "b91ef5db2efb0b575234c1013c5bb130"   # untouched data.bin (only checked here, not by the installer)

def rd(n): return open(os.path.join(GAME, n), "rb").read()

orig = rd("bfn_original.exe")
assert hashlib.md5(orig).hexdigest() == ORIG_MD5
ws169 = rd("bfn_before_aimfix.exe")                           # widescreen 16:9 only
aim169 = rd("bfn_widescreen_16x9.before-platinum-reqs.exe")   # + aim assist v3
VARIANTS = {"16x9": (16, 9), "16x10": (16, 10), "21x9": (64, 27), "32x9": (32, 9)}   # 21x9 = 2.370 (2560x1080)
full = {k: rd("bfn_widescreen_%s.before-fps-fix.exe" % k) for k in VARIANTS}  # + platinum requirements
assert full["16x9"] == rd("bfn.before-fps-fix.exe")
fps = {k: rd("bfn_widescreen_%s.before-mission-wrap.exe" % k) for k in VARIANTS}  # + high fps fix
assert fps["16x9"] == rd("bfn.before-mission-wrap.exe")
quick = {k: rd("bfn_widescreen_%s.before-mission-silent.exe" % k) for k in VARIANTS}  # + mission select quick step
silent = {k: rd("bfn_widescreen_%s.before-ground-convergence.exe" % k) for k in VARIANTS}  # + silent mission steps
assert silent["16x9"] == rd("bfn.before-ground-convergence.exe")

def runs(a, b, gap=8):
    spans, i = [], 0
    assert len(a) == len(b)
    while i < len(a):
        if a[i] != b[i]:
            j = i
            while j < len(a) and a[j] != b[j]: j += 1
            if spans and i - spans[-1][1] <= gap: spans[-1][1] = j
            else: spans.append([i, j])
            i = j
        else: i += 1
    return [(s, b[s:e], a[s:e]) for s, e in spans]  # (offset, new, old)

# --- widescreen -------------------------------------------------------------------------------
# Six float immediates scale with the aspect ratio: value = stock value * aspect / (4/3).
# Everything else in the widescreen diff is the same for every aspect.
WS_FLOATS = {0x00D272: "camera aspect (mov [5F54C8], imm): 4/3 -> screen aspect",
             0x04C083: "terrain/water view-pyramid corner ray x (-1.0 at 4:3)",
             0x0CBEB2: "radar map width (push imm)",
             0x0CBEC8: "radar map x position (push imm)",
             0x0CBED9: "radar map width, second layout (push imm)",
             0x0CBEEF: "radar map x position, second layout (push imm)"}
def f32(b, o=0): return struct.unpack_from("<f", b, o)[0]
def ws_float(off, w, h):
    return struct.pack("<f", f32(orig, off) * (w / h) / (4.0 / 3.0))

def split_ws(exe):
    rr = runs(orig, exe)
    fixed = []
    for off, new, old in rr:
        # drop the float immediates from the run (they're computed per aspect)
        keep = [i for i in range(off, off + len(new)) if not any(f <= i < f + 4 for f in WS_FLOATS)]
        cur = []
        for i in keep:
            if cur and i == cur[-1] + 1: cur.append(i)
            else:
                if cur: fixed.append(cur)
                cur = [i]
        if cur: fixed.append(cur)
    return [(c[0], exe[c[0]:c[-1] + 1], orig[c[0]:c[-1] + 1]) for c in fixed]
widescreen = split_ws(ws169)
assert all(ws169[o:o + 4] == ws_float(o, 16, 9) for o in WS_FLOATS)

aimassist = runs(ws169, aim169)
platinum = runs(aim169, full["16x9"])
highfps = runs(full["16x9"], fps["16x9"])
mquick = runs(fps["16x9"], quick["16x9"])
msilent = runs(quick["16x9"], silent["16x9"])
# every aspect variant = same fixed bytes + computed floats + same runs for every other option
def build(w, h, keys):
    img = bytearray(orig)
    for k in keys:
        for off, new, old in GROUPS_BY_KEY[k]:
            assert img[off:off + len(old)] == old
            img[off:off + len(new)] = new
        if k == "widescreen":
            for o in WS_FLOATS: img[o:o + 4] = ws_float(o, w, h)
    return bytes(img)
GROUPS = [
    ("widescreen", "Hor+ widescreen (3D view, level culling, terrain edges, radar map)", widescreen),
    ("aimassist", "Remove laser aim assist (every craft)", aimassist),
    ("platinum", "Results screen shows the platinum requirements once you have gold", platinum),
    ("highfps", "Vehicles pull away from a standstill above 60 fps", highfps),
    ("mquick", "Mission Select moves one mission per key press, even mid-slide", mquick),
    ("msilent", "Mission Select steps make no sound", msilent),
]
GROUPS_BY_KEY = {k: g for k, _, g in GROUPS}
for name, (w, h) in VARIANTS.items():
    assert build(w, h, ["widescreen", "aimassist", "platinum", "highfps", "mquick", "msilent"]) == silent[name], name
    assert build(w, h, ["widescreen", "aimassist", "platinum", "highfps", "mquick"]) == quick[name], name
    assert build(w, h, ["widescreen", "aimassist", "platinum", "highfps"]) == fps[name], name
    assert build(w, h, ["widescreen", "aimassist", "platinum"]) == full[name], name
    assert build(w, h, ["widescreen", "aimassist"]) == rd("bfn_widescreen_%s.before-platinum-reqs.exe" % name), name
def span(g): return set(i for off, new, old in g for i in range(off, off + len(new)))
spans = [span(g) for _, _, g in GROUPS]
assert all(not (a & b) for i, a in enumerate(spans) for b in spans[i + 1:])
assert not any(f <= i < f + 4 for i in set().union(*spans[1:]) for f in WS_FLOATS)

# --- Borvo the Hutt flight ceiling (data.bin) ------------------------------------------------
# Min Y of the mission's player_StartPosition record (Y points down): -70 -> -200.
BORVO_OFF, BORVO_STOCK, BORVO_NEW = 0x1081D40, -70.0, -200.0
with open(os.path.join(GAME, "data.bin.before-borvo-ceiling"), "rb") as f:
    f.seek(BORVO_OFF); rec = f.read(0x24)
assert f32(rec) == BORVO_STOCK and f32(rec, 4) == 99999.0 and rec[0x10:0x24] == b"player_StartPosition"
with open(os.path.join(GAME, "data.bin"), "rb") as f:
    f.seek(BORVO_OFF); assert f32(f.read(4)) == BORVO_NEW

# --- C# ---------------------------------------------------------------------------------------
def cs_bytes(b): return "new byte[]{" + ",".join("0x%02X" % x for x in b) + "}"
lines = ["// Generated by gen.py - do not edit.", "using System;", "", "static class PatchData {",
         '    public const string OriginalMd5 = "%s";' % ORIG_MD5,
         "    public const int OriginalLength = %d;" % len(orig),
         "    // widescreen float immediates: value = stock * aspect / (4/3)",
         "    public static readonly int[] WsFloatOffsets = {%s};" % ", ".join("0x%06X" % o for o in WS_FLOATS),
         "    public static readonly float[] WsFloatStock = {%s};" % ", ".join("BitConverter.ToSingle(new byte[]{%s}, 0)" % ",".join("0x%02X" % x for x in orig[o:o + 4]) for o in WS_FLOATS),
         "    public const long BorvoOffset = 0x%X;" % BORVO_OFF,
         "    public const float BorvoStock = %.1ff, BorvoNew = %.1ff;" % (BORVO_STOCK, BORVO_NEW),
         "    public static readonly Patch[] Groups = {"]
for key, title, g in GROUPS:
    lines.append('        new Patch("%s", new Run[]{' % key)
    for off, new, old in g:
        lines.append("            new Run(0x%06X, %s, %s)," % (off, cs_bytes(old), cs_bytes(new)))
    lines.append("        }),")
lines += ["    };", "}"]
open(os.path.join(HERE, "PatchData.cs"), "w").write("\n".join(lines) + "\n")

# --- PATCHES.txt ------------------------------------------------------------------------------
import capstone
cs = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_32)
def dis(code, va):
    return [("        %08X  %-22s %s %s" % (i.address, i.bytes.hex(), i.mnemonic, i.op_str)).rstrip() for i in cs.disasm(code, va)]
INSN = {0x03D5AD: 0x03D5AB, 0x072B08: 0x072B07, 0x072B18: 0x072B16, 0x09BF80: 0x09BF80, 0x09BFB4: 0x09BFB4,
        0x0B39AE: 0x0B39AE, 0x0B3A19: 0x0B3A19, 0x0B3AB5: 0x0B3AB5, 0x097C4A: 0x097C4A,
        0x0BDE9E: 0x0BDE9E, 0x006340: 0x006340, 0x0BE17C: 0x0BE17C, 0x0BE214: 0x0BE214}
NOTES = {
    0x03D5AD: "level sector culling: fmul [51A8E4] (its own 4/3) -> fmul [5F54C8] (the camera aspect)",
    0x072B08: "terrain cell buffer: alloc 0x4E0 -> 0x2000 bytes and its four sub-arrays moved apart, room for the wider view pyramid",
    0x072B18: "(same buffer: sub-array offsets 0x190/0x320/0x400 -> 0x800/0x1000/0x1800)",
    0x09BF80: "\"target below you\" cone widening skipped (jne -> jmp)",
    0x09BFB4: "aim-assist cone test always rejects (je -> jmp 0049C05E)",
    0x12B74C: "per-craft aim-assist cone table 0052B73C.. : flyer entries -> 0 (no longer reached after 0049BFB4)",
    0x12B766: "(same table)",
    0x0952E0: "code cave in int3 padding: tier = min(best medal + 1, 4); gold/platinum heading -> own string",
    0x095310: "string \"NEEDED FOR\\nPLATINUM\"",
    0x0B39AE: "results screen: always use the next-medal layout (jb -> jmp)",
    0x0B3A19: "requirement tier -> call cave 004952E0",
    0x0B3AB5: "heading text id -> call cave 004952F0",
    0x093260: "code cave in int3 padding: frame time < 0.0163 s (above ~61 fps) -> skip the standstill cut-off, else stock test",
    0x0932A0: "float 0.0163 (frame-time limit used by the cave)",
    0x097C4A: "vehicle movement: |velocity x frame time|^2 < 1e-6 zeroes the velocity; thrust from rest is smaller than that above 60 fps -> call cave 00493260",
    0x0BDE9E: "Mission Select loop, slide states: call slide 004BE370 -> call cave 00406340",
    0x006340: "code cave in int3 padding: run the slide, then the Left/Right/select handler 004BE050 (input is newly-pressed only, so one step per press)",
    0x0BE17C: "Left step: page-turn sound call 00401924 (sound 0x5C) -> nop",
    0x0BE214: "Right step: page-turn sound call 00401924 (sound 0x5C) -> nop",
}
out = ["Star Wars Episode I: Battle for Naboo (PC) - complete patch listing",
       "Base exe: data_pc\\bfn.exe, %d bytes, MD5 %s" % (len(orig), ORIG_MD5.upper()),
       "off = file offset; VA = 0x400000 + off (all changes are in .text/.rdata, which map 1:1).",
       "Every 'old' value below has been checked against the original exe.", ""]
for key, title, g in GROUPS:
    img = bytearray(build(16, 9, [key]))
    out += ["=" * 78, "[%s] %s  (%d changes)" % (key, title, len(g)), "=" * 78]
    sh = lambda b: b.hex() if len(b) <= 24 else b[:24].hex() + "...(%d bytes)" % len(b)
    for off, new, old in g:
        out.append("  off %06X  (VA %08X)  %s -> %s" % (off, 0x400000 + off, sh(old), sh(new)))
        if off in NOTES: out.append("        ; " + NOTES[off])
        if off in INSN:
            s = INSN[off]; out += dis(bytes(img[s:max(off + len(new), s + 6)]), 0x400000 + s)
        if off == 0x0952E0: out += dis(bytes(img[0x952E0:0x95310]), 0x4952E0)
        if off == 0x093260: out += dis(bytes(img[0x93260:0x93282]), 0x493260)
    if key == "widescreen":
        out.append("  Aspect-dependent float immediates (written by the installer, value = stock x aspect / (4/3)):")
        for o, note in WS_FLOATS.items():
            out.append("  off %06X  (VA %08X)  stock %s = %g   16:9 %s = %g" % (o, 0x400000 + o, orig[o:o + 4].hex(), f32(orig, o),
                                                                       ws_float(o, 16, 9).hex(), f32(ws_float(o, 16, 9))))
            out.append("        ; " + note)
    out.append("")
out += ["=" * 78, "[borvo] Borvo the Hutt flight ceiling  (data_pc\\data.bin, not the exe)", "=" * 78,
        "  off %07X  float %g -> %g  (min Y of the mission's player_StartPosition record; Y points down)" % (BORVO_OFF, BORVO_STOCK, BORVO_NEW),
        "        ; the installer checks the record (maxY 99999.0 at +4, name \"player_StartPosition\" at +0x10) before writing.",
        "        ; untouched data.bin MD5 %s" % DATA_MD5.upper(), ""]
open(os.path.join(HERE, "..", "dist", "PATCHES.txt"), "w", newline="\r\n").write("\n".join(out) + "\n")
print("ok", [(k, len(g)) for k, _, g in GROUPS])
