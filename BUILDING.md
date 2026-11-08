# Building

You don't need any of this to install the mods. It's for anyone who wants to rebuild the installer or port the patches.

## Installer only

`src/PatchData.cs` already holds every patch, so the installer builds with the C# compiler that ships with Windows (.NET Framework 4):

```
cd src
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /optimize /out:BattleForNabooModInstaller.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll Installer.cs PatchData.cs
```

## Regenerating the patches

`src/build.cmd` runs `gen.py` first, which rebuilds `PatchData.cs` and `PATCHES.txt` by comparing a set of tested, hand-patched exes against the original `bfn.exe`. Those exes are modified copies of the game's exe, so they aren't in this repo. `gen.py` takes the `data_pc` folder holding them as its argument. `build.cmd` expects Python from MSYS2's MinGW-w64 at `G:\msys64`, so change that path to suit your machine.

## Tests

`test/harness.cs` loads the installer by reflection and checks its output byte for byte against the tested exes, plus install, rebuild and restore. It needs a scratch copy of the game folder. `test/shot.cs` renders the installer window to a PNG.
