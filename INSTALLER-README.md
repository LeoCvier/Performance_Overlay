# Building the PerformanceOverlay installer

This package is prepared for Windows x64. It creates a self-contained .NET publish and compiles a standard Windows setup executable with Inno Setup 6. FPS capture reads Windows DirectX presentation events in an in-process ETW session.

## Build on Windows

Requirements:
- Windows 10/11 x64
- .NET 8 SDK
- Inno Setup 6

Run PowerShell in this folder:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\Build-Installer.ps1
```

The setup executable is written to `Output\PerformanceOverlay-Setup-x64.exe`.

## Installer behavior

- Installs the self-contained overlay in the current user's Programs folder without requesting elevation.
- Runs with the signed-in user's permissions (`asInvoker`).
- Adds a per-user Windows startup entry and removes the older elevated scheduled task when upgrading.
- Uninstall removes the scheduled task.

Hardware sensor and ETW access can vary by Windows configuration when the process is not elevated.
