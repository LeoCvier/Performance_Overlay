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

- Requests administrator permission during installation (UAC).
- Installs the self-contained overlay to Program Files.
- The application manifest also requests administrator permission.
- Registers a Task Scheduler logon task for the installing Windows account with the highest run level, then starts it. This provides elevated autostart without showing a UAC prompt at every normal logon.
- Uninstall removes the scheduled task.

The scheduled task is per Windows user. If the account is not an administrator, Windows cannot run the overlay with administrator privileges merely because an administrator approved installation; use an administrator account for the intended elevated autostart.
