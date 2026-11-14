using System.ComponentModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Nexus.Core;
using Nexus.Cursor;
using Nexus.Desktop;
using Nexus.Productivity;
using Forms = System.Windows.Forms;

namespace Nexus;

public sealed class MainWindow : Window, IDisposable
{
    private readonly ConfigurationStore _store;
    private readonly AppConfig _config;
    private readonly Diagnostics _diagnostics;
    private readonly InputService _input = new();
    private readonly HotkeyManager _hotkeys = new();
    private readonly ModuleManager _manager;
    private readonly List<IDisposable> _bindings = [];
    private readonly List<Action> _moduleStatusRefreshers = [];
    private readonly List<Action> _overviewRefreshers = [];
    private readonly Dictionary<string, Button> _navigation = [];
    private readonly TextBlock _activity = Ui.Text("0 modules running", 12, "#A9B1C1");
    private ScrollViewer? _pageScroller;
    private readonly Forms.NotifyIcon _tray;
    private readonly StackPanel _page = new();
    private readonly TextBlock _notice = Ui.Text("", 12, "#72E3CA");
    private readonly TextBlock _runtime = Ui.Text("", 11, "#8890AA");
    private TextBlock? _overlayMetrics;
    public int ModuleCount => _manager.Entries.Count;
    private readonly DispatcherTimer _statsTimer;
    private readonly DispatcherTimer _updateTimer;
    private CancellationTokenSource _networkOperations = new();
    private bool _updateChecking;
    private bool _updateDownloading;
    private ReleaseInfo? _availableUpdate;
    private string _updateMessage = "Check for a newer Nexus release.";
    private TextBlock? _updateStatus;
    private Button? _checkUpdateButton, _installUpdateButton;
    private string _section = "Overview";
    private bool _exiting;

    public MainWindow(string directory, Diagnostics diagnostics, bool smoke = false)
    {
        _diagnostics = diagnostics; _store = new(directory); _config = _store.Load();
        if (string.IsNullOrWhiteSpace(_config.UpdateUrl) && string.IsNullOrWhiteSpace(_config.UpdatePublicKey)) { _config.UpdateUrl = NexusRelease.ManifestUrl; _config.UpdatePublicKey = UpdatePublisher.PublicKey; }
        Overlay.TargetFrameRate = _config.OverlayFps;
        Title = "Nexus"; Width = 1180; Height = 880; MinWidth = 920; MinHeight = 640; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Ui.Brush("#0D0F19"); Foreground = Ui.Brush("#ECEAF7"); FontFamily = new FontFamily("Segoe UI");
        Icon = BitmapFrame.Create(new Uri("pack://application:,,,/Nexus;component/Assets/nexus.ico"));
        var context = new ModuleContext(_input, _hotkeys, diagnostics, () => _config.ReducedMotion || !SystemParameters.ClientAreaAnimation, capability => (_config.Grants & capability) == capability);
        _manager = new(_config, context, Save);
        foreach (var module in CursorCatalog.Create().Concat(DesktopCatalog.Create()).Concat(ProductivityCatalog.Create())) _manager.Register(module);
        _manager.Changed += () => { UpdateTray(); foreach (var refresh in _moduleStatusRefreshers) refresh(); foreach (var refresh in _overviewRefreshers) refresh(); _activity.Text = $"{_manager.Entries.Count(e => e.Status == ModuleStatus.Running)} modules running"; };
        BuildShell(); if (!smoke) BindHotkeys();
        using var iconStream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Nexus;component/Assets/nexus.ico")).Stream;
        _tray = new Forms.NotifyIcon { Icon = new System.Drawing.Icon(iconStream), Text = "Nexus", Visible = true };
        var menu = new Forms.ContextMenuStrip(); menu.Items.Add("Open Nexus", null, (_, _) => ShowHub()); menu.Items.Add("Pause all modules", null, (_, _) => { _manager.PauseAll(); Navigate(_section); }); menu.Items.Add("Exit Nexus", null, (_, _) => Exit());
        _tray.ContextMenuStrip = menu; _tray.DoubleClick += (_, _) => ShowHub();
        _statsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) }; _statsTimer.Tick += (_, _) => { _manager.CheckHealth(); _runtime.Text = Diagnostics.Summary(); if (_overlayMetrics is not null) _overlayMetrics.Text = Overlay.PerformanceSummary; }; _statsTimer.Start();
        _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) }; _updateTimer.Tick += async (_, _) => await BackgroundUpdateCheck(); _updateTimer.Start();
        Dispatcher.BeginInvoke(async () => await BackgroundUpdateCheck());
        Navigate("Overview"); _notice.Text = _store.RecoveryMessage;
        Closing += OnClosing;
    }
    private void BuildShell()
    {
        var root = new Grid { Background = Background }; root.ColumnDefinitions.Add(new() { Width = new GridLength(210) }); root.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        var rail = new DockPanel { Background = Ui.Brush("#11151F"), Margin = new Thickness(0, 0, 1, 0) };
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(24, 26, 20, 26) }; DockPanel.SetDock(brand, Dock.Top);
        brand.Children.Add(Ui.NexusMark()); brand.Children.Add(new TextBlock { Text = "Nexus", FontSize = 24, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center }); rail.Children.Add(brand);
        var footer = new StackPanel { Margin = new Thickness(22) }; DockPanel.SetDock(footer, Dock.Bottom);
        footer.Children.Add(Ui.Text($"Version {NexusRelease.Version}", 11, "#A9B1C1")); footer.Children.Add(_runtime); rail.Children.Add(footer);
        var nav = new StackPanel { Margin = new Thickness(12, 0, 12, 0) };
        foreach (var name in new[] { "Overview", "All modules", "Cursor & mouse", "Desktop", "Productivity", "Profiles", "Hotkeys", "Permissions", "Settings & updates" })
        {
            var button = Ui.Button(name, () => Navigate(name)); button.HorizontalAlignment = HorizontalAlignment.Stretch; button.HorizontalContentAlignment = HorizontalAlignment.Left; button.BorderThickness = new Thickness(1); button.BorderBrush = Brushes.Transparent; button.Background = Brushes.Transparent; button.Padding = new Thickness(12, 10, 8, 10); button.Margin = new Thickness(0, 2, 0, 2);
            _navigation.Add(name, button); nav.Children.Add(button);
        }
        rail.Children.Add(nav); root.Children.Add(rail);
        var body = new DockPanel { Margin = new Thickness(30, 22, 26, 18) }; Grid.SetColumn(body, 1);
        var top = new Grid { Margin = new Thickness(0, 0, 0, 20) }; DockPanel.SetDock(top, Dock.Top); top.ColumnDefinitions.Add(new()); top.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        _activity.Margin = new Thickness(0); _activity.VerticalAlignment = VerticalAlignment.Center; top.Children.Add(_activity);
        var pause = Ui.Button("Pause all  ·  Ctrl+Alt+P", () => { _manager.PauseAll(); Navigate(_section); }); Grid.SetColumn(pause, 1); top.Children.Add(pause); body.Children.Add(top);
        DockPanel.SetDock(_notice, Dock.Bottom); body.Children.Add(_notice);
        _pageScroller = new ScrollViewer { Content = _page, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }; body.Children.Add(_pageScroller); root.Children.Add(body); Content = root;
    }
    private void Navigate(string section)
    {
        _section = section; _moduleStatusRefreshers.Clear(); _overviewRefreshers.Clear(); _page.Children.Clear(); _pageScroller?.ScrollToTop();
        foreach (var (name, button) in _navigation)
        {
            bool selected = name == section; button.Background = selected ? Ui.Brush("#252A3B") : Brushes.Transparent; button.BorderBrush = selected ? Ui.Brush("#424961") : Brushes.Transparent; button.Foreground = Ui.Brush(selected ? "#F0EEFA" : "#AFB7C8");
            System.Windows.Automation.AutomationProperties.SetItemStatus(button, selected ? "Current page" : "");
        }
        switch (section)
        {
            case "Overview": Overview(); break;
            case "Profiles": ProfilesPage(); break;
            case "Hotkeys": HotkeysPage(); break;
            case "Permissions": PermissionsPage(); break;
            case "Settings & updates": SettingsPage(); break;
            default: ModulesPage(section == "All modules" ? null : section); break;
        }
    }
    private void Heading(string title, string subtitle) { _page.Children.Add(Ui.Text(title, 28)); _page.Children.Add(Ui.Text(subtitle, 14, "#A6ACC5")); _page.Children.Add(new Border { Height = 8 }); }
    private void Overview()
    {
        Heading("Overview", "Module status and common actions.");
        var stats = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3, Margin = new Thickness(0, 2, 0, 20) };
        foreach (var item in new (string Label, Func<int> Count)[] {
            ("Running", () => _manager.Entries.Count(e => e.Status == ModuleStatus.Running)),
            ("Need permission", () => _manager.Entries.Count(e => e.Status == ModuleStatus.Blocked)),
            ("Installed", () => _manager.Entries.Count(e => _manager.State(e.Manifest.Id).Installed)) })
        {
            var column = new StackPanel(); var value = Ui.Text(item.Count().ToString(), 27); column.Children.Add(value); column.Children.Add(Ui.Text(item.Label, 12, "#A9B1C1"));
            _overviewRefreshers.Add(() => value.Text = item.Count().ToString()); var card = Ui.Card(column); card.Margin = new Thickness(0, 0, 10, 0); stats.Children.Add(card);
        }
        _page.Children.Add(stats);
        var library = new StackPanel(); library.Children.Add(Ui.Text("Module library", 19)); library.Children.Add(Ui.Text("Enable utilities, change settings, and review access requirements.", 13, "#A9B1C1"));
        var actions = new WrapPanel(); actions.Children.Add(Ui.Button("Open modules", () => Navigate("All modules"))); actions.Children.Add(Ui.Button("Permissions", () => Navigate("Permissions"))); library.Children.Add(actions); _page.Children.Add(Ui.Card(library));
        _page.Children.Add(Ui.Text("Quick access", 18));
        var row = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
        foreach (string id in new[] { "scroll-flow", "cursor-blur" }) { var card = ModuleCard(_manager.Entries.Single(e => e.Manifest.Id == id)); card.Margin = new Thickness(0, 0, 10, 12); row.Children.Add(card); }
        _page.Children.Add(row);
        _page.Children.Add(Ui.Text("Ctrl+Alt+N opens Nexus. Ctrl+Alt+P stops all modules. Closing this window keeps Nexus in the tray.", 12, "#A9B1C1"));
    }
    private void ModulesPage(string? category)
    {
        Heading(category ?? "Modules", "Enable, disable, and configure utilities.");
        var grid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
        foreach (var entry in _manager.Entries.Where(e => category is null || e.Manifest.Category == category)) { var card = ModuleCard(entry); card.Margin = new Thickness(0, 0, 12, 12); grid.Children.Add(card); }
        _page.Children.Add(grid);
    }
    private Border ModuleCard(ModuleEntry entry)
    {
        var state = _manager.State(entry.Manifest.Id); var stack = new StackPanel();
        stack.Children.Add(Ui.Text(entry.Manifest.Category, 11, "#9592AF")); stack.Children.Add(Ui.Text(entry.Manifest.Name, 20));
        stack.Children.Add(new TextBlock { Text = entry.Manifest.Description, TextWrapping = TextWrapping.Wrap, Foreground = Ui.Brush("#A6ACC5"), MinHeight = 42, FontSize = 13 });
        var statusText = Ui.Text("", 11); var errorText = Ui.Text("", 12, "#FFC58C");
        void RefreshStatus() { statusText.Text = "●  " + entry.Status; statusText.Foreground = Ui.Brush(entry.Status == ModuleStatus.Running ? "#72E3CA" : "#B0A4DC"); errorText.Text = entry.Error; errorText.Visibility = entry.Error.Length == 0 ? Visibility.Collapsed : Visibility.Visible; }
        _moduleStatusRefreshers.Add(RefreshStatus); RefreshStatus(); stack.Children.Add(statusText); stack.Children.Add(errorText);
        var actions = new WrapPanel(); actions.Children.Add(Ui.Button(state.Installed ? (state.Enabled ? "Disable" : "Enable") : "Install", () => Guard(() =>
        {
            if (!state.Installed) _manager.Install(entry.Manifest.Id, true); else _manager.SetEnabled(entry.Manifest.Id, !state.Enabled);
            Navigate(_section);
        })));
        if (entry.Module.SupportsConfiguration) actions.Children.Add(Ui.Button("Configure", () => FeaturePage(entry)));
        else if (state.Installed) actions.Children.Add(Ui.Button("Uninstall feature", () => Guard(() => { _manager.Install(entry.Manifest.Id, false); Navigate(_section); })));
        if (entry.Status == ModuleStatus.Blocked) actions.Children.Add(Ui.Button("Permissions", () => Navigate("Permissions")));
        if (entry.Manifest.Id == "cursor-blur" && entry.Error.Contains("Visual C++")) actions.Children.Add(Ui.Button("Runtime help", () => Ui.Launch("https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist")));
        stack.Children.Add(actions); return Ui.Card(stack);
    }
    private void FeaturePage(ModuleEntry entry)
    {
        _moduleStatusRefreshers.Clear(); _overviewRefreshers.Clear(); _page.Children.Clear(); _pageScroller?.ScrollToTop(); var state = _manager.State(entry.Manifest.Id);
        Heading(entry.Manifest.Name, entry.Manifest.Description); _page.Children.Add(Ui.Text($"Status: {entry.Status}   ·   Capabilities: {entry.Manifest.Permissions}", 12, "#B5ACD8"));
        if (entry.Error.Length > 0) _page.Children.Add(Ui.Text(entry.Error, 13, "#FFC58C"));
        var options = state.Options.Clone(); var editor = new StackPanel();
        foreach (var field in entry.Module.Schema) editor.Children.Add(OptionEditor(field, options));
        if (entry.Module.CreatePanel(options) is { } extra) editor.Children.Add(extra);
        if (editor.Children.Count > 0) _page.Children.Add(Ui.Card(editor));
        var buttons = new WrapPanel();
        buttons.Children.Add(Ui.Button("Apply changes", () => Guard(() => { options.Validate(); state.Options = options; _manager.Refresh(entry.Manifest.Id); Save(); FeaturePage(entry); _notice.Text = "Settings saved."; })));
        buttons.Children.Add(Ui.Button(state.Enabled ? "Disable module" : "Enable module", () => Guard(() => { _manager.SetEnabled(entry.Manifest.Id, !state.Enabled); FeaturePage(entry); })));
        buttons.Children.Add(Ui.Button(state.Installed ? "Uninstall feature" : "Install feature", () => Guard(() => { _manager.Install(entry.Manifest.Id, !state.Installed); FeaturePage(entry); })));
        buttons.Children.Add(Ui.Button("Back to library", () => Navigate("All modules"))); _page.Children.Add(buttons);
        _page.Children.Add(Ui.Text("Uninstall removes this feature's options and releases its resources. Shared family binaries remain in the unified app.", 12, "#8F97B3"));
    }
    private static FrameworkElement OptionEditor(Option field, ModuleOptions options)
    {
        var property = typeof(ModuleOptions).GetProperty(field.Key)!; var row = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
        row.Children.Add(Ui.Text(field.Label, 13));
        if (field.IsToggle)
        {
            var control = new CheckBox { Content = field.Label, IsChecked = (bool)property.GetValue(options)! }; control.Click += (_, _) => property.SetValue(options, control.IsChecked == true); row.Children.Add(control);
        }
        else if (field.Choices is { } choices)
        {
            var current = (string)property.GetValue(options)!; if (!choices.Contains(current)) { current = choices[0]; property.SetValue(options, current); }
            var control = new ComboBox { ItemsSource = choices, SelectedItem = current }; control.SelectionChanged += (_, _) => property.SetValue(options, (string)control.SelectedItem); row.Children.Add(control); System.Windows.Automation.AutomationProperties.SetName(control, field.Label);
        }
        else if (field.IsText)
        {
            var control = new TextBox { Text = (string)property.GetValue(options)! }; control.TextChanged += (_, _) => property.SetValue(options, control.Text); row.Children.Add(control); System.Windows.Automation.AutomationProperties.SetName(control, field.Label);
        }
        else
        {
            var value = Ui.Text($"{(double)property.GetValue(options)!:0.##}", 12, "#B8A8FF");
            var control = new Slider { Minimum = field.Min, Maximum = field.Max, Value = (double)property.GetValue(options)!, SmallChange = (field.Max - field.Min) / 100, LargeChange = (field.Max - field.Min) / 10 };
            control.ValueChanged += (_, _) => { property.SetValue(options, control.Value); value.Text = control.Value.ToString("0.##"); }; row.Children.Add(control); row.Children.Add(value); System.Windows.Automation.AutomationProperties.SetName(control, field.Label);
        }
        return row;
    }
    private void ProfilesPage()
    {
        Heading("Profiles", "Save and switch module settings. Exports exclude clipboard history and permissions.");
        var name = new TextBox { Text = "My workspace", MaxLength = 80, Margin = new Thickness(0, 0, 0, 10) }; System.Windows.Automation.AutomationProperties.SetName(name, "Profile name"); _page.Children.Add(name);
        _page.Children.Add(Ui.Button("Save current profile", () => Guard(() =>
        {
            if (string.IsNullOrWhiteSpace(name.Text)) throw new InvalidOperationException("Enter a profile name.");
            _config.Profiles.RemoveAll(p => p.Name == name.Text.Trim()); _config.Profiles.Add(ConfigurationStore.Snapshot(name.Text, _config)); Save(); Navigate("Profiles");
        })));
        _page.Children.Add(Ui.Button("Import profile…", () => Guard(() => { var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Nexus profile|*.json" }; if (dialog.ShowDialog() == true) { _config.Profiles.Add(ConfigurationStore.Import(dialog.FileName)); Save(); Navigate("Profiles"); } })));
        foreach (var profile in _config.Profiles)
        {
            var row = new StackPanel(); row.Children.Add(Ui.Text(profile.Name, 20)); var buttons = new WrapPanel();
            buttons.Children.Add(Ui.Button("Apply", () => Guard(() =>
            {
                _manager.PauseAll();
                var snapshot = ConfigurationStore.Clone(profile.Modules);
                foreach (var entry in _manager.Entries) _config.Modules[entry.Manifest.Id] = snapshot.GetValueOrDefault(entry.Manifest.Id) ?? new();
                _manager.RefreshAll(); Navigate("Profiles"); _notice.Text = "Profile applied. Modules without permissions remain blocked.";
            })));
            buttons.Children.Add(Ui.Button("Export…", () => Guard(() => { var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "Nexus profile|*.json", FileName = "nexus-profile.json" }; if (dialog.ShowDialog() == true) File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(profile, AppConfig.JsonOptions)); })));
            buttons.Children.Add(Ui.Button("Delete", () => { _config.Profiles.Remove(profile); Save(); Navigate("Profiles"); })); row.Children.Add(buttons); _page.Children.Add(Ui.Card(row));
        }
    }
    private void HotkeysPage()
    {
        Heading("Hotkeys", "Global shortcuts work while the hub is hidden. Conflicts are reported without taking over other apps.");
        _page.Children.Add(Ui.Text("Ctrl+Alt+P  ·  Emergency pause (fixed)\nCtrl+Alt+N  ·  Show Nexus (fixed)\nWindowGlide: Ctrl+Alt+arrows · ClipVault: Ctrl+Alt+V\nScreenRuler: Ctrl+Alt+R / C · QuickDock: Ctrl+Alt+Q", 14, "#A6ACC5"));
        foreach (var entry in _manager.Entries)
        {
            var row = new StackPanel(); row.Children.Add(Ui.Text("Toggle " + entry.Manifest.Name)); var box = new TextBox { Text = _config.Hotkeys.GetValueOrDefault(entry.Manifest.Id, ""), MaxLength = 60 };
            System.Windows.Automation.AutomationProperties.SetName(box, entry.Manifest.Name + " shortcut"); row.Children.Add(box);
            row.Children.Add(Ui.Button("Save shortcut", () => Guard(() => { if (!string.IsNullOrWhiteSpace(box.Text)) Hotkey.Parse(box.Text); _config.Hotkeys[entry.Manifest.Id] = box.Text; BindHotkeys(); Save(); })));
            _page.Children.Add(Ui.Card(row));
        }
    }
    private void BindHotkeys()
    {
        foreach (var binding in _bindings) binding.Dispose(); _bindings.Clear(); var errors = new List<string>();
        void Bind(string key, Action action) { try { _bindings.Add(_hotkeys.Register(key, action)); } catch (Exception e) { errors.Add(key + ": " + e.Message); } }
        Bind("Ctrl+Alt+P", () => { _manager.PauseAll(); Navigate(_section); }); Bind("Ctrl+Alt+N", ShowHub);
        foreach (var entry in _manager.Entries)
            if (_config.Hotkeys.TryGetValue(entry.Manifest.Id, out var key) && !string.IsNullOrWhiteSpace(key)) Bind(key, () => Guard(() => { _manager.SetEnabled(entry.Manifest.Id, !_manager.State(entry.Manifest.Id).Enabled); Navigate(_section); }));
        _notice.Text = errors.Count == 0 ? "Hotkeys registered." : string.Join("\n", errors);
    }
    private void PermissionsPage()
    {
        Heading("Permissions", "Allow the capabilities required by the modules you use.");
        foreach (var (capability, description) in new[] { (Capability.Mouse, "Observe mouse activity and transform wheel events"), (Capability.Keyboard, "Show keystrokes in KeyCast — may expose typed information on screen"), (Capability.Screen, "Capture screen pixels for magnification and color picking"), (Capability.Clipboard, "Collect clipboard text in memory while ClipVault is enabled"), (Capability.Windows, "Move, dim or change the opacity of other windows"), (Capability.Network, "Fetch weather and check or download signed updates") })
        {
            var check = new CheckBox { Content = new TextBlock { Text = description, TextWrapping = TextWrapping.Wrap }, IsChecked = (_config.Grants & capability) == capability };
            check.Click += (_, _) => Guard(() =>
            {
                if (check.IsChecked == true) _config.Grants |= capability;
                else { _config.Grants &= ~capability; if (capability == Capability.Network) { _networkOperations.Cancel(); _networkOperations.Dispose(); _networkOperations = new(); } }
                _manager.RefreshAll();
            });
            _page.Children.Add(Ui.Card(check));
        }
        _page.Children.Add(Ui.Text("No administrator access is requested. Secure desktop and some elevated, fullscreen or protected applications are outside Nexus's access.", 12, "#A6ACC5"));
    }
    private void SettingsPage()
    {
        Heading("Settings", "Application preferences, rendering and updates.");
        var motion = new CheckBox { Content = "Reduce motion (Windows accessibility preference is also respected)", IsChecked = _config.ReducedMotion }; motion.Click += (_, _) => { _config.ReducedMotion = motion.IsChecked == true; Save(); }; _page.Children.Add(Ui.Card(motion));
        _page.Children.Add(Ui.Text("Overlay rendering", 22));
        var rate = new ComboBox { ItemsSource = new[] { 60, 90, 120, 144, 165, 240 }, SelectedItem = _config.OverlayFps, Margin = new Thickness(0, 4, 0, 12) };
        System.Windows.Automation.AutomationProperties.SetName(rate, "Overlay target frames per second");
        rate.SelectionChanged += (_, _) => { if (rate.SelectedItem is int fps) { _config.OverlayFps = fps; Overlay.TargetFrameRate = fps; Save(); } }; _page.Children.Add(rate);
        _overlayMetrics = Ui.Text(Overlay.PerformanceSummary, 13, "#A6ACC5"); _page.Children.Add(_overlayMetrics);
        _page.Children.Add(Ui.Text("60 FPS by default; higher targets follow the display's composition cadence. Measured rendering can be lower under load or on a slower display.", 12, "#A6ACC5"));
        _page.Children.Add(Ui.Text($"Nexus {NexusRelease.Version} · native Windows / .NET 10\n" + Diagnostics.Summary() + "\nLast local error: " + _diagnostics.LastError, 13, "#A6ACC5"));
        _page.Children.Add(Ui.Button("Open local settings and diagnostics", () => Ui.Launch(_store.DirectoryPath)));
        _page.Children.Add(Ui.Button("View Nexus license", LicenseNotice.Show));
        _page.Children.Add(Ui.Text("Nexus updates", 22));
        _page.Children.Add(Ui.Text("Official releases are checked against the publisher's signature and the downloaded file's SHA-256. Updating replaces the app and keeps your settings. Portable copies receive a per-user installation.", 13, "#A6ACC5"));
        _updateStatus = Ui.Text(_updateMessage, 15, "#72E3CA"); _page.Children.Add(_updateStatus);
        _checkUpdateButton = Ui.Button("Check for updates", () => { });
        _checkUpdateButton.Click += async (_, _) => await CheckForUpdates(false);
        _installUpdateButton = Ui.Button("Download and install update", () => { });
        _installUpdateButton.Click += async (_, _) => await InstallAvailableUpdate();
        _page.Children.Add(_checkUpdateButton); _page.Children.Add(_installUpdateButton);
        var automatic = new CheckBox { Content = "Check automatically once per day (installation remains your choice)", IsChecked = _config.AutoCheckUpdates };
        automatic.Click += (_, _) => { _config.AutoCheckUpdates = automatic.IsChecked == true; Save(); }; _page.Children.Add(automatic);
        if ((_config.Grants & Capability.Network) == 0) _page.Children.Add(Ui.Button("Review network permission", () => Navigate("Permissions")));
        _page.Children.Add(Ui.Text("CursorBlur uses its original native engine. Nexus's overlay frame target and reduced-motion controls apply to Nexus-rendered effects; disable CursorBlur to stop its motion.", 12, "#A6ACC5"));
        var advanced = new StackPanel();
        advanced.Children.Add(Ui.Text("The official feed and trusted public key are included. Change these only to use a publisher you trust. This signature is separate from a Windows Authenticode signature.", 12, "#A6ACC5"));
        var url = new TextBox { Text = _config.UpdateUrl, Margin = new Thickness(0, 8, 0, 8) }; System.Windows.Automation.AutomationProperties.SetName(url, "Update manifest HTTPS URL"); advanced.Children.Add(url);
        var key = new TextBox { Text = _config.UpdatePublicKey, AcceptsReturn = true, MinHeight = 80, MaxHeight = 160, TextWrapping = TextWrapping.Wrap }; System.Windows.Automation.AutomationProperties.SetName(key, "Trusted RSA public key PEM"); advanced.Children.Add(key);
        advanced.Children.Add(Ui.Button("Save update source", () => Guard(() =>
        {
            if (_updateChecking || _updateDownloading) throw new InvalidOperationException("Wait for the current update operation to finish.");
            if (!Uri.TryCreate(url.Text.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != "https") throw new InvalidDataException("Use an HTTPS manifest URL.");
            using var rsa = System.Security.Cryptography.RSA.Create(); rsa.ImportFromPem(key.Text);
            _config.UpdateUrl = uri.AbsoluteUri; _config.UpdatePublicKey = key.Text; _config.LastUpdateCheck = default; Save();
            _availableUpdate = null; _updateMessage = "Update source saved. Check for a release when ready."; RefreshUpdateControls();
        })));
        _page.Children.Add(new Expander { Header = "Advanced update source", Content = advanced, Margin = new Thickness(0, 16, 0, 12) });
        RefreshUpdateControls(); _page.Children.Add(Ui.Button("Exit Nexus", Exit));
    }
    private void Save() => _store.Save(_config);
    private void RefreshUpdateControls()
    {
        if (_exiting) return;
        if (_updateStatus is not null) _updateStatus.Text = _updateMessage;
        if (_checkUpdateButton is not null) _checkUpdateButton.IsEnabled = !_updateChecking && !_updateDownloading;
        if (_installUpdateButton is not null)
        {
            _installUpdateButton.Visibility = _availableUpdate is null ? Visibility.Collapsed : Visibility.Visible;
            _installUpdateButton.IsEnabled = !_updateChecking && !_updateDownloading;
            if (_availableUpdate is not null) _installUpdateButton.Content = "Download and install Nexus " + _availableUpdate.Version;
        }
    }
    private async Task BackgroundUpdateCheck()
    {
        if (_exiting || _updateChecking || _updateDownloading || !_config.AutoCheckUpdates || (_config.Grants & Capability.Network) == 0) return;
        var elapsed = DateTimeOffset.UtcNow - _config.LastUpdateCheck;
        if (elapsed >= TimeSpan.Zero && elapsed < TimeSpan.FromDays(1)) return;
        await CheckForUpdates(true);
    }
    private async Task CheckForUpdates(bool background)
    {
        if (_exiting || _updateChecking || _updateDownloading) return;
        if ((_config.Grants & Capability.Network) == 0) { _updateMessage = "Allow network access on Permissions before checking for updates."; RefreshUpdateControls(); return; }
        _updateChecking = true; _updateMessage = "Checking the signed Nexus release feed…"; RefreshUpdateControls();
        var token = _networkOperations.Token;
        try
        {
            // Failed attempts count too; an offline endpoint is never polled every minute.
            _config.LastUpdateCheck = DateTimeOffset.UtcNow; Save();
            var release = await UpdateService.Check(_config.UpdateUrl, _config.UpdatePublicKey, token); token.ThrowIfCancellationRequested();
            if (_exiting) return;
            _availableUpdate = Version.Parse(release.Version) > NexusRelease.Current ? release : null;
            _updateMessage = _availableUpdate is not null ? $"Nexus {release.Version} is available. Your settings will be kept." : $"You're up to date — Nexus {NexusRelease.Version}.";
            if (_availableUpdate is not null) _notice.Text = $"Nexus {release.Version} is available. Open Settings & updates to install it.";
        }
        catch (OperationCanceledException) { _availableUpdate = null; _updateMessage = "Update check cancelled."; }
        catch (Exception exception) { _availableUpdate = null; _updateMessage = "Could not check for updates: " + exception.Message; _diagnostics.Log("update-check", exception); if (!background) _notice.Text = _updateMessage; }
        finally { _updateChecking = false; RefreshUpdateControls(); }
    }
    private async Task InstallAvailableUpdate()
    {
        if (_exiting || _updateChecking || _updateDownloading || _availableUpdate is not { } release) return;
        if ((_config.Grants & Capability.Network) == 0) { _updateMessage = "Allow network access on Permissions before downloading."; RefreshUpdateControls(); return; }
        _updateDownloading = true; _updateMessage = "Preparing the verified update…"; RefreshUpdateControls();
        var token = _networkOperations.Token;
        try
        {
            var progress = new Progress<DownloadProgress>(value => { if (!_exiting) { _updateMessage = value.Description; RefreshUpdateControls(); } });
            string path = await UpdateService.Download(release, Path.Combine(_store.DirectoryPath, "updates"), token, progress); token.ThrowIfCancellationRequested();
            if (_exiting) return;
            var start = new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add("--apply-update"); start.ArgumentList.Add(Environment.ProcessId.ToString());
            using var worker = System.Diagnostics.Process.Start(start) ?? throw new IOException("Windows could not start the update installer.");
            Exit();
        }
        catch (OperationCanceledException) { _updateMessage = "Update download cancelled. Your current app was kept."; }
        catch (Exception exception) { _updateMessage = "Update could not finish: " + exception.Message; _diagnostics.Log("update-install", exception); }
        finally { _updateDownloading = false; RefreshUpdateControls(); }
    }
    private void Guard(Action action) { try { action(); } catch (Exception e) { _notice.Text = e.Message; _diagnostics.Log("action", e); } }
    private void UpdateTray() { if (_tray is not null) _tray.Text = $"Nexus · {_manager.Entries.Count(e => e.Status == ModuleStatus.Running)} modules active"; }
    private void ShowHub() { Show(); WindowState = WindowState.Normal; Activate(); }
    private void OnClosing(object? sender, CancelEventArgs e) { if (!_exiting) { e.Cancel = true; Hide(); } }
    public void Exit()
    {
        Dispose(); System.Windows.Application.Current.Shutdown();
    }
    public void Dispose()
    {
        if (_exiting) return; _exiting = true; _statsTimer.Stop(); _updateTimer.Stop(); _networkOperations.Cancel(); _networkOperations.Dispose(); _manager.Dispose();
        foreach (var binding in _bindings) binding.Dispose(); _hotkeys.Dispose(); _input.Dispose(); _tray.Dispose();
        try { Save(); } catch (Exception exception) { _diagnostics.Log("shutdown-save", exception); }
        Close(); GC.SuppressFinalize(this);
    }
    public void SaveScreenshot(string path)
    {
        var surface = (FrameworkElement)Content;
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(surface.ActualWidth), (int)Math.Ceiling(surface.ActualHeight), 96, 96, PixelFormats.Pbgra32); bitmap.Render(surface);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(path); png.Save(stream);
    }
    public void SaveScreenGallery(string directory)
    {
        foreach (string section in new[] { "All modules", "Permissions", "Profiles", "Hotkeys", "Settings & updates" })
        {
            Navigate(section); UpdateLayout(); SaveScreenshot(Path.Combine(directory, "nexus-" + section.ToLowerInvariant().Replace(" & ", "-").Replace(' ', '-') + ".png"));
        }
        foreach (var entry in _manager.Entries.Where(e => e.Module.SupportsConfiguration)) { FeaturePage(entry); UpdateLayout(); }
        Navigate("Overview"); UpdateLayout(); SaveScreenshot(Path.Combine(directory, "nexus-dashboard.png"));
    }
}
