# Changelog

This file records the published versions of PerformanceOverlay and the changes included in each release.

## [1.3.1] - 2026-11-22

- Redesign the settings window to match the website and overlay with a unified dark palette, accent highlights, responsive cards and clearer tabs.
- Replace default selection controls with matching custom dropdowns and style sliders, checkboxes and shortcut fields.
- Improve page width and value labels, and localize the updated settings in all five menu languages.
## [1.3.0] - 2026-11-22

- Move all Customizable Overlay controls into the core settings window, including accent color, scale, hover fade and distance, monitor selection, metric visibility and ordering.
- Import existing Customizable Overlay settings into core settings and stop loading the retired plugin to prevent duplicate behavior.
- Replace the custom settings sidebar with a standard resizable window and tabs.
## [1.2.0] - 2026-11-15

- Rework the settings window with responsive sizing and functional General, Plugins, Shortcuts and Help pages.
- Add persistent menu translations in English, German, French, Japanese and Simplified Chinese.
- Let plugins expose their settings from the Plugins page; update the Customizable Overlay plugin accordingly.
- Route tray Help and hotkey actions to their matching settings pages.
- Preserve overlay metric labels and values across menu language changes.

## [1.1.7] - 2026-11-15

- Add in-app Help and Shortcuts pages with clearer guidance and direct hotkey editing.
- Route tray Help and Configure hotkeys actions to their matching settings pages.

## [1.1.6] - 2026-11-15

- Add French, Japanese and Simplified Chinese as menu languages.
- Localize the Customizable Overlay settings in the added languages.

## [1.1.5] - 2026-11-15

- Add persistent English and German language selection for settings, tray menus, hotkey and help dialogs.
- Localize the Customizable Overlay plugin menu and settings dialog based on the selected menu language.

## [1.1.4] - 2026-11-15

- Fit the settings window to the available work area and allow its content to scroll on shorter displays.
- Add a functional Plugins page with per-plugin settings buttons.
- Add an optional plugin settings entry point to the plugin API.

## [1.1.3] - 2026-11-13

- Open a consolidated settings window from a left-click on the tray icon.
- Preserve the overlay's current screen position for the closing animation.
- Apply plugin metric visibility within the core refresh loop to prevent disabled values from flashing.
- Let plugins provide monitor and corner placement through the plugin API.
- Run with the signed-in user's permissions and use a per-user installer and startup entry.
- Remove the elevated scheduled-task startup and PowerShell execution-policy bypass from the installer.

## [1.1.2] - 2026-11-06

- Removed the animated frame around the overlay during FPS capture and reloads.
- Kept FPS values and local error logging from v1.1.1.

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

[1.1.2]: https://github.com/LeoCvier/Performance_Overlay/releases/tag/V1.1.2
[1.1.1]: https://github.com/LeoCvier/Performance_Overlay/releases/tag/V1.1.1
[1.1.0]: https://github.com/LeoCvier/Performance_Overlay/releases/tag/V1.1.0
[1.0.0]: https://github.com/LeoCvier/Performance_Overlay/releases/tag/V1.0.0



