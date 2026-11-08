# Star Wars Episode I: Battle for Naboo (PC) – Mod Installer

Optional fixes and improvements for the PC version of **Star Wars Episode I: Battle for Naboo**: Hor+ widescreen at any aspect ratio, aim assist removal for flying craft, an orange single-ring crosshair with lasers aimed through it, self-repair for every craft, a fix for vehicles stuck at a standstill above 60 fps, a higher ceiling on Borvo the Hutt, platinum requirements on the results screen, and faster Mission Select. Each option can be turned on or off on its own, and the installer keeps your original exe so you can change or undo anything later.

**This is a patch only.** No game files are included. You need your own copy of the game.

## Download

Get `BattleForNabooMods-v1.2.zip` from the [**Releases**](https://github.com/Ma77i7yahu/battle-for-naboo-pc-fixes/releases/latest) page. It holds the installer, its README and `PATCHES.txt`. `SHA256SUMS.txt` on the release page lets you check your download.

Full instructions, requirements and troubleshooting: [docs/README-installer.md](docs/README-installer.md).

## The options

| Option | Status |
|---|---|
| Hor+ widescreen (any aspect ratio) | Tested at 1920x1080 (16:9); 16:10, 21:9 and 32:9 built the same way but not yet played |
| Remove laser aim assist from flying craft | Tested |
| Crosshair and laser aim for flying craft (replaces the option above) | Tested in two flying craft |
| Every craft repairs itself (the N-1's self-repair for every craft; R2 sounds stay on the N-1) | Played in several craft; not checked craft by craft |
| Higher flight ceiling on Borvo the Hutt | Tested |
| Fix vehicles stuck at a standstill above 60 fps | Tested at 119 fps |
| Show the platinum requirements once you have gold | Tested |
| Mission Select: one mission per key press | Tested |
| Mission Select: no sound when changing mission | Not yet tried in game |

The installer needs the original `data_pc\bfn.exe` (MD5 `64F8B9C0F356C31D64C51455A2B0CF23`). For widescreen resolutions, Chip's 64-bit installer port of the game with the ChipResolution plugin is the easiest setup; the installer sets the resolution for you if it finds it.

## What's in this repo

- `docs/README-installer.md`: the README that comes with the installer.
- `PATCHES.txt`: every byte the installer changes, with its address and disassembly.
- `src/`: the installer's C# source and the patch generator. See [BUILDING.md](BUILDING.md).
- `test/`: the test harness used to check the installer's output.

## Licence

The code, patches and documentation here are under the [MIT licence](LICENSE): reuse or port them freely, with credit. The game itself isn't covered.

## Credits

Thanks to Chip (64-bit installer port and ChipResolution), UCyborg (D3D resolution hack) and Elisha (D3D9 wrapper), whose work makes the game run well on modern Windows.

## Disclaimer

Unofficial fan-made mods, not affiliated with or endorsed by Lucasfilm, LucasArts, Disney or Factor 5. Star Wars is a trademark of its owners. No game files are distributed here. Use at your own risk, and keep the backup the installer makes.
