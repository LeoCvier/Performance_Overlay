# PerformanceOverlay plugins

PerformanceOverlay loads trusted .NET plugin assemblies that implement `IOverlayPlugin` from `PerformanceOverlay.PluginApi`.

## Install and manage plugins

1. Open the PerformanceOverlay icon in the Windows notification area.
2. Select **Plugins → Load plugin…** and choose the plugin's main `.dll` file.
3. Confirm the trust notice. The application copies the plugin and its nearby `.dll`, `.json`, and `.config` dependencies to `%LOCALAPPDATA%\PerformanceOverlay\Plugins` and loads it immediately.

Each plugin is shown as a checked menu item. Uncheck it to hide its view from the overlay, or check it to show it again. Plugins in the plugin folder are loaded automatically the next time PerformanceOverlay starts. Use **Plugins → Open plugins folder** to inspect the installed files.

Plugins run inside PerformanceOverlay and have the same permissions as the application. The current installer runs the application with administrator privileges, so only load plugin files from sources you trust.

## Plugin contract

Create a .NET 8 Windows class library and reference `PluginApi\PerformanceOverlay.PluginApi.csproj`. The assembly must contain exactly one public, non-abstract class with a public parameterless constructor that implements `IOverlayPlugin`:

```csharp
using System.Windows;
using System.Windows.Controls;
using PerformanceOverlay.PluginApi;

public sealed class ExamplePlugin : IOverlayPlugin
{
    public string Name => "Example";
    public string Description => "A sample overlay view.";

    public FrameworkElement CreateView() => new TextBlock
    {
        Text = "Plugin loaded",
        Foreground = System.Windows.Media.Brushes.White,
        VerticalAlignment = VerticalAlignment.Center
    };

    public void Dispose() { }
}
```

Build the plugin and select its main DLL from the build output folder. The plugin loader copies the DLL and dependency files into its own folder. `CreateView()` should return a WPF element sized to fit the horizontal overlay. Use `Dispose()` to stop timers or release resources when the application exits.
