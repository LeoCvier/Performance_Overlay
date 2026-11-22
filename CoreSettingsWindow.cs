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
using Forms = System.Windows.Forms;

namespace PerformanceOverlay;

public partial class MainWindow
{
    private void OpenCoreSettings() => OpenCoreSettings("general");

    private void OpenCoreSettings(string page)
    {
        if (_isStopping) return;
        if (_coreSettingsWindow is { IsVisible: true })
        {
            _coreSettingsWindow.Navigate(page);
            _coreSettingsWindow.Activate();
            return;
        }
        _coreSettingsWindow = new CoreSettingsWindow(this, page) { Owner = this };
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
        private System.Windows.Controls.TabControl? _pages;
        private UIElement? _generalPage;
        private UIElement? _customizationPage;
        private UIElement? _pluginsPage;
        private UIElement? _shortcutsPage;
        private UIElement? _helpPage;
        private readonly Dictionary<string, UIElement> _metricRows = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, HotkeyBinding> _hotkeyDraft = new();
        private readonly Dictionary<int, System.Windows.Controls.TextBox> _hotkeyInputs = new();

        private static readonly WpfColor Bg = WpfColor.FromRgb(14, 18, 27);
        private static readonly WpfColor Surface = WpfColor.FromRgb(23, 29, 42);
        private static readonly WpfColor Surface2 = WpfColor.FromRgb(31, 39, 55);
        private static readonly WpfColor Muted = WpfColor.FromRgb(157, 169, 190);
        private static readonly WpfColor Accent = WpfColor.FromRgb(57, 215, 230);

        public CoreSettingsWindow(MainWindow host, string initialPage)
        {
            _host = host;
            Title = L("Performance Overlay Settings");
            var workArea = SystemParameters.WorkArea;
            Width = Math.Min(960, Math.Max(760, workArea.Width - 80)); Height = Math.Min(760, Math.Max(600, workArea.Height - 80));
            MinWidth = 720; MinHeight = 560; ResizeMode = ResizeMode.CanResize; WindowStyle = WindowStyle.SingleBorderWindow; ShowInTaskbar = true;
            WindowStartupLocation = WindowStartupLocation.CenterScreen; Background = Brush(Bg); Foreground = WpfBrushes.White; FontFamily = new WpfFontFamily("Segoe UI");
            var root = new DockPanel { Margin = new Thickness(22, 16, 22, 16) }; Content = root;
            var header = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
            header.Children.Add(new TextBlock { Text = L("Performance Overlay"), FontSize = 24, FontWeight = FontWeights.SemiBold });
            var status = new Grid { Margin = new Thickness(0, 10, 0, 0) }; status.ColumnDefinitions.Add(new ColumnDefinition()); status.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _overlayState.Text = _host.IsVisible ? L("●  Running on desktop") : L("○  Hidden in tray"); _overlayState.VerticalAlignment = VerticalAlignment.Center;
            status.Children.Add(_overlayState); var toggle = Button(L(_host.IsVisible ? "Hide overlay" : "Show overlay"), Accent, Surface, ToggleOverlay); Grid.SetColumn(toggle, 1); status.Children.Add(toggle); header.Children.Add(status);
            DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
            _pages = new System.Windows.Controls.TabControl { Background = Brush(Bg), BorderBrush = Brush(Surface2), Padding = new Thickness(10) }; root.Children.Add(_pages);
            _generalPage = BuildGeneralPage(); _customizationPage = BuildCustomizationPage(); _pluginsPage = BuildPluginsPage(); _shortcutsPage = BuildShortcutsPage(); _helpPage = BuildHelpPage();
            AddTab("general", "General", _generalPage); AddTab("customization", "Overlay", _customizationPage); AddTab("plugins", "Plugins", _pluginsPage); AddTab("shortcuts", "Shortcuts", _shortcutsPage); AddTab("help", "Help", _helpPage);
            var footer = new TextBlock { Text = $"{L("Performance Overlay")} · v{typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "unknown"}", Foreground = Brush(Muted), FontSize = 11, Margin = new Thickness(0, 10, 0, 0), HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
            DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
            RefreshPluginList(); Navigate(initialPage);
        }

        private void AddTab(string key, string title, UIElement page)
        {
            var scroll = new ScrollViewer { Content = page, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            _pages!.Items.Add(new TabItem { Header = L(title), Content = scroll, Tag = key, Padding = new Thickness(14, 8, 14, 8) });
        }

        private UIElement BuildGeneralPage()
        {
            var page = new StackPanel(); page.Children.Add(SectionTitle(L("General"), L("Language and quick actions")));
            page.Children.Add(MakeSettingCard(L("Language"), L("Choose the language used by menus"), BuildLanguagePicker()));
            page.Children.Add(SectionTitle(L("Quick actions"), L("Actions available from the tray menu")));
            var actions = Card(); var stack = new StackPanel();
            stack.Children.Add(ActionRow(L("Reload everything"), L("Restart sensor and plugin data"), async () => await _host.ReloadAllAsync()));
            stack.Children.Add(ActionRow(L("Configure hotkeys"), L("Set reset, reload and exit shortcuts"), () => Navigate("shortcuts")));
            stack.Children.Add(ActionRow(L("Help and indicator guide"), L("What the overlay values mean"), () => Navigate("help")));
            stack.Children.Add(ActionRow(L("Discover plugins"), L("Browse available extensions"), OpenPluginCatalog));
            stack.Children.Add(ActionRow(L("Open plugins folder"), L("Manage installed plugin files"), () => _host.OpenPluginFolder()));
            stack.Children.Add(ActionRow(L("Exit overlay"), L("Close the overlay"), () => _host.StopOverlayAnimation(), danger: true)); actions.Child = stack; page.Children.Add(actions); return page;
        }

        private UIElement BuildCustomizationPage()
        {
            var page = new StackPanel(); page.Children.Add(SectionTitle(L("Overlay appearance"), L("Customize size, color, placement and displayed metrics")));
            var appearance = Card(); var body = new StackPanel();
            body.Children.Add(new TextBlock { Text = L("Accent color"), FontWeight = FontWeights.SemiBold });
            var colorRow = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 7, 0, 12) };
            var swatch = new Border { Width = 28, Height = 28, Background = new SolidColorBrush(_host._accentColor), CornerRadius = new CornerRadius(5), Margin = new Thickness(0, 0, 10, 0) };
            colorRow.Children.Add(swatch); colorRow.Children.Add(Button(L("Choose color…"), Brush(Surface2), WpfBrushes.White, () => { using var dialog = new Forms.ColorDialog { Color = System.Drawing.Color.FromArgb(_host._accentColor.R, _host._accentColor.G, _host._accentColor.B), FullOpen = true }; if (dialog.ShowDialog() == Forms.DialogResult.OK) { var c = WpfColor.FromRgb(dialog.Color.R, dialog.Color.G, dialog.Color.B); _host.SetAccentColor(c); swatch.Background = new SolidColorBrush(c); } })); body.Children.Add(colorRow);
            body.Children.Add(SliderRow(L("Overlay size"), .65, 2.2, _host._overlayScale, v => _host.SetOverlayScale(v), "0.00×"));
            body.Children.Add(SliderRow(L("Hover transparency"), 0, 100, _host._hoverTransparency, v => _host.SetHoverTransparency(v), "0%"));
            body.Children.Add(SliderRow(L("Hover distance"), 0, 500, _host._hoverRadius, v => _host.SetHoverRadius(v), "0 px"));
            body.Children.Add(new TextBlock { Text = L("Monitor"), Margin = new Thickness(0, 12, 0, 4), FontWeight = FontWeights.SemiBold });
            var screens = Forms.Screen.AllScreens; var monitor = new WpfComboBox { Height = 34, ItemsSource = screens.Select((x, i) => new MonitorChoice(x.DeviceName, $"{(x.Primary ? L("Primary") : L("Screen"))} {i + 1} ({x.Bounds.Width}×{x.Bounds.Height})")).ToList(), DisplayMemberPath = nameof(MonitorChoice.Label), SelectedValuePath = nameof(MonitorChoice.Device), SelectedValue = screens.FirstOrDefault(x => x.DeviceName.Equals(_host._selectedMonitor, StringComparison.OrdinalIgnoreCase))?.DeviceName ?? Forms.Screen.PrimaryScreen?.DeviceName };
            monitor.SelectionChanged += (_, _) => { if (monitor.SelectedValue is string device) _host.SetSelectedMonitor(device); }; body.Children.Add(monitor);
            body.Children.Add(new TextBlock { Text = L("Screen position"), Margin = new Thickness(0, 12, 0, 4), FontWeight = FontWeights.SemiBold }); body.Children.Add(BuildPositionPicker());
            body.Children.Add(new TextBlock { Text = L("Hover fading only changes visibility; the overlay remains click-through."), Foreground = Brush(Muted), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0) }); appearance.Child = body; page.Children.Add(appearance);
            page.Children.Add(SectionTitle(L("Displayed metrics"), L("Choose values and change their order")));
            var metricsCard = Card(); var metrics = new StackPanel();
            foreach (var key in CoreMetricOrder) { var row = new Grid { Margin = new Thickness(0, 4, 0, 4) }; row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); var check = new WpfCheckBox { Content = key, IsChecked = _host._enabledMetrics.Contains(key), VerticalAlignment = VerticalAlignment.Center }; check.Checked += (_, _) => _host.SetMetricEnabled(key, true); check.Unchecked += (_, _) => _host.SetMetricEnabled(key, false); row.Children.Add(check); var buttons = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal }; buttons.Children.Add(Button("↑", Brush(Surface2), WpfBrushes.White, () => { _host.MoveMetric(key, -1); RefreshMetricOrder(metrics); })); buttons.Children.Add(Button("↓", Brush(Surface2), WpfBrushes.White, () => { _host.MoveMetric(key, 1); RefreshMetricOrder(metrics); })); Grid.SetColumn(buttons, 1); row.Children.Add(buttons); row.Tag = key; _metricRows[key] = row; metrics.Children.Add(row); }
            metricsCard.Child = metrics; page.Children.Add(metricsCard); return page;
        }

        private sealed record MonitorChoice(string Device, string Label);
        private UIElement SliderRow(string label, double min, double max, double value, Action<double> changed, string format)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 5, 0, 5) }; var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); grid.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center }); var valueText = new TextBlock { Text = string.Format(System.Globalization.CultureInfo.CurrentCulture, format, value), Foreground = Brush(Muted), VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(valueText, 1); grid.Children.Add(valueText); panel.Children.Add(grid);
            var slider = new Slider { Minimum = min, Maximum = max, Value = value, TickFrequency = max > 10 ? 1 : .05, Margin = new Thickness(0, 2, 0, 0) }; slider.ValueChanged += (_, _) => { valueText.Text = format.Contains("0.00") ? $"{slider.Value:0.00}×" : format.Contains("px") ? $"{slider.Value:0} px" : $"{slider.Value:0}%"; changed(slider.Value); }; panel.Children.Add(slider); return panel;
        }
        private void RefreshMetricOrder(StackPanel panel)
        {
            panel.Children.Clear();
            foreach (var key in _host._metricOrder)
                if (_metricRows.TryGetValue(key, out var row)) panel.Children.Add(row);
        }
        private UIElement BuildPluginsPage()
        {
            var page = new StackPanel();
            page.Children.Add(SectionTitle(L("Plugins"), L("Enable plugins and open their settings")));
            var card = Card();
            var panel = new StackPanel();
            var header = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            header.ColumnDefinitions.Add(new ColumnDefinition()); header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.Children.Add(_managedPluginSummary);
            var refresh = Button(L("Refresh"), Brush(Surface2), WpfBrushes.White, RefreshPluginList);
            Grid.SetColumn(refresh, 1); header.Children.Add(refresh); panel.Children.Add(header);
            panel.Children.Add(new Separator { Background = Brush(Surface2), Margin = new Thickness(0, 0, 0, 8) });
            panel.Children.Add(_managedPluginList);
            var buttons = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
            buttons.Children.Add(Button(L("Load plugin…"), Accent, Bg, LoadPlugin));
            buttons.Children.Add(Button(L("Discover"), Brush(Surface2), WpfBrushes.White, OpenPluginCatalog));
            buttons.Children.Add(Button(L("Plugin folder"), Brush(Surface2), WpfBrushes.White, () => _host.OpenPluginFolder()));
            panel.Children.Add(buttons); card.Child = panel; page.Children.Add(card);
            return page;
        }

        private UIElement BuildShortcutsPage()
        {
            var page = new StackPanel();
            page.Children.Add(SectionTitle(L("Shortcuts & actions"), L("Set reset, reload and exit shortcuts")));
            var intro = Card();
            intro.Child = new TextBlock { Text = L("Click a shortcut field and press a combination with at least one modifier key. Press Escape to clear a shortcut."), Foreground = Brush(Muted), TextWrapping = TextWrapping.Wrap, FontSize = 12 };
            page.Children.Add(intro);

            var bindings = Card();
            var rows = new StackPanel();
            foreach (var pair in _host._hotkeys)
            {
                _hotkeyDraft[pair.Key] = pair.Value.Clone();
                rows.Children.Add(BuildHotkeyRow(pair.Key, _hotkeyDraft[pair.Key]));
                if (pair.Key != _host._hotkeys.Keys.Last()) rows.Children.Add(new Separator { Background = Brush(Surface2), Margin = new Thickness(0, 10, 0, 10) });
            }
            bindings.Child = rows;
            page.Children.Add(bindings);

            var buttons = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
            buttons.Children.Add(Button(L("Restore defaults"), Brush(Surface2), WpfBrushes.White, ResetHotkeys));
            buttons.Children.Add(Button(L("Cancel"), Brush(Surface2), WpfBrushes.White, DiscardHotkeyChanges));
            buttons.Children.Add(Button(L("Save"), Accent, Bg, SaveHotkeys));
            page.Children.Add(buttons);
            return page;
        }

        private UIElement BuildHotkeyRow(int id, HotkeyBinding binding)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(245) });
            string action = id == HotkeyIdReset ? L("Reset FPS max/min") : id == HotkeyIdExit ? L("Exit overlay") : L("Reload everything");
            var description = id == HotkeyIdReset ? L("Clear the current session's FPS high and low values") : id == HotkeyIdExit ? L("Close the overlay") : L("Restart sensors and reload plugins");
            var texts = new StackPanel();
            texts.Children.Add(new TextBlock { Text = action, Foreground = WpfBrushes.White, FontSize = 14, FontWeight = FontWeights.SemiBold });
            texts.Children.Add(new TextBlock { Text = description, Foreground = Brush(Muted), FontSize = 11, Margin = new Thickness(0, 3, 12, 0), TextWrapping = TextWrapping.Wrap });
            grid.Children.Add(texts);

            var input = new System.Windows.Controls.TextBox
            {
                Text = binding.Key == 0 ? L("Not set") : FormatHotkey(binding.Modifiers, System.Windows.Input.KeyInterop.KeyFromVirtualKey((int)binding.Key)),
                Height = 38,
                FontSize = 13,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = System.Windows.HorizontalAlignment.Center,
                IsReadOnly = true,
                Tag = id,
                ToolTip = L("Click here, then press a key combination")
            };
            input.GotKeyboardFocus += (_, _) => input.SelectAll();
            input.PreviewKeyDown += (_, e) =>
            {
                e.Handled = true;
                if (e.Key == System.Windows.Input.Key.Escape)
                {
                    var cleared = new HotkeyBinding { Display = "Not set" };
                    _hotkeyDraft[id] = cleared; input.Text = L("Not set"); return;
                }
                var key = e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key;
                if (key is System.Windows.Input.Key.LeftCtrl or System.Windows.Input.Key.RightCtrl or System.Windows.Input.Key.LeftAlt or System.Windows.Input.Key.RightAlt or System.Windows.Input.Key.LeftShift or System.Windows.Input.Key.RightShift or System.Windows.Input.Key.LWin or System.Windows.Input.Key.RWin) return;
                uint modifiers = 0; var pressed = System.Windows.Input.Keyboard.Modifiers;
                if (pressed.HasFlag(System.Windows.Input.ModifierKeys.Control)) modifiers |= ModControl;
                if (pressed.HasFlag(System.Windows.Input.ModifierKeys.Alt)) modifiers |= ModAlt;
                if (pressed.HasFlag(System.Windows.Input.ModifierKeys.Shift)) modifiers |= ModShift;
                if (pressed.HasFlag(System.Windows.Input.ModifierKeys.Windows)) modifiers |= ModWin;
                if (modifiers == 0) { input.Text = L("Use Ctrl/Alt/Shift/Win + a key"); return; }
                var value = new HotkeyBinding { Modifiers = modifiers, Key = (uint)System.Windows.Input.KeyInterop.VirtualKeyFromKey(key), Display = FormatHotkey(modifiers, key) };
                _hotkeyDraft[id] = value; input.Text = value.Display;
            };
            Grid.SetColumn(input, 1); grid.Children.Add(input); _hotkeyInputs[id] = input;
            return grid;
        }

        private string FormatHotkey(uint modifiers, System.Windows.Input.Key key)
        {
            var parts = new List<string>();
            if ((modifiers & ModControl) != 0) parts.Add(L("Ctrl"));
            if ((modifiers & ModAlt) != 0) parts.Add(L("Alt"));
            if ((modifiers & ModShift) != 0) parts.Add(L("Shift"));
            if ((modifiers & ModWin) != 0) parts.Add("Win");
            parts.Add(key.ToString());
            return string.Join(" + ", parts);
        }

        private void SaveHotkeys()
        {
            var previous = _host._hotkeys.ToDictionary(x => x.Key, x => x.Value.Clone());
            foreach (var pair in _hotkeyDraft) _host._hotkeys[pair.Key] = pair.Value.Clone();
            var hwnd = new System.Windows.Interop.WindowInteropHelper(_host).Handle;
            if (!_host.RegisterConfiguredHotkeys(hwnd, showErrors: true))
            {
                foreach (var pair in previous) _host._hotkeys[pair.Key] = pair.Value;
                _host.RegisterConfiguredHotkeys(hwnd, showErrors: false);
                return;
            }
            _host.SaveSettings();
            Navigate("general");
        }

        private void ResetHotkeys()
        {
            _hotkeyDraft[HotkeyIdReset] = new HotkeyBinding { Modifiers = ModControl | ModShift, Key = 0x52, Display = "Ctrl + Shift + R" };
            _hotkeyDraft[HotkeyIdExit] = new HotkeyBinding { Modifiers = ModControl | ModAlt, Key = 0x51, Display = "Ctrl + Alt + Q" };
            _hotkeyDraft[HotkeyIdReload] = new HotkeyBinding { Modifiers = ModControl | ModAlt, Key = 0x4C, Display = "Ctrl + Alt + L" };
            foreach (var pair in _hotkeyInputs)
            {
                var binding = _hotkeyDraft[pair.Key];
                pair.Value.Text = FormatHotkey(binding.Modifiers, System.Windows.Input.KeyInterop.KeyFromVirtualKey((int)binding.Key));
            }
        }

        private void DiscardHotkeyChanges()
        {
            foreach (var pair in _host._hotkeys)
            {
                _hotkeyDraft[pair.Key] = pair.Value.Clone();
                _hotkeyInputs[pair.Key].Text = pair.Value.Key == 0
                    ? L("Not set")
                    : FormatHotkey(pair.Value.Modifiers, System.Windows.Input.KeyInterop.KeyFromVirtualKey((int)pair.Value.Key));
            }
            Navigate("general");
        }

        private UIElement BuildHelpPage()
        {
            var page = new StackPanel();
            page.Children.Add(SectionTitle(L("Help and indicator guide"), L("What the overlay values mean")));

            var hero = Card();
            var heroContent = new StackPanel();
            heroContent.Children.Add(new TextBlock { Text = L("PERFORMANCE OVERLAY – INDICATOR GUIDE\n\n").Trim(), FontSize = 18, FontWeight = FontWeights.SemiBold, Foreground = Brush(Accent) });
            heroContent.Children.Add(new TextBlock { Text = L("GREEN DOT: At least one sensor has a new reading available. Press the reload hotkey to reveal available readings.\n\n").Trim(), Foreground = WpfBrushes.White, FontSize = 13, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) });
            hero.Child = heroContent; page.Children.Add(hero);

            var columns = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            columns.ColumnDefinitions.Add(new ColumnDefinition()); columns.ColumnDefinitions.Add(new ColumnDefinition());
            var fps = Card(); fps.Margin = new Thickness(0, 0, 6, 10);
            fps.Child = HelpCardContent("FPS", new[]
            {
                L("FPS AVG: Current average frames per second reported by Windows graphics events for the captured application.\n").Trim(),
                L("FPS MAX / FPS MIN: Highest and lowest FPS recorded during the current session.\n\n").Trim()
            });
            columns.Children.Add(fps);
            var sensors = Card(); sensors.Margin = new Thickness(6, 0, 0, 10); Grid.SetColumn(sensors, 1);
            sensors.Child = HelpCardContent(L("Sensors"), new[]
            {
                L("CPU LOAD: Processor utilization.\n").Trim(), L("CPU TEMP: Processor temperature, when supported by the hardware.\n").Trim(),
                L("RAM LOAD: System memory usage.\n").Trim(), L("GPU LOAD: Graphics processor utilization.\n").Trim(), L("GPU TEMP: Graphics processor temperature.\n\n").Trim()
            });
            columns.Children.Add(sensors); page.Children.Add(columns);

            var note = Card();
            var noteContent = new StackPanel();
            noteContent.Children.Add(new TextBlock { Text = L("FPS display behavior"), FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = WpfBrushes.White });
            noteContent.Children.Add(new TextBlock { Text = L("After a focus change, FPS fields show — while waiting for frame data. If no data arrives within five seconds, the dashes remain for five seconds and the FPS fields hide until data returns. After active capture goes quiet, the same five-second dash period starts after five seconds without a frame."), Foreground = Brush(Muted), FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) });
            note.Child = noteContent; page.Children.Add(note);
            return page;
        }

        private StackPanel HelpCardContent(string heading, IEnumerable<string> lines)
        {
            var content = new StackPanel();
            content.Children.Add(new TextBlock { Text = heading, FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = Brush(Accent), Margin = new Thickness(0, 0, 0, 8) });
            foreach (var line in lines) content.Children.Add(new TextBlock { Text = line, Foreground = WpfBrushes.White, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 5) });
            return content;
        }

        private void ShowGeneralPage()
        {
            Navigate("general");
        }

        private void ShowPluginsPage()
        {
            Navigate("plugins");
        }

        public void Navigate(string page)
        {
            if (_pages is null) return;
            if (page == "plugins") RefreshPluginList();
            var target = page switch { "plugins" => 2, "shortcuts" => 3, "help" => 4, "customization" or "overlay" => 1, _ => 0 };
            _pages.SelectedIndex = target;
        }
        private UIElement BuildColorPicker()
        {
            var picker = new WpfComboBox { ItemsSource = new[] { "Turquoise", "Red", "Green", "Blue", "Orange", "Black", "White" }, SelectedItem = ColorName(_host._accentColor), Height = 36 };
            picker.SelectionChanged += (_, _) => { if (picker.SelectedItem is string name) _host.SetAccentColor(ColorFromName(name)); };
            return picker;
        }

        private UIElement BuildLanguagePicker()
        {
            var picker = new WpfComboBox { Height = 36, ItemsSource = new[]
            {
                new LanguageEntry("English", L("English")),
                new LanguageEntry("German", L("German")),
                new LanguageEntry("French", L("French")),
                new LanguageEntry("Japanese", L("Japanese")),
                new LanguageEntry("Chinese", L("Chinese"))
            }, DisplayMemberPath = nameof(LanguageEntry.Label), SelectedValuePath = nameof(LanguageEntry.Value), SelectedValue = _host._uiLanguage };
            picker.SelectionChanged += (_, _) =>
            {
                if (picker.SelectedValue is not string language || language == _host._uiLanguage) return;
                _host.SetUiLanguage(language);
                Close();
                _host.Dispatcher.BeginInvoke(new Action(_host.OpenCoreSettings));
            };
            return picker;
        }

        private UIElement BuildPositionPicker()
        {
            var picker = new WpfComboBox { Height = 36, ItemsSource = new[]
            {
                new PositionEntry("Top Left", L("Top Left")),
                new PositionEntry("Top Center", L("Top Center")),
                new PositionEntry("Top Right", L("Top Right"))
            }, DisplayMemberPath = nameof(PositionEntry.Label), SelectedValuePath = nameof(PositionEntry.Value), SelectedValue = _host._overlayPosition };
            picker.SelectionChanged += (_, _) => { if (picker.SelectedValue is string value) _host.SetOverlayPosition(value); };
            return picker;
        }

        private void RefreshPluginList()
        {
            _managedPluginList.Children.Clear();
            _managedPluginSummary.Text = _host._loadedPlugins.Count == 1 ? "1 plugin installed" : $"{_host._loadedPlugins.Count} plugins installed";
            if (_host._loadedPlugins.Count == 0)
                _managedPluginList.Children.Add(new TextBlock { Text = L("No plugins loaded yet."), Foreground = Brush(Muted), Margin = new Thickness(0, 3, 0, 3) });
            foreach (var plugin in _host._loadedPlugins)
                _managedPluginList.Children.Add(CreatePluginRow(plugin));
            if (_host._pluginLoadErrors.Count > 0)
            {
                _managedPluginList.Children.Add(Button(string.Format(L("View {0} plugin error(s)"), _host._pluginLoadErrors.Count), WpfColor.FromRgb(255, 120, 120), Bg, () => System.Windows.MessageBox.Show(string.Join(Environment.NewLine + Environment.NewLine, _host._pluginLoadErrors), L("Plugin errors"), MessageBoxButton.OK, MessageBoxImage.Warning)));
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
            var settingsButton = Button(L("Settings"), Brush(Surface2), WpfBrushes.White, () =>
            {
                if (plugin.Instance is IOverlayPluginSettings settings) settings.OpenSettings(this);
            });
            settingsButton.IsEnabled = hasSettings;
            settingsButton.ToolTip = hasSettings ? L("Open plugin settings") : L("This plugin does not provide a settings window");
            Grid.SetColumn(settingsButton, 1); line.Children.Add(settingsButton);
            return line;
        }

        private void LoadPlugin() { _host.LoadPluginFromDialog(); RefreshPluginList(); }
        private void ToggleOverlay()
        {
            if (_host.IsVisible) _host.HideOverlayToTray(); else _host.ShowOverlayFromTray();
            _overlayState.Text = L(_host.IsVisible ? "●  Running on desktop" : "○  Hidden in tray");
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
        private UIElement ActionRow(string title, string description, Action action, bool danger = false)
        {
            var grid = new Grid { Margin = new Thickness(0, 2, 0, 3) };
            grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var text = new StackPanel(); text.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, Foreground = danger ? Brush(WpfColor.FromRgb(255, 145, 145)) : WpfBrushes.White });
            text.Children.Add(new TextBlock { Text = description, Foreground = Brush(Muted), FontSize = 10, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 4, 0) });
            grid.Children.Add(text);
            var go = Button(L("Open"), Brush(Surface2), WpfBrushes.White, action); Grid.SetColumn(go, 1); grid.Children.Add(go);
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

        private string L(string text) => UiText.Get(_host._uiLanguage, text);
        private sealed record LanguageEntry(string Value, string Label);
        private sealed record PositionEntry(string Value, string Label);
    }
}
