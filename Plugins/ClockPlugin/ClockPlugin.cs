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
        VerticalAlignment = VerticalAlignment.Center
    };
    private readonly System.Windows.Documents.Run _label = new("TIME ")
    {
        FontWeight = FontWeights.SemiBold
    };
    private readonly System.Windows.Documents.Run _time = new()
    {
        Foreground = Brushes.White,
        FontWeight = FontWeights.SemiBold
    };

    private readonly DispatcherTimer _timer = new()
    {
        Interval = TimeSpan.FromSeconds(1)
    };

    public ClockPlugin()
    {
        _view.Inlines.Add(_label);
        _view.Inlines.Add(_time);
        _timer.Tick += UpdateClock;
        UpdateClock(this, EventArgs.Empty);
        _timer.Start();
    }

    public string Name => "Clock";
    public string Description => "Displays the local time in 24-hour format.";

    public FrameworkElement CreateView()
    {
        _label.SetResourceReference(
            System.Windows.Documents.TextElement.ForegroundProperty,
            OverlayTheme.AccentBrushKey);
        return _view;
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= UpdateClock;
    }

    private void UpdateClock(object? sender, EventArgs e)
    {
        _time.Text = DateTime.Now.ToString("HH:mm:ss");
    }
}
