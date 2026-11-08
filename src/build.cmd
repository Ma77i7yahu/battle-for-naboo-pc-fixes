@echo off
rem Rebuilds PatchData.cs + ..\dist\PATCHES.txt from the tested exes, then the installer.
rem Usage: build.cmd ["path to Battle for Naboo data_pc holding bfn_original.exe and the tested variants"]
cd /d "%~dp0"
G:\msys64\mingw64\bin\python.exe -I gen.py %1 || exit /b 1
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /optimize /out:..\dist\BattleForNabooModInstaller.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll Installer.cs PatchData.cs || exit /b 1
