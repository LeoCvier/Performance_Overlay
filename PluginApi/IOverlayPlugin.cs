using System.Windows;

namespace PerformanceOverlay.PluginApi;

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
