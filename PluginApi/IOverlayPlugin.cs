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

/// <summary>Lets a plugin participate in core metric visibility without racing the host refresh loop.</summary>
public interface IOverlayMetricVisibilityFilter
{
    bool IsMetricEnabled(string metricKey);
}

/// <summary>Allows a plugin to supply the overlay's top-left position after the host computes its default.</summary>
public interface IOverlayPlacementProvider
{
    bool TryGetPosition(double overlayWidth, double overlayHeight, out double left, out double top);
}
