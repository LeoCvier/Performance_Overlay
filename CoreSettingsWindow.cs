using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PerformanceOverlay.PluginApi;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfFontFamily = System.Windows.Media.FontFamily;

namespace PerformanceOverlay;

public partial class MainWindow
{
    private void OpenCoreSettings()
    {
        if (_isStopping) return;
        if (_coreSettingsWindow is { IsVisible: true })
        {
            _coreSettingsWindow.Activate();
            return;
        }

        _coreSettingsWindow = new CoreSettingsWindow(this) { Owner = this };
        _coreSettingsWindow.Closed += (_, _) => _coreSettingsWindow = null;
        _coreSettingsWindow.Show();
    }

    private sealed class CoreSettingsWindow : Window
    {
        private readonly MainWindow _host;
        private readonly System.Windows.Controls.ComboBox _color = new();
        private readonly System.Windows.Controls.ComboBox _position = new();
        private readonly StackPanel _plugins = new();

        public CoreSettingsWindow(MainWindow host)
        {
            _host = host;
            Title = "Performance Overlay Settings";
            Width = 470;
            Height = 600;
            MinHeight = 480;
            ResizeMode = ResizeMode.CanResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(WpfColor.FromRgb(25, 29, 36));
            Foreground = WpfBrushes.White;
            FontFamily = new WpfFontFamily("Segoe UI");

            var content = new StackPanel { Margin = new Thickness(24) };
            Content = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            content.Children.Add(new TextBlock { Text = "Performance Overlay", FontSize = 24, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
            content.Children.Add(new TextBlock { Text = "Appearance and behavior", Foreground = new SolidColorBrush(WpfColor.FromRgb(173, 181, 192)), Margin = new Thickness(0, 0, 0, 18) });

            AddLabel(content, "Overlay color");
            _color.ItemsSource = new[] { "Turquoise", "Red", "Green", "Blue", "Orange", "Black", "White" };
            _color.SelectedItem = ColorName(_host._accentColor);
            _color.SelectionChanged += (_, _) =>
            {
                if (_color.SelectedItem is string name) _host.SetAccentColor(ColorFromName(name));
            };
            content.Children.Add(_color);

            AddLabel(content, "Position");
            _position.ItemsSource = new[] { "Top Left", "Top Center", "Top Right" };
            _position.SelectedItem = _host._overlayPosition;
            _position.SelectionChanged += (_, _) =>
            {
                if (_position.SelectedItem is string placement) _host.SetOverlayPosition(placement);
            };
            content.Children.Add(_position);

            AddLabel(content, "Plugins");
            RefreshPluginList();
            content.Children.Add(_plugins);
            var pluginActions = new WrapPanel { Margin = new Thickness(0, 8, 0, 4) };
            pluginActions.Children.Add(MakeButton("Load plugin…", () =>
            {
                _host.LoadPluginFromDialog();
                RefreshPluginList();
            }));
            pluginActions.Children.Add(MakeButton("Discover plugins", OpenPluginCatalog));
            pluginActions.Children.Add(MakeButton("Open plugins folder", () => _host.OpenPluginFolder()));
            content.Children.Add(pluginActions);

            var actions = new WrapPanel { Margin = new Thickness(0, 18, 0, 0) };
            actions.Children.Add(MakeButton("Configure hotkeys…", () => _host.ShowHotkeySettings()));
            actions.Children.Add(MakeButton("Reload everything", async () => await _host.ReloadAllAsync()));
            actions.Children.Add(MakeButton("Help", () => _host.ShowIndicatorHelp()));
            actions.Children.Add(MakeButton(_host.IsVisible ? "Hide overlay" : "Show overlay", () =>
            {
                if (_host.IsVisible) _host.HideOverlayToTray(); else _host.ShowOverlayFromTray();
            }));
            content.Children.Add(actions);
        }

        private void RefreshPluginList()
        {
            _plugins.Children.Clear();
            if (_host._loadedPlugins.Count == 0)
            {
                _plugins.Children.Add(new TextBlock { Text = "No plugins loaded", Foreground = new SolidColorBrush(WpfColor.FromRgb(173, 181, 192)) });
                return;
            }
            foreach (var plugin in _host._loadedPlugins)
            {
                var check = new System.Windows.Controls.CheckBox
                {
                    Content = plugin.Instance.Name,
                    ToolTip = plugin.Instance.Description,
                    IsChecked = _host._pluginContainers.TryGetValue(plugin, out var view) && view.Visibility == Visibility.Visible,
                    Margin = new Thickness(0, 4, 0, 4)
                };
                check.Checked += (_, _) => SetPluginVisibility(plugin, true);
                check.Unchecked += (_, _) => SetPluginVisibility(plugin, false);
                _plugins.Children.Add(check);
            }
        }

        private void SetPluginVisibility(LoadedOverlayPlugin plugin, bool visible)
        {
            if (_host._pluginContainers.TryGetValue(plugin, out var view)) view.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        private static void AddLabel(System.Windows.Controls.Panel panel, string text) => panel.Children.Add(new TextBlock
        {
            Text = text,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 12, 0, 6)
        });

        private static System.Windows.Controls.Button MakeButton(string text, Action action)
        {
            var button = new System.Windows.Controls.Button { Content = text, Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0, 0, 8, 8), MinWidth = 90 };
            button.Click += (_, _) => action();
            return button;
        }

        private static WpfColor ColorFromName(string name) => name switch
        {
            "Red" => Colors.Red,
            "Green" => Colors.LimeGreen,
            "Blue" => Colors.DodgerBlue,
            "Orange" => Colors.DarkOrange,
            "Black" => Colors.Black,
            "White" => Colors.White,
            _ => WpfColor.FromRgb(53, 221, 240)
        };

        private static string ColorName(WpfColor color) =>
            color == Colors.Red ? "Red" : color == Colors.LimeGreen ? "Green" :
            color == Colors.DodgerBlue ? "Blue" : color == Colors.DarkOrange ? "Orange" :
            color == Colors.Black ? "Black" : color == Colors.White ? "White" : "Turquoise";
    }
}
