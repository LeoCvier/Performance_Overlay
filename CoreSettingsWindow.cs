using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PerformanceOverlay.PluginApi;
using WpfColor = System.Windows.Media.Color;
using WpfBrush = System.Windows.Media.Brush;
using WpfButton = System.Windows.Controls.Button;
using WpfPanel = System.Windows.Controls.Panel;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfFontFamily = System.Windows.Media.FontFamily;
using WpfBrushes = System.Windows.Media.Brushes;

namespace PerformanceOverlay;

public partial class MainWindow
{
    private void OpenCoreSettings()
    {
        if (_isStopping) return;
        if (_coreSettingsWindow is { IsVisible: true }) { _coreSettingsWindow.Activate(); return; }
        _coreSettingsWindow = new CoreSettingsWindow(this) { Owner = this };
        _coreSettingsWindow.Closed += (_, _) => _coreSettingsWindow = null;
        _coreSettingsWindow.Show();
    }

    private sealed class CoreSettingsWindow : Window
    {
        private readonly MainWindow _host;
        private readonly StackPanel _managedPluginList = new();
        private readonly TextBlock _managedPluginSummary = new();
        private readonly TextBlock _overlayState = new();
        private readonly TextBlock _versionText = new();
        private Grid? _pages;
        private UIElement? _generalPage;
        private UIElement? _pluginsPage;

        private static readonly WpfColor Bg = WpfColor.FromRgb(14, 18, 27);
        private static readonly WpfColor Surface = WpfColor.FromRgb(23, 29, 42);
        private static readonly WpfColor Surface2 = WpfColor.FromRgb(31, 39, 55);
        private static readonly WpfColor Muted = WpfColor.FromRgb(157, 169, 190);
        private static readonly WpfColor Accent = WpfColor.FromRgb(57, 215, 230);

        public CoreSettingsWindow(MainWindow host)
        {
            _host = host;
            Title = "Performance Overlay";
            var workArea = SystemParameters.WorkArea;
            Width = Math.Min(900, Math.Max(640, workArea.Width - 24));
            Height = Math.Min(720, Math.Max(420, workArea.Height - 24));
            MinWidth = Math.Min(760, Width); MinHeight = Math.Min(560, Height);
            MaxWidth = workArea.Width; MaxHeight = workArea.Height;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Brush(Bg); Foreground = WpfBrushes.White;
            FontFamily = new WpfFontFamily("Segoe UI");

            var root = new Grid();
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(225) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Content = root;

            var sidebar = new StackPanel { Background = Brush(WpfColor.FromRgb(18, 23, 34)), Margin = new Thickness(0), };
            sidebar.Children.Add(new TextBlock { Text = "PERFORMANCE", Foreground = Brush(Accent), FontSize = 11, FontWeight = FontWeights.Bold, Margin = new Thickness(22, 25, 0, 2) });
            sidebar.Children.Add(new TextBlock { Text = "Overlay", FontSize = 25, FontWeight = FontWeights.SemiBold, Margin = new Thickness(22, 0, 0, 23) });
            Grid.SetColumn(sidebar, 0); root.Children.Add(sidebar);

            var main = new StackPanel { Margin = new Thickness(30, 28, 30, 24) };
            var mainScroll = new ScrollViewer { Content = main, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            Grid.SetColumn(mainScroll, 1); root.Children.Add(mainScroll);
            main.Children.Add(new TextBlock { Text = "Control center", FontSize = 25, FontWeight = FontWeights.SemiBold });
            main.Children.Add(new TextBlock { Text = "Appearance, plugins and quick actions", Foreground = Brush(Muted), Margin = new Thickness(0, 3, 0, 20) });

            var status = new Grid { Margin = new Thickness(0, 0, 0, 16) };
            status.ColumnDefinitions.Add(new ColumnDefinition()); status.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var statusCard = Card();
            var statusStack = new StackPanel();
            statusStack.Children.Add(new TextBlock { Text = "OVERLAY STATUS", Foreground = Brush(Muted), FontSize = 10, FontWeight = FontWeights.Bold });
            _overlayState.Text = _host.IsVisible ? "●  Running on desktop" : "○  Hidden in tray";
            _overlayState.FontSize = 17; _overlayState.Margin = new Thickness(0, 5, 0, 0);
            statusStack.Children.Add(_overlayState); statusCard.Child = statusStack; status.Children.Add(statusCard);
            var toggle = Button(_host.IsVisible ? "Hide overlay" : "Show overlay", Accent, Surface, () => ToggleOverlay());
            toggle.Margin = new Thickness(12, 0, 0, 0); Grid.SetColumn(toggle, 1); status.Children.Add(toggle);
            main.Children.Add(status);

            var contentGrid = new Grid();
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.08, GridUnitType.Star) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.92, GridUnitType.Star) });
            var left = new StackPanel(); var right = new StackPanel { Margin = new Thickness(14, 0, 0, 0) };
            Grid.SetColumn(left, 0); Grid.SetColumn(right, 1); contentGrid.Children.Add(left); contentGrid.Children.Add(right);
            _pages = new Grid();
            _generalPage = contentGrid;
            _pluginsPage = BuildPluginsPage();
            _pages.Children.Add(_generalPage);
            _pluginsPage.Visibility = Visibility.Collapsed;
            _pages.Children.Add(_pluginsPage);
            main.Children.Add(_pages);

            left.Children.Add(SectionTitle("Appearance", "Adjust how the overlay looks on screen"));
            left.Children.Add(MakeSettingCard("Accent color", "Choose the overlay highlight", BuildColorPicker()));
            left.Children.Add(MakeSettingCard("Screen position", "Choose the overlay anchor", BuildPositionPicker()));

            right.Children.Add(SectionTitle("Quick actions", "Everything from the tray menu"));
            var actions = Card();
            var actionStack = new StackPanel();
            actionStack.Children.Add(ActionRow("Reload everything", "Restart sensor and plugin data", async () => await _host.ReloadAllAsync()));
            actionStack.Children.Add(ActionRow("Configure hotkeys", "Set reset, reload and exit shortcuts", _host.ShowHotkeySettings));
            actionStack.Children.Add(ActionRow("Help and indicator guide", "What the overlay values mean", _host.ShowIndicatorHelp));
            actionStack.Children.Add(ActionRow("Discover plugins", "Browse available extensions", OpenPluginCatalog));
            actionStack.Children.Add(ActionRow("Open plugins folder", "Manage installed plugin files", () => _host.OpenPluginFolder()));
            actionStack.Children.Add(ActionRow("Exit overlay", "Close the overlay", () => _host.StopOverlayAnimation(), danger: true));
            actions.Child = actionStack; right.Children.Add(actions);

            var version = typeof(MainWindow).Assembly.GetName().Version;
            _versionText.Text = $"Performance Overlay  ·  v{version?.ToString(3) ?? "unknown"}";
            _versionText.Foreground = Brush(Muted); _versionText.FontSize = 11;
            _versionText.Margin = new Thickness(2, 14, 0, 0); right.Children.Add(_versionText);

            NavButton(sidebar, "◈   General", true, ShowGeneralPage);
            NavButton(sidebar, "▦   Plugins", false, ShowPluginsPage);
            NavButton(sidebar, "⌘   Shortcuts & actions", false, () => _host.ShowHotkeySettings());
            var fill = new Border { Height = 1, Background = Brush(Surface2), Margin = new Thickness(18, 17, 18, 13) };
            sidebar.Children.Add(fill);
            sidebar.Children.Add(new TextBlock { Text = "TRAY MENU", Foreground = Brush(Muted), FontSize = 10, FontWeight = FontWeights.Bold, Margin = new Thickness(22, 0, 0, 8) });
            NavButton(sidebar, "↻   Reload everything", false, async () => await _host.ReloadAllAsync());
            NavButton(sidebar, "?   Help", false, _host.ShowIndicatorHelp);
            var spacer = new Border(); System.Windows.Controls.DockPanel.SetDock(spacer, Dock.Bottom); sidebar.Children.Add(spacer);
            sidebar.Children.Add(new TextBlock { Text = _versionText.Text, Foreground = Brush(Muted), FontSize = 10, Margin = new Thickness(22, 0, 0, 20) });
            RefreshPluginList();
        }

        private UIElement BuildPluginsPage()
        {
            var page = new StackPanel();
            page.Children.Add(SectionTitle("Plugins", "Enable plugins and open their settings"));
            var card = Card();
            var panel = new StackPanel();
            var header = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            header.ColumnDefinitions.Add(new ColumnDefinition()); header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.Children.Add(_managedPluginSummary);
            var refresh = Button("Refresh", Brush(Surface2), WpfBrushes.White, RefreshPluginList);
            Grid.SetColumn(refresh, 1); header.Children.Add(refresh); panel.Children.Add(header);
            panel.Children.Add(new Separator { Background = Brush(Surface2), Margin = new Thickness(0, 0, 0, 8) });
            panel.Children.Add(_managedPluginList);
            var buttons = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
            buttons.Children.Add(Button("Load plugin…", Accent, Bg, LoadPlugin));
            buttons.Children.Add(Button("Discover", Brush(Surface2), WpfBrushes.White, OpenPluginCatalog));
            buttons.Children.Add(Button("Plugin folder", Brush(Surface2), WpfBrushes.White, () => _host.OpenPluginFolder()));
            panel.Children.Add(buttons); card.Child = panel; page.Children.Add(card);
            return page;
        }

        private void ShowGeneralPage()
        {
            if (_generalPage is not null) _generalPage.Visibility = Visibility.Visible;
            if (_pluginsPage is not null) _pluginsPage.Visibility = Visibility.Collapsed;
        }

        private void ShowPluginsPage()
        {
            RefreshPluginList();
            if (_generalPage is not null) _generalPage.Visibility = Visibility.Collapsed;
            if (_pluginsPage is not null) _pluginsPage.Visibility = Visibility.Visible;
        }

        private UIElement BuildColorPicker()
        {
            var picker = new WpfComboBox { ItemsSource = new[] { "Turquoise", "Red", "Green", "Blue", "Orange", "Black", "White" }, SelectedItem = ColorName(_host._accentColor), Height = 36 };
            picker.SelectionChanged += (_, _) => { if (picker.SelectedItem is string name) _host.SetAccentColor(ColorFromName(name)); };
            return picker;
        }

        private UIElement BuildPositionPicker()
        {
            var picker = new WpfComboBox { ItemsSource = new[] { "Top Left", "Top Center", "Top Right" }, SelectedItem = _host._overlayPosition, Height = 36 };
            picker.SelectionChanged += (_, _) => { if (picker.SelectedItem is string value) _host.SetOverlayPosition(value); };
            return picker;
        }

        private void RefreshPluginList()
        {
            _managedPluginList.Children.Clear();
            _managedPluginSummary.Text = _host._loadedPlugins.Count == 1 ? "1 plugin installed" : $"{_host._loadedPlugins.Count} plugins installed";
            if (_host._loadedPlugins.Count == 0)
                _managedPluginList.Children.Add(new TextBlock { Text = "No plugins loaded yet.", Foreground = Brush(Muted), Margin = new Thickness(0, 3, 0, 3) });
            foreach (var plugin in _host._loadedPlugins)
                _managedPluginList.Children.Add(CreatePluginRow(plugin));
            if (_host._pluginLoadErrors.Count > 0)
            {
                _managedPluginList.Children.Add(Button($"View {_host._pluginLoadErrors.Count} plugin error(s)", WpfColor.FromRgb(255, 120, 120), Bg, () => System.Windows.MessageBox.Show(string.Join(Environment.NewLine + Environment.NewLine, _host._pluginLoadErrors), "Plugin errors", MessageBoxButton.OK, MessageBoxImage.Warning)));
            }
        }

        private UIElement CreatePluginRow(LoadedOverlayPlugin plugin)
        {
            var line = new Grid { Margin = new Thickness(0, 6, 0, 6) };
            line.ColumnDefinitions.Add(new ColumnDefinition());
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var details = new StackPanel();
            var check = new WpfCheckBox { Content = plugin.Instance.Name, ToolTip = plugin.Instance.Description, Foreground = WpfBrushes.White, IsChecked = _host._pluginContainers.TryGetValue(plugin, out var view) && view.Visibility == Visibility.Visible };
            check.Checked += (_, _) => SetPluginVisibility(plugin, true); check.Unchecked += (_, _) => SetPluginVisibility(plugin, false);
            details.Children.Add(check);
            if (!string.IsNullOrWhiteSpace(plugin.Instance.Description)) details.Children.Add(new TextBlock { Text = plugin.Instance.Description, Foreground = Brush(Muted), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(22, 2, 12, 0), FontSize = 11 });
            line.Children.Add(details);
            var hasSettings = plugin.Instance is IOverlayPluginSettings;
            var settingsButton = Button("Settings", Brush(Surface2), WpfBrushes.White, () =>
            {
                if (plugin.Instance is IOverlayPluginSettings settings) settings.OpenSettings(this);
            });
            settingsButton.IsEnabled = hasSettings;
            settingsButton.ToolTip = hasSettings ? "Open plugin settings" : "This plugin does not provide a settings window";
            Grid.SetColumn(settingsButton, 1); line.Children.Add(settingsButton);
            return line;
        }

        private void LoadPlugin() { _host.LoadPluginFromDialog(); RefreshPluginList(); }
        private void ToggleOverlay()
        {
            if (_host.IsVisible) _host.HideOverlayToTray(); else _host.ShowOverlayFromTray();
            _overlayState.Text = _host.IsVisible ? "●  Running on desktop" : "○  Hidden in tray";
        }
        private void SetPluginVisibility(LoadedOverlayPlugin plugin, bool visible)
        {
            if (_host._pluginContainers.TryGetValue(plugin, out var view)) view.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        private static Border MakeSettingCard(string title, string subtitle, UIElement control)
        {
            var stack = new StackPanel(); stack.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, FontSize = 14 });
            stack.Children.Add(new TextBlock { Text = subtitle, Foreground = Brush(Muted), FontSize = 11, Margin = new Thickness(0, 2, 0, 10) });
            stack.Children.Add(control); var card = Card(); card.Child = stack; card.Margin = new Thickness(0, 0, 0, 10); return card;
        }
        private static Border Card() => new() { Background = Brush(Surface), BorderBrush = Brush(WpfColor.FromRgb(41, 51, 69)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(16), Margin = new Thickness(0, 0, 0, 10) };
        private static TextBlock SectionTitle(string title, string subtitle)
        {
            var block = new TextBlock { Text = title + "\n" + subtitle, FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = WpfBrushes.White, Margin = new Thickness(1, 0, 0, 10) };
            return block;
        }
        private static UIElement ActionRow(string title, string description, Action action, bool danger = false)
        {
            var grid = new Grid { Margin = new Thickness(0, 2, 0, 3) };
            grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var text = new StackPanel(); text.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, Foreground = danger ? Brush(WpfColor.FromRgb(255, 145, 145)) : WpfBrushes.White });
            text.Children.Add(new TextBlock { Text = description, Foreground = Brush(Muted), FontSize = 10, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 4, 0) });
            grid.Children.Add(text);
            var go = Button("Open", Brush(Surface2), WpfBrushes.White, action); Grid.SetColumn(go, 1); grid.Children.Add(go);
            return grid;
        }
        private static WpfButton Button(string label, WpfColor background, WpfColor foreground, Action action) => Button(label, Brush(background), Brush(foreground), action);
        private static WpfButton Button(string label, WpfBrush background, WpfBrush foreground, Action action)
        {
            var button = new WpfButton { Content = label, Background = background, Foreground = foreground, BorderThickness = new Thickness(0), Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 0, 7, 0), Cursor = System.Windows.Input.Cursors.Hand, FontSize = 11 };
            button.Click += (_, _) => action(); return button;
        }
        private static void NavButton(WpfPanel sidebar, string label, bool selected, Action action)
        {
            var button = Button(label, selected ? Brush(WpfColor.FromRgb(33, 51, 67)) : WpfBrushes.Transparent, selected ? Brush(Accent) : Brush(WpfColor.FromRgb(209, 217, 230)), action);
            button.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Left; button.Margin = new Thickness(12, 2, 12, 2); button.Padding = new Thickness(12, 11, 12, 11); button.FontSize = 12;
            sidebar.Children.Add(button);
        }
        private static SolidColorBrush Brush(WpfColor color) => new(color);
        private static WpfColor ColorFromName(string name) => name switch
        {
            "Red" => Colors.Red, "Green" => Colors.LimeGreen, "Blue" => Colors.DodgerBlue, "Orange" => Colors.DarkOrange,
            "Black" => Colors.Black, "White" => Colors.White, _ => WpfColor.FromRgb(53, 221, 240)
        };
        private static string ColorName(WpfColor color) => color == Colors.Red ? "Red" : color == Colors.LimeGreen ? "Green" : color == Colors.DodgerBlue ? "Blue" : color == Colors.DarkOrange ? "Orange" : color == Colors.Black ? "Black" : color == Colors.White ? "White" : "Turquoise";
    }
}
