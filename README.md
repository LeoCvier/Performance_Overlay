# PerformanceOverlay

A Windows overlay that keeps important PC performance metrics visible while you play or work.

## What it shows

- **FPS:** current average frame rate and the highest and lowest values for the current session
- **CPU:** utilization and temperature, when reported by your system
- **GPU:** utilization and temperature, when reported by your system
- **RAM:** current memory usage

After three seconds without frame data, FPS AVG changes to an em dash while MAX and MIN keep their last values. After five seconds, the entire FPS category is hidden. Capture errors show an em dash instead of an error message.

## Installation

Download `PerformanceOverlay-Setup-x64.exe` from **Releases** and run it. The setup installs the application and includes the required components, including PresentMon. You do **not** need to install the .NET runtime/SDK or PresentMon separately.

Windows User Account Control will ask for administrator approval during installation. The application also runs with elevated permissions for hardware and FPS capture. The setup configures the application to start automatically when the installing Windows account signs in. That account must have administrator privileges for elevated startup.

## How to use it

After installation, the overlay starts automatically when you sign in. It stays above other windows along the top edge of the screen. Use the icon in the Windows notification area to access these options:

- **Show overlay / Hide overlay:** show or hide the overlay
- **Overlay position:** place it at the top left, top center, or top right
- **Overlay color:** choose an accent color
- **Configure hotkeys:** change the keyboard shortcuts
- **Plugins:** load trusted external .NET plugins and show or hide their overlay views
- **Reload everything:** restart the sensors and FPS capture, and reset the session's minimum and maximum FPS
- **Exit:** close the application

Default hotkeys:

- `Ctrl + Shift + R`: reset minimum and maximum FPS
- `Ctrl + Alt + Q`: exit the application
- `Ctrl + Alt + L`: reload everything

If another program is already using a hotkey, it cannot be registered. Choose a different combination in the notification area menu.

The selected position, color, and hotkeys are saved in `%APPDATA%\PerformanceOverlay\settings.json`.

## How monitoring works

The overlay reads CPU and GPU sensors through LibreHardwareMonitor and gets RAM usage from Windows. PresentMon captures the frame rate of supported applications. Sensor availability depends on your hardware, firmware, drivers, and permissions, so some metrics may not be available on every system.

FPS is reported per supported foreground app, not as one combined value for the whole desktop. The minimum and maximum track the current session. A green animated border appears while FPS capture is waiting for data. A purple border appears while **Reload everything** is running.

Plugins are stored under `%LOCALAPPDATA%\PerformanceOverlay\Plugins` and load automatically when the application starts. See [PLUGINS.md](PLUGINS.md) for the plugin interface and installation details.
