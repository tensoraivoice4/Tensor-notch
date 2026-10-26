# SILENTREAPER Dynamic Notch

A Windows prototype of a MacBook-inspired top-centred dynamic notch.

## Features in this prototype
- Borderless top-centred notch with animated expand/collapse.
- Dark translucent, glass-inspired styling with layered highlights.
- Battery percentage and charging/AC status.
- CPU and physical RAM monitoring.
- Adaptive mode: hides over detected full-screen foreground windows.
- Does not require administrator permissions.

## Important limitations
This is not Apple's macOS implementation or its proprietary Liquid Glass renderer. The glass appearance is approximated with WPF gradients and highlights. Media controls, volume events, notifications, settings, advanced refraction and production-grade full-screen detection are not implemented yet. This prototype has not been validated on every Windows machine.

## Build locally
Install the .NET 8 SDK on Windows, open PowerShell in this folder, and run:

```powershell
dotnet publish .\SILENTREAPER.DynamicNotch.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o .\publish
```

Run `.\publish\SILENTREAPER.DynamicNotch.exe`.

## GitHub Actions
Push this folder to a GitHub repository. The workflow under `.github/workflows/build-windows.yml` builds a self-contained Windows x64 executable and uploads a ZIP artifact.

## Controls
- Single-click the notch to expand.
- Click the collapse arrow to close the expanded panel.
- It hides over detected full-screen foreground windows and reappears when you return to normal windows.

## Safety
The app does not change boot settings, partitions, BitLocker, or other projects. Review source code before running binaries from public repositories.
