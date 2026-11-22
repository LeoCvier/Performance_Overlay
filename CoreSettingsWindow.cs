using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using WpfBinding = System.Windows.Data.Binding;
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
using WpfControl = System.Windows.Controls.Control;
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
        private System.Windows.Controls.TabControl? _pages;
        private UIElement? _generalPage;
        private UIElement? _customizationPage;
        private UIElement? _pluginsPage;
        private UIElement? _shortcutsPage;
        private UIElement? _helpPage;
        private readonly Dictionary<string, UIElement> _metricRows = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, HotkeyBinding> _hotkeyDraft = new();
        private readonly Dictionary<int, System.Windows.Controls.TextBox> _hotkeyInputs = new();
        private readonly List<ScrollViewer> _pageScrollers = new();

        private static readonly WpfColor Bg = WpfColor.FromRgb(8, 11, 16);
        private static readonly WpfColor Surface = WpfColor.FromRgb(16, 22, 31);
        private static readonly WpfColor Surface2 = WpfColor.FromRgb(18, 26, 36);
        private static readonly WpfColor Muted = WpfColor.FromRgb(153, 168, 184);
        private static readonly WpfColor Accent = WpfColor.FromRgb(53, 221, 240);

        public CoreSettingsWindow(MainWindow host, string initialPage)
        {
            _host = host;
            Title = L("Performance Overlay Settings");
            var workArea = SystemParameters.WorkArea;
            Width = Math.Min(1040, Math.Max(680, workArea.Width - 56)); Height = Math.Min(820, Math.Max(560, workArea.Height - 56));
            MinWidth = Math.Min(720, Width); MinHeight = Math.Min(540, Height); ResizeMode = ResizeMode.CanResize; WindowStyle = WindowStyle.SingleBorderWindow; ShowInTaskbar = true;
            WindowStartupLocation = WindowStartupLocation.CenterScreen; Background = Brush(Bg); Foreground = WpfBrushes.White; FontFamily = new WpfFontFamily("Segoe UI");
            var root = new DockPanel { Margin = new Thickness(26, 20, 26, 18) }; Content = root;
            SizeChanged += (_, _) => FitTabPages();
            var header = BuildWindowHeader(); DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
            _pages = new System.Windows.Controls.TabControl
            {
                Background = Brush(Bg), BorderThickness = new Thickness(0),
                ItemContainerStyle = BuildTabItemStyle(), ItemsPanel = BuildTabItemsPanel(),
                Template = BuildTabControlTemplate()
            };
            root.Children.Add(_pages);
            _generalPage = BuildGeneralPage(); _customizationPage = BuildCustomizationPage(); _pluginsPage = BuildPluginsPage(); _shortcutsPage = BuildShortcutsPage(); _helpPage = BuildHelpPage();
            AddTab("general", "General", _generalPage); AddTab("customization", "Overlay", _customizationPage); AddTab("plugins", "Plugins", _pluginsPage); AddTab("shortcuts", "Shortcuts", _shortcutsPage); AddTab("help", "Help", _helpPage);
            RefreshPluginList(); Navigate(initialPage);
        }

        private UIElement BuildWindowHeader()
        {
            var header = new Grid { Margin = new Thickness(0, 0, 0, 16) };
            header.ColumnDefinitions.Add(new ColumnDefinition()); header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var brand = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var mark = new Border { Width = 42, Height = 42, CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1), BorderBrush = Brush(WpfColor.FromArgb(86, Accent.R, Accent.G, Accent.B)), Background = new LinearGradientBrush(WpfColor.FromRgb(24, 49, 60), WpfColor.FromRgb(16, 25, 36), 45), Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Accent, BlurRadius = 18, ShadowDepth = 0, Opacity = .18 } };
            mark.Child = new TextBlock { Text = "P", Foreground = Brush(Accent), FontSize = 23, FontWeight = FontWeights.Bold, HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            brand.Children.Add(mark);
            var titles = new StackPanel { Margin = new Thickness(13, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            titles.Children.Add(new TextBlock { Text = L("PERFORMANCE OVERLAY"), Foreground = Brush(Accent), FontSize = 9, FontWeight = FontWeights.Bold });
            titles.Children.Add(new TextBlock { Text = L("Settings"), FontSize = 23, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, -1, 0, 0) });
            brand.Children.Add(titles); header.Children.Add(brand);
            var statusCard = new Border { Background = new LinearGradientBrush(Surface, Surface2, 0), BorderBrush = Brush(WpfColor.FromRgb(41, 55, 70)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(11), Padding = new Thickness(11, 8, 9, 8), VerticalAlignment = VerticalAlignment.Center };
            var status = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var stateDot = new System.Windows.Shapes.Ellipse { Width = 7, Height = 7, Fill = Brush(_host.IsVisible ? WpfColor.FromRgb(86, 229, 160) : WpfColor.FromRgb(157, 169, 184)), Margin = new Thickness(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center };
            status.Children.Add(stateDot); _overlayState.Text = _host.IsVisible ? L("RUNNING") : L("HIDDEN"); _overlayState.FontSize = 9; _overlayState.FontWeight = FontWeights.Bold; _overlayState.Foreground = Brush(Muted); _overlayState.VerticalAlignment = VerticalAlignment.Center; status.Children.Add(_overlayState);
            var toggle = Button(L(_host.IsVisible ? "Hide" : "Show"), Brush(WpfColor.FromArgb(35, Accent.R, Accent.G, Accent.B)), Brush(Accent), ToggleOverlay); toggle.Margin = new Thickness(11, 0, 0, 0); toggle.Padding = new Thickness(11, 6, 11, 6); status.Children.Add(toggle);
            statusCard.Child = status; Grid.SetColumn(statusCard, 1); header.Children.Add(statusCard);
            return header;
        }

        private static ItemsPanelTemplate BuildTabItemsPanel()
        {
            var panel = new FrameworkElementFactory(typeof(StackPanel)); panel.SetValue(StackPanel.OrientationProperty, System.Windows.Controls.Orientation.Horizontal);
            return new ItemsPanelTemplate(panel);
        }

        private static ControlTemplate BuildTabControlTemplate()
        {
            var dock = new FrameworkElementFactory(typeof(DockPanel));
            var items = new FrameworkElementFactory(typeof(ItemsPresenter)); items.SetValue(DockPanel.DockProperty, Dock.Top); dock.AppendChild(items);
            var content = new FrameworkElementFactory(typeof(ContentPresenter)); content.SetValue(ContentPresenter.ContentSourceProperty, "SelectedContent"); content.SetValue(ContentPresenter.MarginProperty, new Thickness(0, 15, 0, 0)); dock.AppendChild(content);
            return new ControlTemplate(typeof(System.Windows.Controls.TabControl)) { VisualTree = dock };
        }

        private static Style BuildTabItemStyle()
        {
            var style = new Style(typeof(TabItem));
            style.Setters.Add(new Setter(WpfControl.ForegroundProperty, Brush(Muted))); style.Setters.Add(new Setter(WpfControl.BackgroundProperty, WpfBrushes.Transparent));
            style.Setters.Add(new Setter(WpfControl.FontSizeProperty, 11.5)); style.Setters.Add(new Setter(WpfControl.FontWeightProperty, FontWeights.SemiBold));
            style.Setters.Add(new Setter(WpfControl.BorderThicknessProperty, new Thickness(0))); style.Setters.Add(new Setter(WpfControl.PaddingProperty, new Thickness(14, 9, 14, 9)));
            var border = new FrameworkElementFactory(typeof(Border), "TabBorder"); border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8)); border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(WpfControl.PaddingProperty)); border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(WpfControl.BackgroundProperty));
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter)); presenter.SetValue(ContentPresenter.ContentSourceProperty, "Header"); presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center); presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center); border.AppendChild(presenter);
            var template = new ControlTemplate(typeof(TabItem)) { VisualTree = border };
            var selected = new Trigger { Property = TabItem.IsSelectedProperty, Value = true };
            selected.Setters.Add(new Setter(WpfControl.BackgroundProperty, Brush(WpfColor.FromRgb(24, 39, 51)))); selected.Setters.Add(new Setter(WpfControl.ForegroundProperty, Brush(Accent)));
            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true }; hover.Setters.Add(new Setter(WpfControl.BackgroundProperty, Brush(WpfColor.FromRgb(20, 31, 42))));
            template.Triggers.Add(selected); template.Triggers.Add(hover); style.Setters.Add(new Setter(WpfControl.TemplateProperty, template)); return style;
        }

        private void AddTab(string key, string title, UIElement page)
        {
            var scroll = new ScrollViewer { Content = page, Width = Math.Max(480, Width - 82), HorizontalAlignment = System.Windows.HorizontalAlignment.Left, HorizontalContentAlignment = System.Windows.HorizontalAlignment.Stretch, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            _pageScrollers.Add(scroll);
            void FitPageToViewport()
            {
                if (page is FrameworkElement element && scroll.ActualWidth > 0)
                    element.Width = scroll.ActualWidth;
            }
            scroll.SizeChanged += (_, _) => FitPageToViewport();
            scroll.Loaded += (_, _) => FitPageToViewport();
            _pages!.Items.Add(new TabItem { Header = L(title), Content = scroll, Tag = key, Padding = new Thickness(14, 8, 14, 8) });
        }

        private void FitTabPages()
        {
            foreach (var scroll in _pageScrollers)
                scroll.Width = Math.Max(480, ActualWidth - 82);
        }

        private UIElement BuildGeneralPage()
        {
            var page = new StackPanel(); page.Children.Add(SectionTitle(L("General"), L("Language, status and quick actions.")));
            var columns = new Grid(); columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(.82, GridUnitType.Star) }); columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.18, GridUnitType.Star) });
            var left = new StackPanel { Margin = new Thickness(0, 0, 10, 0) }; var right = new StackPanel { Margin = new Thickness(10, 0, 0, 0) };
            Grid.SetColumn(left, 0); Grid.SetColumn(right, 1); columns.Children.Add(left); columns.Children.Add(right);
            left.Children.Add(MakeSettingCard(L("Language"), L("Choose the language used by menus"), BuildLanguagePicker()));
            var note = Card(); var noteBody = new StackPanel(); noteBody.Children.Add(CardEyebrow(L("YOUR SETUP"))); noteBody.Children.Add(new TextBlock { Text = L("Preferences save as you go"), FontSize = 15, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 3, 0, 4) }); noteBody.Children.Add(new TextBlock { Text = L("Your language, appearance and metric choices are stored for your Windows account."), Foreground = Brush(Muted), FontSize = 11, TextWrapping = TextWrapping.Wrap }); note.Child = noteBody; left.Children.Add(note);
            right.Children.Add(SectionTitle(L("Quick actions"), L("Everything from the tray menu, right here.")));
            var actions = Card(); var stack = new StackPanel();
            stack.Children.Add(ActionRow(L("Reload everything"), L("Restart sensor and plugin data"), async () => await _host.ReloadAllAsync()));
            stack.Children.Add(ActionRow(L("Configure hotkeys"), L("Set reset, reload and exit shortcuts"), () => Navigate("shortcuts")));
            stack.Children.Add(ActionRow(L("Help and indicator guide"), L("What the overlay values mean"), () => Navigate("help")));
            stack.Children.Add(ActionRow(L("Discover plugins"), L("Browse available extensions"), OpenPluginCatalog));
            stack.Children.Add(ActionRow(L("Open plugins folder"), L("Manage installed plugin files"), () => _host.OpenPluginFolder()));
            stack.Children.Add(ActionRow(L("Exit overlay"), L("Close the overlay"), () => _host.StopOverlayAnimation(), danger: true)); actions.Child = stack; right.Children.Add(actions);
            page.Children.Add(columns); return page;
        }

        private UIElement BuildCustomizationPage()
        {
            var page = new StackPanel();
            page.Children.Add(SectionTitle(L("Make it yours"), L("Tune the look, position and information in your overlay.")));
            var columns = new Grid(); columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.08, GridUnitType.Star) });
            var left = new StackPanel { Margin = new Thickness(0, 0, 10, 0) }; var right = new StackPanel { Margin = new Thickness(10, 0, 0, 0) };
            Grid.SetColumn(left, 0); Grid.SetColumn(right, 1); columns.Children.Add(left); columns.Children.Add(right);

            var appearance = Card(); var body = new StackPanel();
            body.Children.Add(CardEyebrow(L("APPEARANCE")));
            body.Children.Add(new TextBlock { Text = L("Accent color"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 0) });
            var colorRow = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 12) };
            var swatch = new Border { Width = 30, Height = 30, Background = new SolidColorBrush(_host._accentColor), CornerRadius = new CornerRadius(8), BorderBrush = Brush(WpfColor.FromArgb(80, 255, 255, 255)), BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 10, 0) };
            colorRow.Children.Add(swatch); colorRow.Children.Add(Button(L("Choose color…"), Brush(WpfColor.FromRgb(28, 39, 51)), WpfBrushes.White, () => { using var dialog = new Forms.ColorDialog { Color = System.Drawing.Color.FromArgb(_host._accentColor.R, _host._accentColor.G, _host._accentColor.B), FullOpen = true }; if (dialog.ShowDialog() == Forms.DialogResult.OK) { var c = WpfColor.FromRgb(dialog.Color.R, dialog.Color.G, dialog.Color.B); _host.SetAccentColor(c); swatch.Background = new SolidColorBrush(c); } })); body.Children.Add(colorRow);
            body.Children.Add(SliderRow(L("Overlay size"), .65, 2.2, _host._overlayScale, v => _host.SetOverlayScale(v), "0.00×"));
            body.Children.Add(SliderRow(L("Hover transparency"), 0, 100, _host._hoverTransparency, v => _host.SetHoverTransparency(v), "0%"));
            body.Children.Add(SliderRow(L("Hover distance"), 0, 500, _host._hoverRadius, v => _host.SetHoverRadius(v), "0 px"));
            appearance.Child = body; left.Children.Add(appearance);

            var placement = Card(); var placementBody = new StackPanel(); placementBody.Children.Add(CardEyebrow(L("PLACEMENT")));
            placementBody.Children.Add(new TextBlock { Text = L("Monitor"), Margin = new Thickness(0, 9, 0, 5), FontWeight = FontWeights.SemiBold });
            var screens = Forms.Screen.AllScreens; var monitor = CreateModernComboBox(); monitor.ItemsSource = screens.Select((x, i) => new MonitorChoice(x.DeviceName, $"{(x.Primary ? L("Primary") : L("Screen"))} {i + 1}  ·  {x.Bounds.Width} × {x.Bounds.Height}")).ToList(); monitor.ItemTemplate = TextDataTemplate(typeof(MonitorChoice), nameof(MonitorChoice.Label)); monitor.SelectedValuePath = nameof(MonitorChoice.Device); monitor.SelectedValue = screens.FirstOrDefault(x => x.DeviceName.Equals(_host._selectedMonitor, StringComparison.OrdinalIgnoreCase))?.DeviceName ?? Forms.Screen.PrimaryScreen?.DeviceName;
            monitor.SelectionChanged += (_, _) => { if (monitor.SelectedValue is string device) _host.SetSelectedMonitor(device); }; placementBody.Children.Add(monitor);
            placementBody.Children.Add(new TextBlock { Text = L("Screen position"), Margin = new Thickness(0, 13, 0, 5), FontWeight = FontWeights.SemiBold }); placementBody.Children.Add(BuildPositionPicker());
            placementBody.Children.Add(new TextBlock { Text = L("Hover fading only changes visibility; the overlay remains click-through."), Foreground = Brush(Muted), TextWrapping = TextWrapping.Wrap, FontSize = 10, Margin = new Thickness(0, 12, 0, 0) }); placement.Child = placementBody; left.Children.Add(placement);

            var metricsCard = Card(); var metrics = new StackPanel(); metrics.Children.Add(CardEyebrow(L("METRICS"))); metrics.Children.Add(new TextBlock { Text = L("Displayed values"), FontSize = 17, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 5, 0, 3) }); metrics.Children.Add(new TextBlock { Text = L("Choose what appears, then move each value into place."), Foreground = Brush(Muted), FontSize = 10, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 13) });
            var metricRows = new StackPanel(); metrics.Children.Add(metricRows);
            foreach (var key in CoreMetricOrder)
            {
                var row = new Grid { Margin = new Thickness(0, 3, 0, 3), MinHeight = 38 }; row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var check = new WpfCheckBox { Content = key, IsChecked = _host._enabledMetrics.Contains(key), VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.Medium, Foreground = WpfBrushes.White, Style = BuildCheckBoxStyle() }; check.Checked += (_, _) => _host.SetMetricEnabled(key, true); check.Unchecked += (_, _) => _host.SetMetricEnabled(key, false); row.Children.Add(check);
                var buttons = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal }; buttons.Children.Add(Button("↑", Brush(WpfColor.FromRgb(22, 31, 42)), Brush(Muted), () => { _host.MoveMetric(key, -1); RefreshMetricOrder(metricRows); })); buttons.Children.Add(Button("↓", Brush(WpfColor.FromRgb(22, 31, 42)), Brush(Muted), () => { _host.MoveMetric(key, 1); RefreshMetricOrder(metricRows); })); Grid.SetColumn(buttons, 1); row.Children.Add(buttons); row.Tag = key; _metricRows[key] = row; metricRows.Children.Add(row);
            }
            RefreshMetricOrder(metricRows);
            metricsCard.Child = metrics; right.Children.Add(metricsCard);
            var hint = new Border { Background = Brush(WpfColor.FromArgb(16, Accent.R, Accent.G, Accent.B)), BorderBrush = Brush(WpfColor.FromArgb(42, Accent.R, Accent.G, Accent.B)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(13), Margin = new Thickness(0, 2, 0, 10) };
            hint.Child = new TextBlock { Text = L("Changes apply instantly and are saved automatically."), Foreground = Brush(WpfColor.FromRgb(173, 194, 207)), FontSize = 10, TextWrapping = TextWrapping.Wrap }; right.Children.Add(hint);
            page.Children.Add(columns); return page;
        }

        private static TextBlock CardEyebrow(string text) => new() { Text = text, Foreground = Brush(Accent), FontSize = 9, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 5) };

        private sealed record MonitorChoice(string Device, string Label);
        private WpfComboBox CreateModernComboBox() => new()
        {
            Height = 40, Padding = new Thickness(12, 6, 40, 6), Background = Brush(WpfColor.FromRgb(12, 17, 24)),
            Foreground = WpfBrushes.White, BorderBrush = Brush(WpfColor.FromRgb(44, 60, 76)), BorderThickness = new Thickness(1),
            FontSize = 12, VerticalContentAlignment = VerticalAlignment.Center, Cursor = System.Windows.Input.Cursors.Hand,
            Style = BuildComboBoxStyle(), ItemContainerStyle = BuildComboBoxItemStyle()
        };

        private static Style BuildComboBoxStyle()
        {
            var style = new Style(typeof(WpfComboBox));
            style.Setters.Add(new Setter(WpfControl.FocusVisualStyleProperty, null));
            var root = new FrameworkElementFactory(typeof(Grid));
            var frame = new FrameworkElementFactory(typeof(Border), "ComboFrame");
            frame.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
            frame.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(WpfComboBox.BackgroundProperty));
            frame.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(WpfComboBox.BorderBrushProperty));
            frame.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(WpfComboBox.BorderThicknessProperty));
            root.AppendChild(frame);

            var selected = new FrameworkElementFactory(typeof(ContentPresenter));
            selected.SetValue(FrameworkElement.NameProperty, "SelectionPresenter");
            selected.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(WpfComboBox.SelectionBoxItemProperty));
            selected.SetValue(ContentPresenter.ContentTemplateProperty, new TemplateBindingExtension(WpfComboBox.SelectionBoxItemTemplateProperty));
            selected.SetValue(ContentPresenter.ContentStringFormatProperty, new TemplateBindingExtension(WpfComboBox.SelectionBoxItemStringFormatProperty));
            selected.SetValue(ContentPresenter.MarginProperty, new Thickness(12, 0, 36, 0));
            selected.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            selected.SetValue(ContentPresenter.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Left);
            selected.SetValue(UIElement.IsHitTestVisibleProperty, false); root.AppendChild(selected);

            var toggle = new FrameworkElementFactory(typeof(ToggleButton), "DropDownToggle");
            toggle.SetValue(ToggleButton.FocusableProperty, false); toggle.SetValue(ToggleButton.ClickModeProperty, ClickMode.Press);
            toggle.SetValue(FrameworkElement.WidthProperty, 34.0); toggle.SetValue(FrameworkElement.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Right);
            toggle.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Stretch); toggle.SetBinding(ToggleButton.IsCheckedProperty, new WpfBinding("IsDropDownOpen") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            var toggleBorder = new FrameworkElementFactory(typeof(Border)); toggleBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(0, 8, 8, 0)); toggleBorder.SetValue(Border.BackgroundProperty, Brush(WpfColor.FromRgb(17, 25, 34)));
            var arrow = new FrameworkElementFactory(typeof(System.Windows.Shapes.Path), "DropDownArrow"); arrow.SetValue(System.Windows.Shapes.Path.DataProperty, Geometry.Parse("M 1 2 L 5 6 L 9 2")); arrow.SetValue(System.Windows.Shapes.Path.StrokeProperty, Brush(Accent)); arrow.SetValue(System.Windows.Shapes.Path.StrokeThicknessProperty, 1.7); arrow.SetValue(System.Windows.Shapes.Path.StrokeStartLineCapProperty, PenLineCap.Round); arrow.SetValue(System.Windows.Shapes.Path.StrokeEndLineCapProperty, PenLineCap.Round); arrow.SetValue(FrameworkElement.WidthProperty, 10.0); arrow.SetValue(FrameworkElement.HeightProperty, 7.0); arrow.SetValue(FrameworkElement.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center); arrow.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center); toggleBorder.AppendChild(arrow);
            var toggleTemplate = new ControlTemplate(typeof(ToggleButton)) { VisualTree = toggleBorder }; toggle.SetValue(WpfControl.TemplateProperty, toggleTemplate); root.AppendChild(toggle);

            var popup = new FrameworkElementFactory(typeof(Popup), "DropDownPopup");
            popup.SetValue(Popup.PlacementProperty, PlacementMode.Bottom); popup.SetValue(Popup.StaysOpenProperty, false); popup.SetValue(Popup.AllowsTransparencyProperty, true); popup.SetValue(Popup.PopupAnimationProperty, PopupAnimation.Fade); popup.SetValue(Popup.VerticalOffsetProperty, 6.0);
            popup.SetBinding(Popup.IsOpenProperty, new WpfBinding("IsDropDownOpen") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) }); popup.SetBinding(Popup.PlacementTargetProperty, new WpfBinding { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            var popupFrame = new FrameworkElementFactory(typeof(Border)); popupFrame.SetValue(Border.BackgroundProperty, Brush(WpfColor.FromRgb(15, 21, 29))); popupFrame.SetValue(Border.BorderBrushProperty, Brush(WpfColor.FromRgb(46, 64, 81))); popupFrame.SetValue(Border.BorderThicknessProperty, new Thickness(1)); popupFrame.SetValue(Border.CornerRadiusProperty, new CornerRadius(11)); popupFrame.SetValue(Border.PaddingProperty, new Thickness(5)); popupFrame.SetValue(Border.EffectProperty, new System.Windows.Media.Effects.DropShadowEffect { Color = Colors.Black, BlurRadius = 22, ShadowDepth = 7, Opacity = .52 });
            var scroll = new FrameworkElementFactory(typeof(ScrollViewer)); scroll.SetValue(ScrollViewer.CanContentScrollProperty, true); scroll.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto); scroll.SetValue(FrameworkElement.MaxHeightProperty, 260.0);
            scroll.AppendChild(new FrameworkElementFactory(typeof(ItemsPresenter))); popupFrame.AppendChild(scroll); popup.AppendChild(popupFrame); root.AppendChild(popup);

            var template = new ControlTemplate(typeof(WpfComboBox)) { VisualTree = root };
            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true }; hover.Setters.Add(new Setter(Border.BorderBrushProperty, Brush(WpfColor.FromArgb(145, Accent.R, Accent.G, Accent.B)), "ComboFrame"));
            var focus = new Trigger { Property = WpfControl.IsKeyboardFocusWithinProperty, Value = true }; focus.Setters.Add(new Setter(Border.BorderBrushProperty, Brush(Accent), "ComboFrame"));
            template.Triggers.Add(hover); template.Triggers.Add(focus); style.Setters.Add(new Setter(WpfControl.TemplateProperty, template)); return style;
        }

        private static Style BuildComboBoxItemStyle()
        {
            var style = new Style(typeof(ComboBoxItem));
            style.Setters.Add(new Setter(WpfControl.ForegroundProperty, Brush(WpfColor.FromRgb(220, 229, 237)))); style.Setters.Add(new Setter(WpfControl.BackgroundProperty, WpfBrushes.Transparent));
            style.Setters.Add(new Setter(WpfControl.PaddingProperty, new Thickness(10, 8, 10, 8))); style.Setters.Add(new Setter(WpfControl.FontSizeProperty, 12.0));
            var border = new FrameworkElementFactory(typeof(Border), "ItemFrame"); border.SetValue(Border.CornerRadiusProperty, new CornerRadius(7)); border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(WpfControl.PaddingProperty)); border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(WpfControl.BackgroundProperty));
            var content = new FrameworkElementFactory(typeof(ContentPresenter)); content.SetValue(ContentPresenter.ContentSourceProperty, "Content"); content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center); border.AppendChild(content);
            var template = new ControlTemplate(typeof(ComboBoxItem)) { VisualTree = border };
            var highlighted = new Trigger { Property = ComboBoxItem.IsHighlightedProperty, Value = true }; highlighted.Setters.Add(new Setter(WpfControl.BackgroundProperty, Brush(WpfColor.FromRgb(23, 48, 59)))); highlighted.Setters.Add(new Setter(WpfControl.ForegroundProperty, Brush(Accent)));
            var selected = new Trigger { Property = ComboBoxItem.IsSelectedProperty, Value = true }; selected.Setters.Add(new Setter(WpfControl.BackgroundProperty, Brush(WpfColor.FromRgb(20, 40, 50)))); selected.Setters.Add(new Setter(WpfControl.ForegroundProperty, Brush(Accent)));
            template.Triggers.Add(highlighted); template.Triggers.Add(selected); style.Setters.Add(new Setter(WpfControl.TemplateProperty, template)); return style;
        }

        private static Style BuildCheckBoxStyle()
        {
            var style = new Style(typeof(WpfCheckBox)); style.Setters.Add(new Setter(WpfControl.FocusVisualStyleProperty, null));
            var row = new FrameworkElementFactory(typeof(StackPanel)); row.SetValue(StackPanel.OrientationProperty, System.Windows.Controls.Orientation.Horizontal); row.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            var frame = new FrameworkElementFactory(typeof(Border), "CheckFrame"); frame.SetValue(FrameworkElement.WidthProperty, 16.0); frame.SetValue(FrameworkElement.HeightProperty, 16.0); frame.SetValue(Border.CornerRadiusProperty, new CornerRadius(5)); frame.SetValue(Border.BackgroundProperty, Brush(WpfColor.FromRgb(11, 16, 23))); frame.SetValue(Border.BorderBrushProperty, Brush(WpfColor.FromRgb(58, 75, 91))); frame.SetValue(Border.BorderThicknessProperty, new Thickness(1)); frame.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 9, 0)); frame.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            var mark = new FrameworkElementFactory(typeof(System.Windows.Shapes.Path), "CheckMark"); mark.SetValue(System.Windows.Shapes.Path.DataProperty, Geometry.Parse("M 2 8 L 6.5 12 L 14 3")); mark.SetValue(System.Windows.Shapes.Path.StrokeProperty, Brush(WpfColor.FromRgb(7, 16, 20))); mark.SetValue(System.Windows.Shapes.Path.StrokeThicknessProperty, 2.0); mark.SetValue(System.Windows.Shapes.Path.StrokeStartLineCapProperty, PenLineCap.Round); mark.SetValue(System.Windows.Shapes.Path.StrokeEndLineCapProperty, PenLineCap.Round); mark.SetValue(FrameworkElement.WidthProperty, 16.0); mark.SetValue(FrameworkElement.HeightProperty, 16.0); mark.SetValue(UIElement.OpacityProperty, 0.0); frame.AppendChild(mark); row.AppendChild(frame);
            var content = new FrameworkElementFactory(typeof(ContentPresenter)); content.SetValue(ContentPresenter.ContentSourceProperty, "Content"); content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center); row.AppendChild(content);
            var template = new ControlTemplate(typeof(WpfCheckBox)) { VisualTree = row };
            var selected = new Trigger { Property = ToggleButton.IsCheckedProperty, Value = true }; selected.Setters.Add(new Setter(Border.BackgroundProperty, Brush(Accent), "CheckFrame")); selected.Setters.Add(new Setter(Border.BorderBrushProperty, Brush(Accent), "CheckFrame")); selected.Setters.Add(new Setter(UIElement.OpacityProperty, 1.0, "CheckMark"));
            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true }; hover.Setters.Add(new Setter(Border.BorderBrushProperty, Brush(WpfColor.FromArgb(170, Accent.R, Accent.G, Accent.B)), "CheckFrame"));
            template.Triggers.Add(selected); template.Triggers.Add(hover); style.Setters.Add(new Setter(WpfControl.TemplateProperty, template)); return style;
        }

        private static Style BuildTextBoxStyle()
        {
            var style = new Style(typeof(System.Windows.Controls.TextBox)); style.Setters.Add(new Setter(WpfControl.FocusVisualStyleProperty, null));
            var frame = new FrameworkElementFactory(typeof(Border), "InputFrame"); frame.SetValue(Border.CornerRadiusProperty, new CornerRadius(9)); frame.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(WpfControl.BackgroundProperty)); frame.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(WpfControl.BorderBrushProperty)); frame.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(WpfControl.BorderThicknessProperty));
            var host = new FrameworkElementFactory(typeof(ScrollViewer), "PART_ContentHost"); host.SetValue(FrameworkElement.MarginProperty, new Thickness(3, 0, 3, 0)); frame.AppendChild(host);
            var template = new ControlTemplate(typeof(System.Windows.Controls.TextBox)) { VisualTree = frame };
            var focus = new Trigger { Property = WpfControl.IsKeyboardFocusWithinProperty, Value = true }; focus.Setters.Add(new Setter(Border.BorderBrushProperty, Brush(Accent), "InputFrame"));
            template.Triggers.Add(focus); style.Setters.Add(new Setter(WpfControl.TemplateProperty, template)); return style;
        }

        private UIElement SliderRow(string label, double min, double max, double value, Action<double> changed, string format)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 5, 0, 5) }; var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); grid.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center }); var valueText = new TextBlock { Text = FormatSliderValue(value, format), Foreground = Brush(Muted), VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(valueText, 1); grid.Children.Add(valueText); panel.Children.Add(grid);
            var visual = new Grid { Height = 26, Margin = new Thickness(0, 1, 0, 0), ClipToBounds = false };
            var rail = new Border { Height = 3, Margin = new Thickness(8, 0, 8, 0), CornerRadius = new CornerRadius(2), Background = Brush(WpfColor.FromRgb(40, 54, 68)), VerticalAlignment = VerticalAlignment.Center };
            var fill = new Border { Height = 3, Width = 0, Margin = new Thickness(8, 0, 0, 0), CornerRadius = new CornerRadius(2), Background = Brush(Accent), HorizontalAlignment = System.Windows.HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
            var thumb = new Border { Width = 14, Height = 14, Margin = new Thickness(1, 0, 0, 0), CornerRadius = new CornerRadius(7), Background = Brush(Accent), BorderBrush = Brush(WpfColor.FromRgb(214, 251, 255)), BorderThickness = new Thickness(2), HorizontalAlignment = System.Windows.HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Accent, BlurRadius = 10, ShadowDepth = 0, Opacity = .36 } };
            var slider = new Slider { Minimum = min, Maximum = max, Value = value, TickFrequency = max > 10 ? 1 : .05, Height = 26, Background = WpfBrushes.Transparent, Opacity = 0, Cursor = System.Windows.Input.Cursors.Hand, IsMoveToPointEnabled = true, Focusable = true };
            void UpdateVisual()
            {
                double travel = Math.Max(0, visual.ActualWidth - 14);
                double ratio = max <= min ? 0 : Math.Clamp((slider.Value - min) / (max - min), 0, 1);
                double offset = travel * ratio;
                thumb.Margin = new Thickness(1 + offset, 0, 0, 0);
                fill.Width = Math.Max(0, 8 + offset);
            }
            slider.ValueChanged += (_, _) =>
            {
                valueText.Text = FormatSliderValue(slider.Value, format);
                UpdateVisual(); changed(slider.Value);
            };
            visual.SizeChanged += (_, _) => UpdateVisual();
            visual.Children.Add(rail); visual.Children.Add(fill); visual.Children.Add(thumb); visual.Children.Add(slider);
            panel.Children.Add(visual); UpdateVisual(); return panel;
        }

        private static string FormatSliderValue(double value, string format) =>
            format.Contains("0.00") ? $"{value:0.00}×" : format.Contains("px") ? $"{value:0} px" : $"{value:0}%";

        private static DataTemplate TextDataTemplate(Type itemType, string property)
        {
            var text = new FrameworkElementFactory(typeof(TextBlock));
            text.SetBinding(TextBlock.TextProperty, new WpfBinding(property));
            text.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            text.SetValue(TextBlock.ForegroundProperty, Brush(WpfColor.FromRgb(229, 237, 244)));
            return new DataTemplate(itemType) { VisualTree = text };
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
                ToolTip = L("Click here, then press a key combination"),
                Background = Brush(WpfColor.FromRgb(12, 17, 24)),
                Foreground = Brush(WpfColor.FromRgb(240, 246, 251)),
                BorderBrush = Brush(WpfColor.FromRgb(48, 64, 80)),
                BorderThickness = new Thickness(1),
                CaretBrush = Brush(Accent),
                Style = BuildTextBoxStyle()
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
            var picker = CreateModernComboBox(); picker.ItemsSource = new[] { "Turquoise", "Red", "Green", "Blue", "Orange", "Black", "White" }; picker.SelectedItem = ColorName(_host._accentColor);
            picker.SelectionChanged += (_, _) => { if (picker.SelectedItem is string name) _host.SetAccentColor(ColorFromName(name)); };
            return picker;
        }

        private UIElement BuildLanguagePicker()
        {
            var picker = CreateModernComboBox(); picker.ItemsSource = new[]
            {
                new LanguageEntry("English", L("English")),
                new LanguageEntry("German", L("German")),
                new LanguageEntry("French", L("French")),
                new LanguageEntry("Japanese", L("Japanese")),
                new LanguageEntry("Chinese", L("Chinese"))
            }; picker.ItemTemplate = TextDataTemplate(typeof(LanguageEntry), nameof(LanguageEntry.Label)); picker.SelectedValuePath = nameof(LanguageEntry.Value); picker.SelectedValue = _host._uiLanguage;
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
            var picker = CreateModernComboBox(); picker.ItemsSource = new[]
            {
                new PositionEntry("Top Left", L("Top Left")),
                new PositionEntry("Top Center", L("Top Center")),
                new PositionEntry("Top Right", L("Top Right"))
            }; picker.ItemTemplate = TextDataTemplate(typeof(PositionEntry), nameof(PositionEntry.Label)); picker.SelectedValuePath = nameof(PositionEntry.Value); picker.SelectedValue = _host._overlayPosition;
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
            var check = new WpfCheckBox { Content = plugin.Instance.Name, ToolTip = plugin.Instance.Description, Foreground = WpfBrushes.White, IsChecked = _host._pluginContainers.TryGetValue(plugin, out var view) && view.Visibility == Visibility.Visible, Style = BuildCheckBoxStyle() };
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
        private static Border Card() => new() { Background = new LinearGradientBrush(Surface, WpfColor.FromRgb(13, 19, 27), 120), BorderBrush = Brush(WpfColor.FromArgb(30, 190, 210, 230)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(18), Margin = new Thickness(0, 0, 0, 12), Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Colors.Black, BlurRadius = 18, ShadowDepth = 4, Opacity = .2 } };
        private static UIElement SectionTitle(string title, string subtitle)
        {
            var section = new StackPanel { Margin = new Thickness(2, 3, 0, 15) };
            var eyebrow = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            eyebrow.Children.Add(new Border { Width = 17, Height = 1, Background = Brush(Accent), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
            eyebrow.Children.Add(new TextBlock { Text = title.ToUpperInvariant(), FontSize = 9, FontWeight = FontWeights.Bold, Foreground = Brush(Accent) }); section.Children.Add(eyebrow);
            section.Children.Add(new TextBlock { Text = subtitle, FontSize = 18, FontWeight = FontWeights.SemiBold, Foreground = WpfBrushes.White, Margin = new Thickness(0, 5, 0, 0), TextWrapping = TextWrapping.Wrap });
            return section;
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
            var button = new WpfButton { Content = label, Background = background, Foreground = foreground, BorderBrush = Brush(WpfColor.FromArgb(48, Accent.R, Accent.G, Accent.B)), BorderThickness = new Thickness(1), Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 0, 7, 0), Cursor = System.Windows.Input.Cursors.Hand, FontSize = 11, Style = BuildButtonStyle() };
            button.Click += (_, _) => action(); return button;
        }

        private static Style BuildButtonStyle()
        {
            var style = new Style(typeof(WpfButton));
            var border = new FrameworkElementFactory(typeof(Border), "ButtonBorder");
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(WpfButton.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(WpfButton.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(WpfButton.BorderThicknessProperty));
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(WpfButton.ContentProperty));
            content.SetValue(ContentPresenter.ContentTemplateProperty, new TemplateBindingExtension(WpfButton.ContentTemplateProperty));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, new TemplateBindingExtension(WpfButton.HorizontalContentAlignmentProperty));
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, new TemplateBindingExtension(WpfButton.VerticalContentAlignmentProperty));
            content.SetValue(ContentPresenter.MarginProperty, new TemplateBindingExtension(WpfButton.PaddingProperty)); border.AppendChild(content);
            var template = new ControlTemplate(typeof(WpfButton)) { VisualTree = border };
            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Border.BorderBrushProperty, Brush(WpfColor.FromArgb(130, Accent.R, Accent.G, Accent.B)), "ButtonBorder"));
            var pressed = new Trigger { Property = WpfButton.IsPressedProperty, Value = true };
            pressed.Setters.Add(new Setter(UIElement.OpacityProperty, .78));
            var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
            disabled.Setters.Add(new Setter(UIElement.OpacityProperty, .42));
            template.Triggers.Add(hover); template.Triggers.Add(pressed); template.Triggers.Add(disabled);
            style.Setters.Add(new Setter(WpfControl.TemplateProperty, template)); return style;
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
