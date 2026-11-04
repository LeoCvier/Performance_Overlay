# Building the PerformanceOverlay installer

This package is prepared for Windows x64. It creates a self-contained .NET publish, bundles the PresentMon console executable beside the overlay, and compiles a standard Windows setup executable with Inno Setup 6.

## Important dependency step

Place the **x64 PresentMon console executable named `PresentMon.exe`** in `Dependencies\PresentMon.exe` before building. The current overlay launches the legacy console interface with `--output_stdout --no_console_stats --no_track_gpu --v1_metrics --restart_as_admin`; use a PresentMon console build that supports those arguments. Do not substitute the newer graphical PresentMon app executable.

PresentMon is maintained by Intel's GameTechDev project. Official releases: https://github.com/GameTechDev/PresentMon/releases

## Build on Windows

Requirements:
- Windows 10/11 x64
- .NET 8 SDK
- Inno Setup 6
- Compatible x64 `PresentMon.exe` at `Dependencies\PresentMon.exe`

Run PowerShell in this folder:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\Build-Installer.ps1
```

The setup executable is written to `Output\PerformanceOverlay-Setup-x64.exe`.

## Installer behavior

- Requests administrator permission during installation (UAC).
- Installs the self-contained overlay and bundled PresentMon to Program Files.
- The application manifest also requests administrator permission.
- Registers a Task Scheduler logon task for the installing Windows account with the highest run level, then starts it. This provides elevated autostart without showing a UAC prompt at every normal logon.
- Uninstall removes the scheduled task.

The scheduled task is per Windows user. If the account is not an administrator, Windows cannot run the overlay with administrator privileges merely because an administrator approved installation; use an administrator account for the intended elevated autostart.
