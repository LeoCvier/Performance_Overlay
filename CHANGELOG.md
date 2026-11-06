# Changelog

This file records the published versions of PerformanceOverlay and the changes included in each release.

## [1.1.1] - 2026-11-06

- Replaced the external PresentMon process with in-process Windows ETW capture for DirectX presentation events.
- Track frame data for the focused application and its process tree, including supported desktop shell presentation activity.
- Keep FPS fields as dashes for five seconds after data goes stale, then hide them until new frame data arrives.
- Show the installed application version in the system tray menu.
- Write local diagnostic and error logs under `%LOCALAPPDATA%\\PerformanceOverlay\\Logs`.
- Rotate log files and remove the oldest logs when total log storage exceeds 1 GB.
- Bundle the required tracing components in the self-contained Windows x64 installer.

## [1.1.0] - 2026-11-05

- Added plugin support and the plugin API.
- Added plugin loading, visibility controls, and plugin discovery from the system tray.
- Added configurable overlay position, colors, and keyboard shortcuts.
- Added reload controls and improved startup and installer behavior.
- Added the system tray menu and automatic startup for the installing Windows account.

## [1.0.0] - Initial release

- Added the Windows performance overlay with FPS average, minimum and maximum, CPU, GPU, and RAM metrics.
- Added overlay color and position controls.
- Added a self-contained Windows x64 installer.

[1.1.1]: https://github.com/LeoCvier/Performance_Overlay/releases/tag/V1.1.1
[1.1.0]: https://github.com/LeoCvier/Performance_Overlay/releases/tag/V1.1.0
[1.0.0]: https://github.com/LeoCvier/Performance_Overlay/releases/tag/V1.0.0

