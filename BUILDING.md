# Building

You don't need any of this to install the mods. It's for anyone who wants to rebuild the installer or port the patches.

## Installer only

`src/PatchData.cs` already holds every patch, so the installer builds with the C# compiler that ships with Windows (.NET Framework 4):

```
cd src
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /optimize /out:BattleForNabooModInstaller.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll Installer.cs PatchData.cs
```

## Regenerating the patches

`src/build.cmd` runs `gen.py` first, which rebuilds `PatchData.cs` and `PATCHES.txt` by comparing a set of tested, hand-patched exes against the original `bfn.exe`. Those exes are modified copies of the game's exe, so they aren't in this repo. `gen.py` takes the `data_pc` folder holding them as its argument. `build.cmd` expects Python from MSYS2's MinGW-w64 at `G:\msys64`, so change that path to suit your machine. Two later changes were made to `PatchData.cs` by hand, so keep them if you regenerate: the aim-assist option was cut down to the flying craft's entries in the cone table, and the crosshair option's bytes come from `gunsight.py`, which assembles `gunsight.s` (a code cave at 0x403000) with MSYS2's binutils and applies it to the original exe, and the repair option's bytes come from `all_craft_repair.py` applied to the original exe (`python all_craft_repair.py bfn.exe apply`). The tested exes `gen.py` compared against no longer exist, so for now `PatchData.cs` itself is the reference: build the installer with the `csc` line above.

## Tests

`test/harness.cs` loads the installer by reflection and checks its output byte for byte against the tested exes, plus install, rebuild and restore. It needs a scratch copy of the game folder. `test/shot.cs` renders the installer window to a PNG. The harness predates the crosshair and repair options and the flying-craft-only aim-assist change; those were checked separately against the patched exes they came from.
