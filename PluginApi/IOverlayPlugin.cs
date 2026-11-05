using System.Windows;

namespace PerformanceOverlay.PluginApi;

/// <summary>Resource keys supplied by the overlay for plugins to match its theme.</summary>
public static class OverlayTheme
{
    public const string AccentBrushKey = "PerformanceOverlay.AccentBrush";
}

/// <summary>
/// Implement this interface in a .NET class library to add a view to the overlay.
/// </summary>
public interface IOverlayPlugin : IDisposable
{
    string Name { get; }
    string Description { get; }

    /// <summary>
    /// Creates the WPF element shown in the performance overlay.
    /// </summary>
    FrameworkElement CreateView();
}
