# Performance Overlay (Windows / WPF)

A lightweight Windows performance overlay for CPU, GPU, RAM and FPS monitoring.

## Requirements
- Windows 10/11 x64
- .NET 8 SDK
- `PresentMon.exe` (x64) next to the application for FPS capture
- The distributed installer can bundle PresentMon beside the application

## Build
Run these commands in the folder containing `PerformanceOverlay.csproj`:

```powershell
dotnet clean
Remove-Item -Recurse -Force bin,obj -ErrorAction SilentlyContinue
dotnet restore
dotnet build -c Release
```

Publish a standalone executable:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

Copy the x64 PresentMon console executable into the publish folder beside `PerformanceOverlay.exe` and name it `PresentMon.exe`.

## System tray
- Show overlay / Hide overlay
- Overlay color: Red, Green, Blue, Turquoise, Orange, Black, White
- Overlay position: Left, Center, Right (top edge of the primary display)
- Reload everything: restarts hardware sensor initialization and PresentMon, resets FPS min/max and refreshes metric visibility
- Configure hotkeys: customize the shortcuts for FPS reset, exit and full reload
- Help: explains the FPS, CPU, GPU, RAM, availability-dot and reload-border indicators
- Exit

The selected color, overlay position and custom hotkeys are saved in `%APPDATA%\PerformanceOverlay\settings.json`.

Default hotkeys:
- `Ctrl + Shift + R`: Reset FPS max/min
- `Ctrl + Alt + Q`: Exit overlay
- `Ctrl + Alt + L`: Reload everything

If a hotkey is already used by another program, it will not be registered. Choose a different combination in the tray menu.

## Overlay position
Choose Left, Center or Right from the tray menu. The overlay stays near the top edge and the selected position is remembered after restarting the application.

## Activity animations
- Purple moving border segment: full overlay reload in progress
- Green moving border segment: waiting for the first FPS data from PresentMon
- The green animation stops when FPS data arrives, or after 12 seconds if no data is received

## Sensor availability
Unavailable metrics are hidden. When new sensor data arrives, a green availability dot indicates that metrics are ready. A metric is hidden again after five minutes without fresh readings.

LibreHardwareMonitor supplies CPU/GPU sensors and Windows memory status supplies RAM usage. Sensor availability depends on hardware, firmware, drivers and permissions. PresentMon supplies FPS data.


## Overlay positions
Use the system tray menu **Overlay position** to choose **Top Left**, **Top Center**, or **Top Right**. The overlay remains aligned to the top edge of the Windows work area, and the selected position is saved between launches.


On exit, the overlay displays **Made by LeoCvier** for five seconds, then uses the existing closing wipe animation.


## Installer packaging
See `INSTALLER-README.md` and `Build-Installer.ps1` for the Windows x64 installer build. The installer requests administrator approval, registers elevated logon autostart, and removes that task on uninstall. A compatible x64 PresentMon console binary must be placed in `Dependencies\PresentMon.exe` before building.
