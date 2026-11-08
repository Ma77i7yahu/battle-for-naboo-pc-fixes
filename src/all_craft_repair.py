# Battle for Naboo: give every player craft the N-1's self-repair.
# Repair fn 0x4898E0 (per player, per frame): if craft flags [cfg+0x120] & 0x40 (and not 0x100000/0x80000)
# and health [obj+0x34] < max [cfg+0xEC]: health += dt * rate [cfg+0xF0], clamped to max.
# Stock: only the N-1 (type 4) init sets bit 0x40; its rate is 1.92/s for max 320 (0.6 %/s).
# Damage handler 0x48C091 (byte obj+0x420 = copy of the bit): below 50 % health plays a warning (sound 0x1CA),
# below 25 % plays 0x34A/0x1CB and turns the repair off for that life - same as the N-1.
# Patch: in all 14 craft init functions set bit 0x40 in the flags immediate and set rate = max * 0.006.
# Usage: python -I all_craft_repair.py <bfn.exe> [apply]
import struct, sys

def sections(d):
    pe = struct.unpack_from('<I', d, 0x3c)[0]; n = struct.unpack_from('<H', d, pe + 6)[0]
    opt = struct.unpack_from('<H', d, pe + 20)[0]
    return [struct.unpack_from('<8sIIII', d, pe + 24 + opt + i * 40) for i in range(n)]

def main():
    path = sys.argv[1]; apply = len(sys.argv) > 2 and sys.argv[2] == 'apply'
    d = bytearray(open(path, 'rb').read())
    nm, vs, rva, rs, ro = sections(d)[0]
    base = 0x400000 + rva - ro
    lo, hi = 0x485C90 - base, 0x487D10 - base          # the craft init functions
    FLAGS = bytes.fromhex('c78120010000'); MAX = bytes.fromhex('c781ec000000'); RATE = bytes.fromhex('c781f0000000')
    def find(pat):
        out, i = [], d.find(pat, lo, hi)
        while i != -1: out.append(i); i = d.find(pat, i + 1, hi)
        return out
    flags, maxes, rates = find(FLAGS), find(MAX), find(RATE)
    assert len(flags) == 14 and len(rates) == 14, (len(flags), len(rates))
    for k, f in enumerate(flags):
        end = flags[k + 1] if k + 1 < 14 else hi
        r = [x for x in rates if f < x < end]; m = [x for x in maxes if f < x < end]
        assert len(r) == 1 and len(m) == 1, hex(f + base)
        r, m = r[0], m[0]
        fl = struct.unpack_from('<I', d, f + 6)[0]; mx = struct.unpack_from('<f', d, m + 6)[0]
        rt = struct.unpack_from('<f', d, r + 6)[0]
        want = mx * 0.006
        print('init %08X flags %08X -> %08X  max %6.1f  rate %7.3f -> %6.3f' % (f + base, fl, fl | 0x40, mx, rt, want))
        if apply:
            struct.pack_into('<I', d, f + 6, fl | 0x40)
            if not (fl & 0x40): struct.pack_into('<f', d, r + 6, want)   # N-1 keeps its own rate
    # R2 sounds only for the N-1: the three damage-handler calls to the sound fn (thunk 0x40110E, thiscall,
    # 2 args, ret 8) go through a cave that checks cfg type [cfg+0x118] == 4 (cfg = [esp+0x14] in the handler
    # = [esp+0x20] inside the cave after 2 pushes + return address). Other craft: drop the 2 args, no sound.
    # The 25 % repair loss is kept for every craft.
    CAVE, SND = 0x48C120, 0x40110E
    def o(va): return va - base
    cave = bytearray(bytes.fromhex('8b442420' '80b81801000004'))           # mov eax,[esp+20]; cmp byte [eax+118],4
    cave += b'\x0f\x84' + struct.pack('<i', SND - (CAVE + len(cave) + 6))  # je 0x40110E
    cave += bytes.fromhex('c20800')                                         # ret 8
    done = d[o(CAVE):o(CAVE) + len(cave)] == cave
    if not done:
        assert d[o(CAVE):o(CAVE) + 24] == b'\xcc' * 24, 'cave area not free'
    for site in (0x48C0C0, 0x48C0CE, 0x48C10A):
        stock = b'\xe8' + struct.pack('<i', SND - (site + 5)); new = b'\xe8' + struct.pack('<i', CAVE - (site + 5))
        cur = bytes(d[o(site):o(site) + 5])
        assert cur in (stock, new), hex(site)
        print('sound call %08X: %s' % (site, 'already N-1 only' if cur == new else 'stock -> N-1 only'))
        if apply: d[o(site):o(site) + 5] = new
    if apply: d[o(CAVE):o(CAVE) + len(cave)] = cave
    if apply:
        open(path, 'wb').write(d); print('written', path)

main()
