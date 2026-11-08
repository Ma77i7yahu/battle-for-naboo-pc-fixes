# Star Wars Episode I: Battle for Naboo (PC) – Mod Installer

A small installer that applies optional fixes and improvements to Battle for Naboo. It only
changes bytes in the game's own files, contains none of the game's files, and keeps your
original exe, so you can change or remove any option later.

## What it can install

| Option | What it does | Status |
|---|---|---|
| **Hor+ widescreen** | The game hard-codes a 4:3 picture, so at a widescreen resolution everything is stretched and the sky smears when you turn. This patch changes the 3D view to your screen's shape by adding width at the sides. The vertical view stays the same as the original. It also widens level culling (distant terrain and water no longer vanish at the edges), generates terrain across the full width (no jagged missing strips), and puts the radar map back inside its ring. | Tested at 1920x1080 (16:9). 16:10, 21:9 and 32:9 are built the same way but haven't been played. |
| **Remove laser aim assist** | Every craft bends its lasers toward enemies near the nose. With this on, shots go where the crosshair points. Missile and torpedo lock-on are unchanged. | Tested |
| **Higher flight ceiling on Borvo the Hutt** | That mission's ceiling is far lower than every other mission's. It's raised from -70 to -200 (in this game, more negative is higher), so you can climb about 2.5 times as high. This edits 4 bytes of `data.bin`. | Tested |
| **Fix vehicles stuck at a standstill above 60 fps** | Above 60 fps your speeder or ship can sit still at full throttle, sometimes for 10 seconds or more, until a stutter gets it going. The game zeroes any velocity that would move a vehicle less than 0.001 units in one frame, and at high frame rates the first push of thrust is smaller than that. Above about 61 fps that cut-off is skipped. At 60 fps and below the game is unchanged. With this on you no longer need to cap the game at 60 fps. | Tested at 119 fps |
| **Show the platinum requirements once you have gold** | The results screen normally hides the "NEEDED FOR ..." column once you have gold. With this on, it shows a "NEEDED FOR PLATINUM" column instead. | Tested |
| **Mission Select: one mission per key press** | Mission Select opens on your latest mission, and each step plays a 0.8-second slide during which Left and Right are ignored. With this on, every press moves one mission straight away, even mid-slide, so you can tap back to mission 1 quickly. Holding a key still moves only once. | Tested |
| **Mission Select: no sound when changing mission** | Removes the page-turn sound on every step through the mission list. The sound at either end of the list is kept. | Not yet tried in game |

Known limit: the 2D HUD and menus are still drawn at 4:3 and stretched.

## Requirements

- Battle for Naboo for PC, with the original `data_pc\bfn.exe`
  (MD5 `64F8B9C0F356C31D64C51455A2B0CF23`). If your exe is different, the installer will say so and won't change anything.
- For widescreen you need to run the game at a widescreen resolution. The easiest way is
  **Chip's 64-bit installer port** of the game, which includes the ChipResolution plugin
  (`data_pc\plugins\ChipResolution.ini`), UCyborg's D3D resolution hack and Elisha's D3D7→D3D9
  wrapper. If ChipResolution is installed, this installer sets the resolution for you.
- Windows with .NET Framework 4 (included in Windows 8, 10 and 11).

## How to install

1. Close the game.
2. Run `BattleForNabooModInstaller.exe`. You can put it in the game folder, or start it from anywhere and click **Browse...** to find the game folder (either the main folder or `data_pc`).
3. Tick the options you want. Point at an option to see what it does. If you tick widescreen, also pick your screen resolution.
4. Click **Install selected**.

Your untouched exe is saved as `data_pc\bfn_original.exe`. To change options later, run the
installer again: it shows what's installed now and rebuilds the exe from that backup.
**Restore original game** puts the original exe and the stock Borvo ceiling back.

If Windows refuses access to the game folder (for example under `C:\Program Files`), right-click
the installer and choose **Run as administrator**.

## Troubleshooting

- **Still no widescreen, or the picture is stretched again.** The patch is built for the
  aspect ratio you picked, so if you change resolution, run the installer again and pick the new one.
  If the game starts at 640x480, it's probably running as administrator (for example when started
  through the `BattleforNaboo.exe` launcher or the disc autorun). Then it reads its settings from a
  different place in the registry from the one ChipResolution writes to. Start `data_pc\bfn.exe`
  directly instead, or copy the `Screen` value from
  `HKEY_CURRENT_USER\Software\Classes\VirtualStore\MACHINE\SOFTWARE\WOW6432Node\LucasArts Entertainment Company LLC\Battle for Naboo\Retail`
  to the matching key under `HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node`.
- **Your ship won't speed up (no thrust).** The original game breaks above 60 fps. Install the
  **Fix vehicles stuck at a standstill above 60 fps** option, or cap the game at 60 fps. Chip's port
  caps it with `OverrideRefreshRate = 60` in `data_pc\plugins\dxwrapper.ini`. With the fix installed
  you can set that to `0` to run at your monitor's refresh rate.
- **The game crashes when you alt-tab.** That happens in the original game too. Avoid alt-tab while playing.
- **The game sometimes crashes on exit.** Also an original-game issue, and harmless.

## For the curious

`PATCHES.txt` lists every byte the installer changes, with its address and disassembly.
On the widescreen option, six values scale with the aspect ratio
(stock value × aspect ÷ 4/3). The installer computes them for the resolution you pick.

## Credits

Patches made by reverse engineering `bfn.exe`. Thanks to Chip (64-bit installer port and ChipResolution),
UCyborg (D3D resolution hack) and Elisha (D3D9 wrapper), whose work makes the game run well on modern Windows.
