using LibreHardwareMonitor.Hardware;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows.Input;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Windows.Threading;
using PerformanceOverlay.PluginApi;
using Forms = System.Windows.Forms;

namespace PerformanceOverlay;

public partial class MainWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x20;
    private const int WsExToolWindow = 0x80;
    private const int WsExLayered = 0x80000;

    private const int HotkeyIdReset = 1;
    private const int HotkeyIdExit = 2;
    private const int HotkeyIdReload = 3;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;
    private const int WmHotkey = 0x0312;

    private sealed class HotkeyBinding
    {
        public uint Modifiers { get; set; }
        public uint Key { get; set; }
        public string Display { get; set; } = "";
        public HotkeyBinding Clone() => new() { Modifiers = Modifiers, Key = Key, Display = Display };
    }

    private readonly Dictionary<int, HotkeyBinding> _hotkeys = new()
    {
        [HotkeyIdReset] = new() { Modifiers = ModControl | ModShift, Key = 0x52, Display = "Ctrl + Shift + R" },
        [HotkeyIdExit] = new() { Modifiers = ModControl | ModAlt, Key = 0x51, Display = "Ctrl + Alt + Q" },
        [HotkeyIdReload] = new() { Modifiers = ModControl | ModAlt, Key = 0x4C, Display = "Ctrl + Alt + L" }
    };

    private System.Windows.Media.Color _accentColor = System.Windows.Media.Color.FromRgb(53, 221, 240);
    private string SettingsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PerformanceOverlay", "settings.json");
    private string PluginDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PerformanceOverlay", "Plugins");
    private string _overlayPosition = "Top Left";
    private Forms.ToolStripMenuItem? _positionMenu;
    private Forms.ToolStripMenuItem? _pluginMenu;
    private readonly OverlayPluginManager _pluginManager = new();
    private readonly List<LoadedOverlayPlugin> _loadedPlugins = new();
    private readonly List<string> _pluginLoadErrors = new();

    private readonly Computer _computer = new()
    {
        IsCpuEnabled = true,
        IsGpuEnabled = true,
        IsMemoryEnabled = false,
        IsMotherboardEnabled = false,
        IsControllerEnabled = false,
        IsNetworkEnabled = false,
        IsStorageEnabled = false
    };

    private readonly DispatcherTimer _timer = new()
    {
        Interval = TimeSpan.FromSeconds(1)
    };

    private Process? _presentMon;
    private readonly object _fpsLock = new();
    private readonly Queue<(long Timestamp, double IntervalMs)> _fpsSamples = new();
    private double _fpsWindowDurationMs;
    private static readonly long FpsWindowTicks = Stopwatch.Frequency;

    private double _maxFps;
    private double _minFps = double.PositiveInfinity;

    private int _foregroundProcessId;

    private sealed class MetricState
    {
        public required TextBlock Block { get; init; }
        public required string Label { get; init; }
        public DateTime LastSeenUtc { get; set; } = DateTime.MinValue;
        public bool IsRevealed { get; set; }
        public bool IsPending { get; set; }
    }

    private readonly Dictionary<string, MetricState> _metrics = new();
    private static readonly TimeSpan MetricTimeout = TimeSpan.FromMinutes(5);
    private HwndSource? _source;
    private bool _isStopping;
    private bool _isFullReloading;
    private bool _fpsReceived;
    private Storyboard? _borderAnimation;
    private bool _resetFlash;
    private Forms.NotifyIcon? _trayIcon;
    private Forms.ContextMenuStrip? _trayMenu;

    private readonly DispatcherTimer _resetFlashTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(650)
    };

    public MainWindow()
    {
        InitializeComponent();

        InitializeMetricStates();
        LoadSettings();

        Loaded += OnLoaded;
        SizeChanged += (_, _) => ApplyOverlayPosition();
        Closed += OnClosed;

        _timer.Tick += (_, _) => { UpdateHardware(); RefreshMetricVisibility(); };

        _resetFlashTimer.Tick += (_, _) =>
        {
            _resetFlashTimer.Stop();
            _resetFlash = false;

            SetText(FpsMax, "FPS MAX",
                _maxFps > 0 ? $"{_maxFps:0}" : "—");
            SetText(FpsMin, "FPS MIN",
                double.IsFinite(_minFps) ? $"{_minFps:0}" : "—");
        };
    }

    private void InitializeMetricStates()
    {
        _metrics["FPS AVG"] = new() { Block = FpsAvg, Label = "FPS AVG" };
        _metrics["FPS MAX"] = new() { Block = FpsMax, Label = "FPS MAX" };
        _metrics["FPS MIN"] = new() { Block = FpsMin, Label = "FPS MIN" };
        _metrics["CPU"] = new() { Block = CpuLoad, Label = "CPU" };
        _metrics["CPU TEMP"] = new() { Block = CpuTemp, Label = "CPU TEMP" };
        _metrics["RAM"] = new() { Block = RamLoad, Label = "RAM" };
        _metrics["GPU"] = new() { Block = GpuLoad, Label = "GPU" };
        _metrics["GPU TEMP"] = new() { Block = GpuTemp, Label = "GPU TEMP" };
    }

    private void UpdateMetric(string label, string value, bool revealImmediately = false)
    {
        if (!_metrics.TryGetValue(label, out var metric)) return;
        metric.LastSeenUtc = DateTime.UtcNow;
        metric.Block.Tag = value;
        if (revealImmediately)
        {
            metric.IsRevealed = true;
            metric.IsPending = false;
            SetText(metric.Block, label, value);
        }
        else
        {
            if (!metric.IsRevealed) metric.IsPending = true;
            if (metric.IsRevealed) SetText(metric.Block, label, value);
        }
        RefreshMetricVisibility();
    }

    private void RefreshMetricVisibility()
    {
        var now = DateTime.UtcNow;
        foreach (var metric in _metrics.Values)
        {
            if (metric.LastSeenUtc == DateTime.MinValue || now - metric.LastSeenUtc > MetricTimeout)
            {
                metric.IsRevealed = false;
                metric.IsPending = false;
                metric.Block.Visibility = Visibility.Collapsed;
            }
            else
            {
                metric.Block.Visibility = metric.IsRevealed ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        AvailabilityDot.Visibility = _metrics.Values.Any(m => m.IsPending &&
            m.LastSeenUtc != DateTime.MinValue && now - m.LastSeenUtc <= MetricTimeout)
            ? Visibility.Visible : Visibility.Collapsed;

        // Separators are shown only between two visible values.
        var blocks = new[] { FpsAvg, FpsMax, FpsMin, CpuLoad, CpuTemp, RamLoad, GpuLoad, GpuTemp };
        var separators = new[] { Sep1, Sep2, Sep3, Sep4, Sep5, Sep6, Sep7 };
        for (int i = 0; i < separators.Length; i++)
            separators[i].Visibility = blocks[i].Visibility == Visibility.Visible &&
                blocks[i + 1].Visibility == Visibility.Visible
                ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RevealAvailableMetrics()
    {
        var now = DateTime.UtcNow;
        foreach (var metric in _metrics.Values)
        {
            if (metric.LastSeenUtc != DateTime.MinValue && now - metric.LastSeenUtc <= MetricTimeout)
            {
                metric.IsRevealed = true;
                metric.IsPending = false;
                SetText(metric.Block, metric.Label, metric.Block.Tag as string ?? "—");
            }
        }
        RefreshMetricVisibility();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        InitializeSystemTray();
        LoadInstalledPlugins();
        SetAccentColor(_accentColor);

        // Position is configurable from the system tray.
        Topmost = true;
        ApplyOverlayPosition();

        var hwnd = new WindowInteropHelper(this).Handle;

        // Click-through and transparent window
        var style = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();

        style |= WsExTransparent |
                 WsExToolWindow |
                 WsExLayered;

        SetWindowLongPtr(
            hwnd,
            GwlExStyle,
            new IntPtr(style)
        );

        _source = HwndSource.FromHwnd(hwnd);
        _source?.AddHook(WndProc);

        RegisterConfiguredHotkeys(hwnd, showErrors: false);

        try
        {
            _computer.Open();
        }
        catch { }

        StartPresentMon();

        _timer.Start();

        foreach (var metric in _metrics.Values)
            metric.Block.Visibility = Visibility.Collapsed;
        RefreshMetricVisibility();
    }


    // System tray
    private void InitializeSystemTray()
    {
        try
        {
            string iconPath = Path.Combine(AppContext.BaseDirectory, "app.ico");
            _trayIcon = new Forms.NotifyIcon
            {
                Icon = File.Exists(iconPath)
                    ? new System.Drawing.Icon(iconPath)
                    : System.Drawing.SystemIcons.Application,
                Text = "Performance Overlay",
                Visible = true
            };

            _trayMenu = new Forms.ContextMenuStrip();
            _trayMenu.Items.Add("Show overlay", null, (_, _) => ShowOverlayFromTray());
            _trayMenu.Items.Add("Hide overlay", null, (_, _) => HideOverlayToTray());
            _trayMenu.Items.Add(new Forms.ToolStripSeparator());

            var colorMenu = new Forms.ToolStripMenuItem("Overlay color");
            AddColorOption(colorMenu, "Red", System.Windows.Media.Colors.Red);
            AddColorOption(colorMenu, "Green", System.Windows.Media.Colors.LimeGreen);
            AddColorOption(colorMenu, "Blue", System.Windows.Media.Colors.DodgerBlue);
            AddColorOption(colorMenu, "Turquoise", System.Windows.Media.Color.FromRgb(53, 221, 240));
            AddColorOption(colorMenu, "Orange", System.Windows.Media.Colors.DarkOrange);
            AddColorOption(colorMenu, "Black", System.Windows.Media.Colors.Black);
            AddColorOption(colorMenu, "White", System.Windows.Media.Colors.White);
            _trayMenu.Items.Add(colorMenu);

            _positionMenu = new Forms.ToolStripMenuItem("Overlay position");
            AddPositionOption("Top Left");
            AddPositionOption("Top Center");
            AddPositionOption("Top Right");
            _trayMenu.Items.Add(_positionMenu);

            _pluginMenu = new Forms.ToolStripMenuItem("Plugins");
            _pluginMenu.DropDownOpening += (_, _) => RefreshPluginMenu();
            _trayMenu.Items.Add(_pluginMenu);

            _trayMenu.Items.Add("Reload everything", null, async (_, _) => await ReloadAllAsync());
            _trayMenu.Items.Add("Configure hotkeys…", null, (_, _) => ShowHotkeySettings());
            _trayMenu.Items.Add(new Forms.ToolStripSeparator());
            _trayMenu.Items.Add("Help", null, (_, _) => ShowIndicatorHelp());
            _trayMenu.Items.Add(new Forms.ToolStripSeparator());
            _trayMenu.Items.Add("Exit", null, (_, _) => StopOverlayAnimation());
            _trayIcon.ContextMenuStrip = _trayMenu;
            _trayIcon.DoubleClick += (_, _) =>
            {
                if (IsVisible) HideOverlayToTray();
                else ShowOverlayFromTray();
            };
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"The system tray icon could not be initialized:\n{ex.Message}",
                "Performance Overlay", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void LoadInstalledPlugins()
    {
        try
        {
            Directory.CreateDirectory(PluginDirectory);
            foreach (var folder in Directory.GetDirectories(PluginDirectory))
            {
                var assemblyPath = Path.Combine(folder, Path.GetFileName(folder) + ".dll");
                if (!File.Exists(assemblyPath)) continue;

                try { LoadPluginAssembly(assemblyPath); }
                catch (Exception ex)
                {
                    _pluginLoadErrors.Add($"{Path.GetFileName(folder)}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            _pluginLoadErrors.Add(ex.Message);
        }

        RefreshPluginMenu();
    }

    private void RefreshPluginMenu()
    {
        if (_pluginMenu is null) return;
        _pluginMenu.DropDownItems.Clear();
        _pluginMenu.DropDownItems.Add("Load plugin…", null, (_, _) => LoadPluginFromDialog());
        _pluginMenu.DropDownItems.Add("Open plugins folder", null, (_, _) => OpenPluginFolder());
        _pluginMenu.DropDownItems.Add(new Forms.ToolStripSeparator());

        if (_loadedPlugins.Count == 0 && _pluginLoadErrors.Count == 0)
        {
            _pluginMenu.DropDownItems.Add("No plugins loaded").Enabled = false;
            return;
        }

        foreach (var plugin in _loadedPlugins)
        {
            var item = new Forms.ToolStripMenuItem(plugin.Instance.Name)
            {
                CheckOnClick = true,
                Checked = plugin.View.Visibility == Visibility.Visible,
                ToolTipText = plugin.Instance.Description
            };
            item.CheckedChanged += (_, _) =>
                plugin.View.Visibility = item.Checked ? Visibility.Visible : Visibility.Collapsed;
            _pluginMenu.DropDownItems.Add(item);
        }

        if (_pluginLoadErrors.Count > 0)
        {
            _pluginMenu.DropDownItems.Add(new Forms.ToolStripSeparator());
            var errors = new Forms.ToolStripMenuItem($"Plugin load errors ({_pluginLoadErrors.Count})")
            {
                ToolTipText = string.Join(Environment.NewLine, _pluginLoadErrors)
            };
            errors.Click += (_, _) => System.Windows.MessageBox.Show(
                string.Join(Environment.NewLine + Environment.NewLine, _pluginLoadErrors),
                "Performance Overlay – Plugin errors",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            _pluginMenu.DropDownItems.Add(errors);
        }
    }

    private void LoadPluginFromDialog()
    {
        var permission = System.Windows.MessageBox.Show(
            "Plugins run inside PerformanceOverlay with the same permissions as the application. Only load plugins from sources you trust.",
            "Load plugin",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (permission != MessageBoxResult.Yes) return;

        Directory.CreateDirectory(PluginDirectory);
        using var dialog = new Forms.OpenFileDialog
        {
            Title = "Select a PerformanceOverlay plugin",
            Filter = "PerformanceOverlay plugin assemblies (*.dll)|*.dll",
            CheckFileExists = true,
            InitialDirectory = PluginDirectory
        };
        if (dialog.ShowDialog() != Forms.DialogResult.OK) return;

        var sourcePath = Path.GetFullPath(dialog.FileName);
        var pluginName = Path.GetFileNameWithoutExtension(sourcePath);
        var pluginFolder = Path.Combine(PluginDirectory, pluginName);
        var assemblyPath = Path.Combine(pluginFolder, pluginName + ".dll");

        try
        {
            if (!Path.GetDirectoryName(sourcePath)!.Equals(pluginFolder, StringComparison.OrdinalIgnoreCase))
            {
                if (Directory.Exists(pluginFolder))
                    throw new IOException($"A plugin named '{pluginName}' is already installed.");

                CopyPluginFiles(Path.GetDirectoryName(sourcePath)!, pluginFolder);
            }

            LoadPluginAssembly(assemblyPath);
            _pluginLoadErrors.RemoveAll(error => error.StartsWith(pluginName + ":", StringComparison.OrdinalIgnoreCase));
            RefreshPluginMenu();
        }
        catch (Exception ex)
        {
            _pluginLoadErrors.Add($"{pluginName}: {ex.Message}");
            RefreshPluginMenu();
            System.Windows.MessageBox.Show(ex.Message, "Plugin could not be loaded", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void CopyPluginFiles(string sourceFolder, string destinationFolder)
    {
        foreach (var sourcePath in Directory.EnumerateFiles(sourceFolder, "*", SearchOption.AllDirectories))
        {
            var extension = Path.GetExtension(sourcePath);
            if (!extension.Equals(".dll", StringComparison.OrdinalIgnoreCase) &&
                !extension.Equals(".json", StringComparison.OrdinalIgnoreCase) &&
                !extension.Equals(".config", StringComparison.OrdinalIgnoreCase))
                continue;

            var relativePath = Path.GetRelativePath(sourceFolder, sourcePath);
            var destinationPath = Path.Combine(destinationFolder, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(sourcePath, destinationPath);
        }
    }

    private void LoadPluginAssembly(string assemblyPath)
    {
        if (_loadedPlugins.Any(plugin => plugin.AssemblyPath.Equals(assemblyPath, StringComparison.OrdinalIgnoreCase)))
            return;

        var plugin = _pluginManager.Load(assemblyPath);
        plugin.View.Margin = new Thickness(8, 0, 0, 0);
        _loadedPlugins.Add(plugin);
        OverlayContent.Children.Add(plugin.View);
        UpdateLayout();
        ApplyOverlayPosition();
    }

    private void OpenPluginFolder()
    {
        Directory.CreateDirectory(PluginDirectory);
        Process.Start(new ProcessStartInfo(PluginDirectory) { UseShellExecute = true });
    }

    private void AddPositionOption(string position)
    {
        if (_positionMenu is null) return;
        var item = new Forms.ToolStripMenuItem(position)
        {
            Checked = string.Equals(position, _overlayPosition, StringComparison.OrdinalIgnoreCase),
            CheckOnClick = false
        };
        item.Click += (_, _) => SetOverlayPosition(position);
        _positionMenu.DropDownItems.Add(item);
    }

    private void SetOverlayPosition(string position)
    {
        _overlayPosition = position;
        ApplyOverlayPosition();
        SaveSettings();
        if (_positionMenu is not null)
            foreach (Forms.ToolStripMenuItem item in _positionMenu.DropDownItems)
                item.Checked = item.Text == _overlayPosition;
    }

    private void ApplyOverlayPosition()
    {
        if (!IsLoaded) return;
        UpdateLayout();
        var area = SystemParameters.WorkArea;
        const double margin = 8;
        Left = _overlayPosition switch
        {
            "Top Center" => area.Left + (area.Width - ActualWidth) / 2,
            "Top Right" => area.Right - ActualWidth - margin,
            _ => area.Left + margin
        };
        Top = area.Top + margin;
    }

    private void ShowIndicatorHelp()
    {
        const string help =
            "PERFORMANCE OVERLAY – INDICATOR GUIDE\n\n" +
            "GREEN DOT: At least one sensor has a new reading available. Press the reload hotkey to reveal available readings.\n\n" +
            "FPS AVG: Current average frames per second reported by PresentMon for the captured application.\n" +
            "FPS MAX / FPS MIN: Highest and lowest FPS recorded during the current session.\n\n" +
            "CPU LOAD: Processor utilization.\n" +
            "CPU TEMP: Processor temperature, when supported by the hardware.\n" +
            "RAM LOAD: System memory usage.\n" +
            "GPU LOAD: Graphics processor utilization.\n" +
            "GPU TEMP: Graphics processor temperature.\n\n" +
            "PURPLE BORDER: A full overlay reload is in progress.\n" +
            "GREEN BORDER: FPS capture is starting or waiting for FPS data.\n\n" +
            "Unavailable readings are hidden. Readings that receive no updates for five minutes are hidden again.";
        System.Windows.MessageBox.Show(help, "Performance Overlay – Help", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ShowOverlayFromTray()
    {
        if (_isStopping) return;
        if (!IsVisible) Show();
        WindowState = WindowState.Normal;
        ShowInTaskbar = false;
        Topmost = true;
        Activate();
        UpdateLayout();
    }

    private void HideOverlayToTray()
    {
        if (!_isStopping) Hide();
    }

    private void AddColorOption(Forms.ToolStripMenuItem parent, string name, System.Windows.Media.Color color)
    {
        var item = new Forms.ToolStripMenuItem(name) { Checked = color == _accentColor, CheckOnClick = false };
        item.Click += (_, _) => SetAccentColor(color);
        parent.DropDownItems.Add(item);
    }

    private void SetAccentColor(System.Windows.Media.Color color)
    {
        _accentColor = color;
        foreach (var metric in _metrics.Values)
            if (metric.Block.Tag is string value && !string.IsNullOrEmpty(value))
                SetText(metric.Block, metric.Label, value);
        OverlayBackground.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(150, color.R, color.G, color.B));
        SaveSettings();
        if (_trayMenu is not null)
        {
            foreach (Forms.ToolStripItem item in _trayMenu.Items)
                if (item is Forms.ToolStripMenuItem menu && menu.Text == "Overlay color")
                    foreach (Forms.ToolStripMenuItem option in menu.DropDownItems)
                        option.Checked = option.Text == ColorName(color);
        }
    }

    private static string ColorName(System.Windows.Media.Color c) =>
        c == System.Windows.Media.Colors.Red ? "Red" : c == System.Windows.Media.Colors.LimeGreen ? "Green" :
        c == System.Windows.Media.Colors.DodgerBlue ? "Blue" : c == System.Windows.Media.Color.FromRgb(53,221,240) ? "Turquoise" :
        c == System.Windows.Media.Colors.DarkOrange ? "Orange" : c == System.Windows.Media.Colors.Black ? "Black" : "White";

    private void LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;
            using var doc = JsonDocument.Parse(File.ReadAllText(SettingsPath));
            if (doc.RootElement.TryGetProperty("Position", out var pos) && pos.GetString() is string savedPosition &&
                new[] { "Top Left", "Top Center", "Top Right" }.Contains(savedPosition))
                _overlayPosition = savedPosition;
            if (doc.RootElement.TryGetProperty("Accent", out var a) && a.GetString() is string hex &&
                hex.Length == 6 && byte.TryParse(hex[..2], NumberStyles.HexNumber, null, out var r) &&
                byte.TryParse(hex.Substring(2,2), NumberStyles.HexNumber, null, out var g) &&
                byte.TryParse(hex.Substring(4,2), NumberStyles.HexNumber, null, out var b))
                _accentColor = System.Windows.Media.Color.FromRgb(r,g,b);
            if (doc.RootElement.TryGetProperty("Hotkeys", out var h))
                foreach (var prop in h.EnumerateObject())
                    if (int.TryParse(prop.Name, out int id) && _hotkeys.ContainsKey(id) &&
                        prop.Value.TryGetProperty("Modifiers", out var m) && prop.Value.TryGetProperty("Key", out var k))
                    {
                        _hotkeys[id].Modifiers = m.GetUInt32();
                        _hotkeys[id].Key = k.GetUInt32();
                        _hotkeys[id].Display = prop.Value.TryGetProperty("Display", out var d) ? d.GetString() ?? "" : "";
                    }
        }
        catch { }
    }

    private void SaveSettings()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(dir);
            var data = new { Accent = $"{_accentColor.R:X2}{_accentColor.G:X2}{_accentColor.B:X2}", Position = _overlayPosition, Hotkeys = _hotkeys };
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private bool RegisterConfiguredHotkeys(IntPtr hwnd, bool showErrors)
    {
        foreach (int id in _hotkeys.Keys) UnregisterHotKey(hwnd, id);
        var failed = new List<string>();
        foreach (var pair in _hotkeys)
        {
            var h = pair.Value;
            if (h.Key == 0) continue; // Hotkey bewusst deaktiviert.
            if (!RegisterHotKey(hwnd, pair.Key, h.Modifiers, h.Key)) failed.Add($"{h.Display} (error {Marshal.GetLastWin32Error()})");
        }
        if (failed.Count == 0) return true;
        if (showErrors) System.Windows.MessageBox.Show("These hotkeys could not be registered:\n" + string.Join("\n", failed), "Performance Overlay", MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;
    }

    private void ShowHotkeySettings()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var dialog = new Window { Title = "Configure hotkeys", Width = 390, Height = 280, WindowStartupLocation = WindowStartupLocation.CenterScreen, ResizeMode = ResizeMode.NoResize, Owner = this, Background = System.Windows.Media.Brushes.White };
        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock { Text = "Select a field and press the desired key combination (at least one modifier key).", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,12) });
        var boxes = new Dictionary<int, System.Windows.Controls.TextBox>();
        foreach (var pair in _hotkeys)
        {
            string label = pair.Key == HotkeyIdReset ? "Reset FPS max/min" : pair.Key == HotkeyIdExit ? "Exit overlay" : "Reload everything";
            panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0,6,0,3), FontWeight = FontWeights.SemiBold });
            var box = new System.Windows.Controls.TextBox { Text = pair.Value.Display, IsReadOnly = true, Height = 28, Tag = pair.Key };
            box.PreviewKeyDown += (_, e) =>
            {
                e.Handled = true;
                if (e.Key == Key.Escape) { box.Text = "Not set"; box.Tag = new HotkeyBinding { Modifiers = 0, Key = 0, Display = "Not set" }; return; }
                Key key = e.Key == Key.System ? e.SystemKey : e.Key;
                if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;
                uint mods = 0; var m = Keyboard.Modifiers;
                if (m.HasFlag(ModifierKeys.Control)) mods |= ModControl;
                if (m.HasFlag(ModifierKeys.Alt)) mods |= ModAlt;
                if (m.HasFlag(ModifierKeys.Shift)) mods |= ModShift;
                if (m.HasFlag(ModifierKeys.Windows)) mods |= ModWin;
                if (mods == 0) { box.Text = "Use Ctrl/Alt/Shift/Win + a key"; return; }
                uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
                string prefix = string.Join(" + ", new[] { (mods & ModControl) != 0 ? "Ctrl" : null, (mods & ModAlt) != 0 ? "Alt" : null, (mods & ModShift) != 0 ? "Shift" : null, (mods & ModWin) != 0 ? "Win" : null }.Where(x => x is not null));
                var binding = new HotkeyBinding { Modifiers = mods, Key = vk, Display = prefix + " + " + key };
                box.Tag = binding; box.Text = binding.Display;
            };
            boxes[pair.Key] = box; panel.Children.Add(box);
        }
        var buttons = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(0,16,0,0) };
        var save = new System.Windows.Controls.Button { Content = "Save", Width = 90, Margin = new Thickness(0,0,8,0), IsDefault = true };
        var cancel = new System.Windows.Controls.Button { Content = "Cancel", Width = 90, IsCancel = true };
        save.Click += (_, _) =>
        {
            var proposed = _hotkeys.ToDictionary(x => x.Key, x => x.Value.Clone());
            foreach (var pair in boxes) if (pair.Value.Tag is HotkeyBinding b) proposed[pair.Key] = b;
            var previous = _hotkeys.ToDictionary(x => x.Key, x => x.Value.Clone());
            foreach (var pair in proposed) _hotkeys[pair.Key] = pair.Value;
            if (!RegisterConfiguredHotkeys(hwnd, showErrors: true))
            {
                foreach (var pair in previous) _hotkeys[pair.Key] = pair.Value;
                RegisterConfiguredHotkeys(hwnd, showErrors: false);
                return;
            }
            SaveSettings(); dialog.DialogResult = true; dialog.Close();
        };
        buttons.Children.Add(save); buttons.Children.Add(cancel); panel.Children.Add(buttons); dialog.Content = panel; dialog.ShowDialog();
    }

    private async Task ReloadAllAsync()
    {
        if (_isStopping || _isFullReloading) return;

        _isFullReloading = true;
        StartBorderAnimation(System.Windows.Media.Colors.MediumPurple);
        try
        {
            // A reload from the tray must also bring the actual overlay back on screen.
            ShowOverlayFromTray();
            _timer.Stop();

            try { _presentMon?.Kill(true); } catch { }
            try { _presentMon?.Dispose(); } catch { }
            _presentMon = null;
            try { _computer.Close(); } catch { }

            foreach (var metric in _metrics.Values)
            {
                metric.LastSeenUtc = DateTime.MinValue;
                metric.IsRevealed = true;
                metric.IsPending = false;
                metric.Block.Tag = null;
                metric.Block.Visibility = Visibility.Collapsed;
            }

            lock (_fpsLock)
            {
                _maxFps = 0;
                _minFps = double.PositiveInfinity;
            }
            _fpsSamples.Clear();
            _fpsWindowDurationMs = 0;

            RefreshMetricVisibility();
            try { _computer.Open(); } catch { }
            StartPresentMon();
            UpdateHardware();
            RevealAvailableMetrics();
            _timer.Start();
            await Task.Delay(500);
            RevealAvailableMetrics();
            RefreshMetricVisibility();
        }
        finally
        {
            _isFullReloading = false;
            StopBorderAnimation();
            // If PresentMon has not produced a frame yet, show its own green activity sweep.
            if (!_fpsReceived && !_isStopping)
                StartFpsBorderAnimation();
        }
    }

    private void StartBorderAnimation(System.Windows.Media.Color color)
    {
        StopBorderAnimation();
        ReloadAnimationStroke.Stroke = new SolidColorBrush(color);
        ReloadAnimationStroke.Visibility = Visibility.Visible;
        var animation = new DoubleAnimation
        {
            From = 0,
            To = -260,
            Duration = TimeSpan.FromMilliseconds(1150),
            RepeatBehavior = RepeatBehavior.Forever
        };
        _borderAnimation = new Storyboard();
        _borderAnimation.Children.Add(animation);
        Storyboard.SetTarget(animation, ReloadAnimationStroke);
        Storyboard.SetTargetProperty(animation, new PropertyPath(System.Windows.Shapes.Shape.StrokeDashOffsetProperty));
        _borderAnimation.Begin(this, true);
    }

    private void StartFpsBorderAnimation()
    {
        if (_isStopping || _isFullReloading || _fpsReceived) return;
        StartBorderAnimation(System.Windows.Media.Colors.LimeGreen);
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(12));
            try
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    if (!_isFullReloading && !_fpsReceived) StopBorderAnimation();
                });
            }
            catch { }
        });
    }

    private void StopBorderAnimation()
    {
        _borderAnimation?.Stop(this);
        _borderAnimation = null;
        if (ReloadAnimationStroke is not null)
            ReloadAnimationStroke.Visibility = Visibility.Collapsed;
    }

    // Text styling
    private void SetText(
        System.Windows.Controls.TextBlock block,
        string label,
        string value)
    {
        block.Inlines.Clear();

        block.Inlines.Add(
            new System.Windows.Documents.Run(label + " ")
            {
                Foreground = new SolidColorBrush(
                    _resetFlash && (label == "FPS MAX" || label == "FPS MIN")
                        ? System.Windows.Media.Color.FromRgb(70, 230, 120)
                        : _accentColor
                ),
                FontWeight = FontWeights.SemiBold
            }
        );

        block.Inlines.Add(
            new System.Windows.Documents.Run(value)
            {
                Foreground = System.Windows.Media.Brushes.White,
                FontWeight = FontWeights.SemiBold
            }
        );
    }

    // Hardware monitoring
    private void UpdateHardware()
    {
        if (_isStopping) return;

        try
        {
            var foreground = GetForegroundWindow();

            GetWindowThreadProcessId(
                foreground,
                out var pid
            );

            // The overlay can briefly become the foreground window when its tray menu
            // triggers a reload. Keep monitoring the last real foreground application
            // instead of filtering PresentMon output down to the overlay process.
            if (pid != 0 && pid != (uint)Environment.ProcessId)
                _foregroundProcessId = unchecked((int)pid);

            float? cpuLoad = null;
            float? cpuTemp = null;
            float? gpuLoad = null;
            float? gpuTemp = null;

            foreach (var hw in _computer.Hardware)
            {
                try
                {
                    hw.Update();
                }
                catch
                {
                    continue;
                }

                ReadSensors(
                    hw,
                    ref cpuLoad,
                    ref cpuTemp,
                    ref gpuLoad,
                    ref gpuTemp
                );

                foreach (var sub in hw.SubHardware)
                {
                    try
                    {
                        sub.Update();
                    }
                    catch
                    {
                        continue;
                    }

                    ReadSensors(
                        sub,
                        ref cpuLoad,
                        ref cpuTemp,
                        ref gpuLoad,
                        ref gpuTemp
                    );
                }
            }

            double ram = GetMemoryStatus();

            if (cpuLoad is not null) UpdateMetric("CPU", $"{cpuLoad.Value:0}%");

            if (cpuTemp is not null) UpdateMetric("CPU TEMP", $"{cpuTemp.Value:0}°C");

            if (ram > 0) UpdateMetric("RAM", $"{ram:0}%");

            if (gpuLoad is not null) UpdateMetric("GPU", $"{gpuLoad.Value:0}%");

            if (gpuTemp is not null) UpdateMetric("GPU TEMP", $"{gpuTemp.Value:0}°C");
        }
        catch { }
    }

    // Read CPU and GPU sensors
    private static void ReadSensors(
        IHardware hw,
        ref float? cpuLoad,
        ref float? cpuTemp,
        ref float? gpuLoad,
        ref float? gpuTemp)
    {
        foreach (var sensor in hw.Sensors)
        {
            if (sensor.Value is not float value ||
                float.IsNaN(value) ||
                float.IsInfinity(value))
            continue;

            var name = sensor.Name.ToLowerInvariant();

            if (hw.HardwareType == HardwareType.Cpu)
            {
                if (sensor.SensorType == SensorType.Load &&
                    (name.Contains("total") ||
                     name.Contains("cpu")))
                {
                    cpuLoad ??= value;
                }

                if (sensor.SensorType == SensorType.Temperature)
                {
                    if (name.Contains("tdie") ||
                        name.Contains("tctl") ||
                        name.Contains("package"))
                    {
                        cpuTemp = value;
                    }
                    else
                    {
                        cpuTemp ??= value;
                    }
                }
            }

            if (hw.HardwareType is
                HardwareType.GpuNvidia or
                HardwareType.GpuAmd or
                HardwareType.GpuIntel)
            {
                if (sensor.SensorType == SensorType.Load &&
                    (name.Contains("core") ||
                     name.Contains("gpu") ||
                     name.Contains("d3d")))
                {
                    gpuLoad ??= value;
                }

                if (sensor.SensorType == SensorType.Temperature)
                {
                    gpuTemp ??= value;
                }
            }
        }
    }

    // RAM usage
    private static double GetMemoryStatus()
    {
        var m = new MemoryStatusEx
        {
            Length = (uint)Marshal.SizeOf<MemoryStatusEx>()
        };

        return GlobalMemoryStatusEx(ref m) &&
               m.TotalPhys > 0
            ? 100.0 * (m.TotalPhys - m.AvailPhys) /
              m.TotalPhys
            : 0;
    }

    // FPS monitoring
    private void StartPresentMon()
    {
        _fpsReceived = false;
        if (!_isFullReloading) StartFpsBorderAnimation();
        var exe = Path.Combine(
            AppContext.BaseDirectory,
            "PresentMon.exe"
        );

        if (!File.Exists(exe))
        {
            SetText(
                FpsAvg,
                "FPS AVG",
                "PresentMon not found"
            );

            return;
        }

        try
        {
            var psi = new ProcessStartInfo(
               exe,
               "--output_stdout --no_console_stats --no_track_gpu --v1_metrics --restart_as_admin"
            )
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };

            var presentMon = Process.Start(psi);
            _presentMon = presentMon;

            if (presentMon is null)
            {
                SetText(FpsAvg, "FPS AVG", "Not started");
                return;
            }

            _ = Task.Run(() => ReadPresentMonAsync(presentMon));

            _ = Task.Run(async () =>
            {
                try
                {
                    var error = await presentMon.StandardError.ReadToEndAsync();

                    if (!string.IsNullOrWhiteSpace(error) && ReferenceEquals(_presentMon, presentMon))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            if (ReferenceEquals(_presentMon, presentMon) && !_fpsReceived)
                                SetText(FpsAvg, "FPS AVG", "Capture error");
                        }
                        );
                    }
                }
                catch { }
            });
        }
        catch
        {
            SetText(FpsAvg, "FPS AVG", "Capture error");
        }
    }

    private async Task ReadPresentMonAsync(Process presentMon)
    {
        string? header = null;

        int displayColumn = -1;
        int presentColumn = -1;
        int processColumn = -1;

        try
        {
            while (!presentMon.HasExited)
            {
                var line = await presentMon.StandardOutput.ReadLineAsync();

                if (line is null)
                    break;

                if (header is null &&
                    line.Contains(
                        "Application",
                        StringComparison.OrdinalIgnoreCase
                    ) &&
                    line.Contains(
                        "MsBetween",
                        StringComparison.OrdinalIgnoreCase
                    ))
                {
                    header = line;

                    var cols = SplitCsv(line);

                    displayColumn = Array.FindIndex(
                        cols,
                        x => x.Trim().Equals(
                            "MsBetweenDisplayChange",
                            StringComparison.OrdinalIgnoreCase
                        )
                    );

                    presentColumn = Array.FindIndex(
                        cols,
                        x => x.Trim().Equals(
                            "MsBetweenPresents",
                            StringComparison.OrdinalIgnoreCase
                        )
                    );

                    processColumn = Array.FindIndex(
                        cols,
                        x => x.Trim().Equals(
                            "ProcessID",
                            StringComparison.OrdinalIgnoreCase
                        )
                    );

                    continue;
                }

                if (header is null)
                    continue;

                var values = SplitCsv(line);

                if (processColumn >= 0 &&
                    processColumn < values.Length &&
                    int.TryParse(
                        values[processColumn],
                        out var rowPid
                    ) &&
                    _foregroundProcessId != 0 &&
                    rowPid != _foregroundProcessId)
                {
                    continue;
                }

                double ms = 0;

                bool valid =
                    displayColumn >= 0 &&
                    displayColumn < values.Length &&
                    double.TryParse(
                        values[displayColumn],
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out ms
                    ) &&
                    ms > 0 &&
                    ms < 1000;

                if (!valid)
                {
                    valid =
                        presentColumn >= 0 &&
                        presentColumn < values.Length &&
                        double.TryParse(
                            values[presentColumn],
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out ms
                        ) &&
                        ms > 0 &&
                        ms < 1000;
                }

                if (!valid)
                    continue;

                var sampleTimestamp = Stopwatch.GetTimestamp();

                Dispatcher.Invoke(() =>
                {
                    if (_isStopping) return;
                    _fpsReceived = true;
                    if (!_isFullReloading) StopBorderAnimation();

                    _fpsSamples.Enqueue((sampleTimestamp, ms));
                    _fpsWindowDurationMs += ms;
                    var cutoff = sampleTimestamp - FpsWindowTicks;
                    while (_fpsSamples.Count > 0 && _fpsSamples.Peek().Timestamp < cutoff)
                        _fpsWindowDurationMs -= _fpsSamples.Dequeue().IntervalMs;

                    if (_fpsWindowDurationMs <= 0 || _fpsSamples.Count == 0)
                        return;

                    double fps = _fpsSamples.Count * 1000.0 / _fpsWindowDurationMs;
                    lock (_fpsLock)
                    {
                        _maxFps = Math.Max(_maxFps, fps);
                        _minFps = Math.Min(_minFps, fps);

                        UpdateMetric("FPS AVG", $"{fps:0}", revealImmediately: true);

                        UpdateMetric("FPS MAX", $"{_maxFps:0}", revealImmediately: true);

                        UpdateMetric("FPS MIN", $"{_minFps:0}", revealImmediately: true);
                    }
                });
            }
        }
        catch
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    if (ReferenceEquals(_presentMon, presentMon) && !_fpsReceived)
                        SetText(FpsAvg, "FPS AVG", "Capture stopped");
                });
            }
            catch { }
        }
    }

    // CSV reader
    private static string[] SplitCsv(string line)
    {
        var result = new List<string>();
        var sb = new StringBuilder();

        bool quoted = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                quoted = !quoted;
                continue;
            }

            if (c == ',' && !quoted)
            {
                result.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }

        result.Add(sb.ToString());

        return result.ToArray();
    }

    // Keyboard shortcuts
    private IntPtr WndProc(
        IntPtr hwnd,
        int msg,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (msg == WmHotkey)
        {
            if (wParam.ToInt32() == HotkeyIdReset)
            {
                lock (_fpsLock)
                {
                    _maxFps = 0;
                    _minFps = double.PositiveInfinity;
                }

                _resetFlash = true;
                _resetFlashTimer.Stop();
                _resetFlashTimer.Start();

                SetText(FpsMax, "FPS MAX", "—");
                SetText(FpsMin, "FPS MIN", "—");
            }
            else if (wParam.ToInt32() == HotkeyIdReload)
            {
                _ = ReloadAllAsync();
            }
            else if (wParam.ToInt32() == HotkeyIdExit)
            {
                StopOverlayAnimation();
            }

            handled = true;
        }

        return IntPtr.Zero;
    }

    // CTRL + ALT + Q: replace the overlay text, then wipe the gray bar away.
    private async void StopOverlayAnimation()
    {
        if (_isStopping)
            return;

        _isStopping = true;
        _timer.Stop();
        _resetFlashTimer.Stop();

        var hwnd = new WindowInteropHelper(this).Handle;
        foreach (int id in _hotkeys.Keys) UnregisterHotKey(hwnd, id);

        try { _presentMon?.Kill(true); }
        catch { }

        // Keep the original overlay dimensions while replacing all its text.
        double overlayWidth = Math.Max(1, ActualWidth);
        double overlayHeight = Math.Max(1, ActualHeight);
        SizeToContent = SizeToContent.Manual;
        Width = overlayWidth;
        Height = overlayHeight;
        OverlayBackground.Width = overlayWidth;
        OverlayBackground.Height = overlayHeight;

        OverlayContent.Children.Clear();
        OverlayContent.Orientation = System.Windows.Controls.Orientation.Horizontal;
        OverlayContent.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
        OverlayContent.VerticalAlignment = VerticalAlignment.Center;
        OverlayContent.Children.Add(new TextBlock
        {
            Text = "Made by LeoCvier",
            Foreground = System.Windows.Media.Brushes.White,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center
        });

        OverlayBackground.RenderTransformOrigin = new System.Windows.Point(1, 0.5);
        var transform = new ScaleTransform(1, 1);
        OverlayBackground.RenderTransform = transform;

        // Keep the creator credit visible for five seconds before the exit wipe.
        await Task.Delay(5000);

        var wipe = new DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(700),
            EasingFunction = new CubicEase
            {
                EasingMode = EasingMode.EaseIn
            }
        };

        transform.BeginAnimation(ScaleTransform.ScaleXProperty, wipe);
        await Task.Delay(750);
        Close();
    }

    // Cleanup
    private void OnClosed(object? sender, EventArgs e)
    {
        StopBorderAnimation();
        _timer.Stop();
        _resetFlashTimer.Stop();

        foreach (var plugin in _loadedPlugins)
        {
            try { plugin.Dispose(); }
            catch { }
        }
        _loadedPlugins.Clear();

        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }
        _trayMenu?.Dispose();
        _trayMenu = null;

        var hwnd = new WindowInteropHelper(this).Handle;

        foreach (int id in _hotkeys.Keys) UnregisterHotKey(hwnd, id);

        try { _presentMon?.Kill(true); }
        catch { }

        try { _computer.Close(); }
        catch { }

        _source?.RemoveHook(WndProc);
    }

    // Windows memory structure
    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length, MemoryLoad;

        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(
        ref MemoryStatusEx lpBuffer
    );

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(
        IntPtr hWnd,
        int nIndex
    );

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(
        IntPtr hWnd,
        int nIndex,
        IntPtr dwNewLong
    );

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(
        IntPtr hWnd,
        int id,
        uint fsModifiers,
        uint vk
    );

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(
        IntPtr hWnd,
        int id
    );

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(
        IntPtr hWnd,
        out uint processId
    );
}
