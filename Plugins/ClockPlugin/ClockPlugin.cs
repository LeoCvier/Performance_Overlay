using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using PerformanceOverlay.PluginApi;

namespace PerformanceOverlay.ClockPlugin;

public sealed class ClockPlugin : IOverlayPlugin
{
    private readonly TextBlock _view = new()
    {
        Foreground = Brushes.White,
        FontSize = 14,
        FontWeight = FontWeights.SemiBold,
        VerticalAlignment = VerticalAlignment.Center
    };

    private readonly DispatcherTimer _timer = new()
    {
        Interval = TimeSpan.FromSeconds(1)
    };

    public ClockPlugin()
    {
        _timer.Tick += UpdateClock;
        UpdateClock(this, EventArgs.Empty);
        _timer.Start();
    }

    public string Name => "Clock";
    public string Description => "Displays the local time in 24-hour format.";

    public FrameworkElement CreateView() => _view;

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= UpdateClock;
    }

    private void UpdateClock(object? sender, EventArgs e)
    {
        _view.Text = $"TIME {DateTime.Now:HH:mm:ss}";
    }
}
