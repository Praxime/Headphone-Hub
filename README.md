# <img src="https://github.com/Praxime/Headphone-Hub/releases/download/1.0.0/headphonehub.ico" width="32" height="32"> | Headphone Hub [![forthebadge](https://forthebadge.com/images/badges/made-with-c-sharp.svg)](https://forthebadge.com)
HeadphoneHub is a small Windows app that plays your PC's audio through **several headphones or speakers at the same time**, for example Bluetooth earbuds together with wired headphones or a speaker. Every output gets its own volume and its own sync delay, so a slow Bluetooth connection can be lined up with a fast wired one.

It's compact, animated, has light and dark mode, six accent colors, and works in **English** and **Farsi (فارسی)**.

<p align="center">
  <img src="https://github.com/Praxime/Headphone-Hub/releases/download/1.0.0/app-showcase.png" width="340" alt="HeadphoneHub screenshot">
</p>

# ✨ | Features
- **Share audio** - one switch plays whatever Windows is playing to every output you turned on
- **Per-device volume** - 0 to 100 for each output
- **Per-device sync delay** - 0 to 1000 ms, to line up Bluetooth with wired devices
- **Bluetooth battery** - shown next to each device when Windows reports it
- **Device list** - outputs are labelled Bluetooth or Wired, and virtual devices can be hidden
- **Live spectrum** - a small visualizer that shows sound is flowing
- **Themes** - light, dark, or follow Windows (default), plus blue, red, green, yellow, orange and pink accents
- **Dynamic app icon** - the window and taskbar icon takes the color of the selected accent
- **Remembers everything** - devices, volumes, delays, theme, accent and language are saved between launches
- **Installer or portable** - installers for 64-bit and 32-bit Windows, plus a portable 64-bit build

# 🎧 | How it works
Windows' default output plays the sound directly. HeadphoneHub listens to that sound and mirrors it to every other output you enabled, each with its own volume and delay.

**For the best sync, make your slowest device (usually Bluetooth) the Windows default output.** Then use the delay slider on the faster devices to catch up with it.

# 📥 | Download
Grab the latest build from the [Releases](https://github.com/Praxime/HeadphoneHub/releases) page:

| File | What it is |
| --- | --- |
| `HeadphoneHub-x64-Setup.exe` | Installer for 64-bit Windows |
| `HeadphoneHub-x86-Setup.exe` | Installer for 32-bit Windows |
| `HeadphoneHub-Portable-x64.zip` | Portable, no install, 64-bit |

**Requirements:** Windows 10 (version 2004 / build 19041) or newer. Nothing else to install, the .NET runtime is included in every build.

# 🚀 | Using it
1. Open HeadphoneHub
2. Turn on the outputs you want to listen on
3. Set a volume for each one
4. If a device is late or early, adjust its **Delay (sync)**
5. Turn on **Share audio**

Use the sun / moon / monitor button for light, dark or system theme, the colored dots to change the accent, and the language switch for English or Farsi.

# 💾 | Where settings are saved
| Version | Location |
| --- | --- |
| Installed | `%AppData%\HeadphoneHub\settings.json` |
| Portable | `data\settings.json` next to `HeadphoneHub.exe` |

The portable build switches on with a `portable.txt` file next to the exe. Delete that file and it uses `%AppData%` like the installed version. If the app ever crashes, the error is written to `crash.log` in the same folder.

# 🛠️ | Build it yourself
You need the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0). For installers you also need [NSIS 3](https://nsis.sourceforge.io).

Run from source:
```
dotnet run
```

Build the installer (output goes to the `artifacts` folder):
```
powershell -ExecutionPolicy Bypass -File installer\build-installer.ps1
powershell -ExecutionPolicy Bypass -File installer\build-installer.ps1 -Arch x86
```
This gives you `HeadphoneHub-x64-Setup.exe` and `HeadphoneHub-x86-Setup.exe`. Add `-Build 3` to put a build number inside the installer.

Build the portable zip:
```
powershell -ExecutionPolicy Bypass -File installer\build-portable.ps1
powershell -ExecutionPolicy Bypass -File installer\build-portable.ps1 -Arch x86
```

If NSIS says `Can't open output file`, an old installer with the same name is still open or locked. Close it, delete it from `artifacts`, and run the command again.

# 🗑️ | Uninstall
Use **Apps & features** in Windows settings, or run `Uninstall.exe` from the install folder. It asks if you also want to delete your saved settings. For the portable build just delete the folder.

# 🧰 | Built with
- C# / .NET 8 / WPF
- [NAudio](https://github.com/naudio/NAudio) for audio capture and playback
- [Vazirmatn](https://github.com/rastikerdar/vazirmatn) font for Farsi (SIL Open Font License)
- [NSIS](https://nsis.sourceforge.io) for the installer

# ❤️ | Support
Support me by giving a little **star** to this Repo.
