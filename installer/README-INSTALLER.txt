HeadphoneHub installer (NSIS)

1. Install NSIS 3 from https://nsis.sourceforge.io (makensis.exe).
2. From the project folder run:
     powershell -ExecutionPolicy Bypass -File installer\build-installer.ps1
   Optionally pass a build number: -Build 3
   For 32-bit Windows (x86) add: -Arch x86   (default is x64)
   It publishes the app self-contained (the .NET runtime is inside, nothing to install
   separately) and builds artifacts\HeadphoneHub-x64-Setup.exe (or HeadphoneHub-x86-Setup.exe with -Arch x86).

What the installer does
  - Installs to C:\Program Files\HeadphoneHub (x86 build: Program Files (x86) on 64-bit Windows;
    needs administrator rights).
  - Start menu shortcut, optional desktop shortcut, entry in "Apps & features".
  - Installer, shortcuts and the .exe use the white-on-black headphone icon (Assets\headphonehub.ico);
    while the app is running, its window/taskbar icon recolors the headphones to whichever accent
    color the user picks (AppIcon.cs).
  - Silent install: HeadphoneHub-Setup-buildN.exe /S
  - Settings are not installed: HeadphoneHub creates %LocalAppData%\HeadphoneHub\settings.json
    by itself with its defaults on first start.

Uninstall: stops HeadphoneHub if it's running, removes the program folder, and asks whether
to delete %LocalAppData%\HeadphoneHub (output device settings, volumes, theme, accent color).

Portable build (no installer)
  powershell -ExecutionPolicy Bypass -File installer\build-portable.ps1 [-Arch x86]
  Produces artifacts\HeadphoneHub-Portable-<arch>.zip: a single self-contained HeadphoneHub.exe plus
  portable.txt. Unzip anywhere (USB stick included) and run it. While portable.txt sits next to the
  exe, settings go to a "data" folder beside it instead of %AppData%; delete portable.txt to switch back.
