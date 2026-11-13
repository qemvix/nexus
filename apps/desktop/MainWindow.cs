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

public sealed class MainWindow : Window
{
    private readonly ConfigurationStore _store;
    private readonly AppConfig _config;
    private readonly Diagnostics _diagnostics;
    private readonly InputService _input = new();
    private readonly HotkeyManager _hotkeys = new();
    private readonly ModuleManager _manager;
    private readonly List<IDisposable> _bindings = [];
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
    private string _section = "Overview";
    private bool _exiting;

    public MainWindow(string directory, Diagnostics diagnostics)
    {
        _diagnostics = diagnostics; _store = new(directory); _config = _store.Load();
        Overlay.TargetFrameRate = _config.OverlayFps;
        Title = "Nexus — Desktop, refined."; Width = 1180; Height = 880; MinWidth = 920; MinHeight = 640; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Ui.Brush("#0D0F19"); Foreground = Ui.Brush("#ECEAF7"); FontFamily = new FontFamily("Segoe UI");
        Icon = BitmapFrame.Create(new Uri("pack://application:,,,/Assets/nexus.ico"));
        var context = new ModuleContext(_input, _hotkeys, diagnostics, () => _config.ReducedMotion || !SystemParameters.ClientAreaAnimation, capability => (_config.Grants & capability) == capability);
        _manager = new(_config, context, Save);
        foreach (var module in CursorCatalog.Create().Concat(DesktopCatalog.Create()).Concat(ProductivityCatalog.Create())) _manager.Register(module);
        _manager.Changed += UpdateTray;
        BuildShell(); BindHotkeys();
        using var iconStream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/nexus.ico")).Stream;
        _tray = new Forms.NotifyIcon { Icon = new System.Drawing.Icon(iconStream), Text = "Nexus · desktop enhancement ecosystem", Visible = true };
        var menu = new Forms.ContextMenuStrip(); menu.Items.Add("Open Nexus", null, (_, _) => ShowHub()); menu.Items.Add("Pause all modules", null, (_, _) => { _manager.PauseAll(); Navigate(_section); }); menu.Items.Add("Exit Nexus", null, (_, _) => Exit());
        _tray.ContextMenuStrip = menu; _tray.DoubleClick += (_, _) => ShowHub();
        _statsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) }; _statsTimer.Tick += (_, _) => { _runtime.Text = Diagnostics.Summary(); if (_overlayMetrics is not null) _overlayMetrics.Text = Overlay.PerformanceSummary; }; _statsTimer.Start();
        _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) }; _updateTimer.Tick += async (_, _) => await BackgroundUpdateCheck(); _updateTimer.Start();
        Dispatcher.BeginInvoke(async () => await BackgroundUpdateCheck());
        Navigate("Overview"); _notice.Text = _store.RecoveryMessage;
        Closing += OnClosing;
    }
    private void BuildShell()
    {
        var root = new Grid(); root.ColumnDefinitions.Add(new() { Width = new GridLength(222) }); root.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        var rail = new DockPanel { Background = Ui.Brush("#10121E"), Margin = new Thickness(0, 0, 1, 0) }; var brand = new StackPanel { Margin = new Thickness(26, 28, 20, 0) }; DockPanel.SetDock(brand, Dock.Top);
        brand.Children.Add(Ui.Text("◈  nexus", 30)); brand.Children.Add(Ui.Text("DESKTOP, REFINED.", 10, "#9691B0")); brand.Children.Add(new Border { Height = 28 }); rail.Children.Add(brand);
        var footer = new StackPanel { Margin = new Thickness(22) }; DockPanel.SetDock(footer, Dock.Bottom); footer.Children.Add(Ui.Text("NEXUS  /  0.2 PREVIEW", 10, "#9691B0")); footer.Children.Add(_runtime); rail.Children.Add(footer);
        var nav = new StackPanel { Margin = new Thickness(14, 0, 8, 0) };
        foreach (var name in new[] { "Overview", "All modules", "Cursor & mouse", "Desktop", "Productivity", "Profiles", "Hotkeys", "Permissions", "Settings & updates" })
        {
            var button = Ui.Button(name, () => Navigate(name)); button.HorizontalContentAlignment = HorizontalAlignment.Left; button.BorderThickness = new Thickness(0); button.Background = Brushes.Transparent; button.Padding = new Thickness(14, 12, 8, 12); nav.Children.Add(button);
        }
        rail.Children.Add(nav); root.Children.Add(rail);
        var body = new DockPanel { Margin = new Thickness(34, 26, 32, 20) }; Grid.SetColumn(body, 1);
        var top = new Grid { Margin = new Thickness(0, 0, 0, 22) }; DockPanel.SetDock(top, Dock.Top); top.ColumnDefinitions.Add(new()); top.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        top.Children.Add(Ui.Text("WORKSPACE  /  PERSONAL", 11, "#8F97B3")); var pause = Ui.Button("Pause all  ·  Ctrl+Alt+P", () => { _manager.PauseAll(); Navigate(_section); }); Grid.SetColumn(pause, 1); top.Children.Add(pause); body.Children.Add(top);
        DockPanel.SetDock(_notice, Dock.Bottom); body.Children.Add(_notice);
        body.Children.Add(new ScrollViewer { Content = _page, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }); root.Children.Add(body); Content = root;
    }
    private void Navigate(string section)
    {
        _section = section; _page.Children.Clear();
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
    private void Heading(string title, string subtitle) { _page.Children.Add(Ui.Text(title, 30)); _page.Children.Add(Ui.Text(subtitle, 14, "#A6ACC5")); _page.Children.Add(new Border { Height = 12 }); }
    private void Overview()
    {
        Heading("Make your desktop yours.", "Thirteen native utilities. One quiet place to bring them together.");
        var hero = new Grid(); hero.ColumnDefinitions.Add(new()); hero.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var intro = new StackPanel(); intro.Children.Add(Ui.Text("A LITTLE LESS FRICTION", 10, "#B8A8FF")); intro.Children.Add(Ui.Text("Find your flow.", 31)); intro.Children.Add(Ui.Text("Shape your cursor. Clear distractions.\nKeep useful controls within reach.", 14, "#C0BED6")); intro.Children.Add(Ui.Button("Explore your modules  →", () => Navigate("All modules"))); hero.Children.Add(intro);
        var art = new Grid { Width = 235, Height = 175, Margin = new Thickness(15, 0, 0, 0) }; Grid.SetColumn(art, 1);
        for (int i = 0; i < 4; i++) art.Children.Add(new System.Windows.Shapes.Ellipse { Width = 62 + i * 38, Height = 62 + i * 38, Stroke = Ui.Brush(i % 2 == 0 ? "#8D7CF0" : "#438A92"), StrokeThickness = i == 0 ? 8 : 1, Opacity = 1 - i * .18 });
        art.Children.Add(new TextBlock { Text = "◈", FontSize = 36, Foreground = Ui.Brush("#DDD6FF"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }); hero.Children.Add(art);
        var heroCard = Ui.Card(hero); heroCard.Background = new LinearGradientBrush(Color.FromRgb(38, 31, 67), Color.FromRgb(19, 35, 46), 20); heroCard.Padding = new Thickness(28); _page.Children.Add(heroCard);
        var stats = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3, Margin = new Thickness(0, 4, 0, 12) };
        foreach (var (value, label) in new[] { (_manager.Entries.Count(e => e.Status == ModuleStatus.Running).ToString(), "MODULES ACTIVE"), (ModuleCount.ToString(), "WAYS TO REFINE"), ("Local", "PRIVATE BY DEFAULT") })
        {
            var column = new StackPanel(); column.Children.Add(Ui.Text(value, 26, "#C1B4FF")); column.Children.Add(Ui.Text(label, 10, "#A6ACC5")); var card = Ui.Card(column); card.Margin = new Thickness(0, 0, 12, 0); stats.Children.Add(card);
        }
        _page.Children.Add(stats); _page.Children.Add(Ui.Text("START WITH SOMETHING SMALL", 11, "#9CA4BE"));
        var row = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
        foreach (string id in new[] { "cursor-aura", "focus-fade" }) { var card = ModuleCard(_manager.Entries.Single(e => e.Manifest.Id == id)); card.Margin = new Thickness(0, 0, 12, 12); row.Children.Add(card); }
        _page.Children.Add(row);
    }
    private void ModulesPage(string? category)
    {
        Heading(category ?? "Your module library", "Install what you need. Enable it when it helps. Everything starts disabled.");
        var grid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
        foreach (var entry in _manager.Entries.Where(e => category is null || e.Manifest.Category == category)) { var card = ModuleCard(entry); card.Margin = new Thickness(0, 0, 12, 12); grid.Children.Add(card); }
        _page.Children.Add(grid);
    }
    private Border ModuleCard(ModuleEntry entry)
    {
        var state = _manager.State(entry.Manifest.Id); var stack = new StackPanel();
        stack.Children.Add(Ui.Text(entry.Manifest.Category.ToUpperInvariant(), 10, "#9592AF")); stack.Children.Add(Ui.Text(entry.Manifest.Name, 22));
        stack.Children.Add(new TextBlock { Text = entry.Manifest.Description, TextWrapping = TextWrapping.Wrap, Foreground = Ui.Brush("#A6ACC5"), MinHeight = 58, FontSize = 13 });
        stack.Children.Add(Ui.Text("●  " + entry.Status, 11, entry.Status == ModuleStatus.Running ? "#72E3CA" : "#B0A4DC"));
        var actions = new WrapPanel(); actions.Children.Add(Ui.Button(state.Installed ? (state.Enabled ? "Disable" : "Enable") : "Install", () => Guard(() =>
        {
            if (!state.Installed) _manager.Install(entry.Manifest.Id, true); else _manager.SetEnabled(entry.Manifest.Id, !state.Enabled);
            Navigate(_section);
        })));
        actions.Children.Add(Ui.Button("Configure", () => FeaturePage(entry))); stack.Children.Add(actions); return Ui.Card(stack);
    }
    private void FeaturePage(ModuleEntry entry)
    {
        _page.Children.Clear(); var state = _manager.State(entry.Manifest.Id);
        Heading(entry.Manifest.Name, entry.Manifest.Description); _page.Children.Add(Ui.Text($"Status: {entry.Status}   ·   Capabilities: {entry.Manifest.Permissions}", 12, "#B5ACD8"));
        if (entry.Error.Length > 0) _page.Children.Add(Ui.Text(entry.Error, 13, "#FFC58C"));
        var options = state.Options; var editor = new StackPanel();
        foreach (var field in entry.Module.Schema) editor.Children.Add(OptionEditor(field, options));
        if (entry.Module.CreatePanel(options) is { } extra) editor.Children.Add(extra);
        if (editor.Children.Count > 0) _page.Children.Add(Ui.Card(editor));
        var buttons = new WrapPanel();
        buttons.Children.Add(Ui.Button("Apply changes", () => Guard(() => { options.Validate(); _manager.Refresh(entry.Manifest.Id); Save(); FeaturePage(entry); _notice.Text = "Settings saved."; })));
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
        Heading("Profiles", "Save a workspace and switch it in one step. Exports contain settings, never clipboard history or grants.");
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
        Heading("You are in control.", "These grants control Nexus features. They are not an operating-system sandbox for third-party code.");
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
        Heading("Quiet by design.", "Local configuration, deliberate network access, and no telemetry service.");
        var motion = new CheckBox { Content = "Reduce motion (Windows accessibility preference is also respected)", IsChecked = _config.ReducedMotion }; motion.Click += (_, _) => { _config.ReducedMotion = motion.IsChecked == true; Save(); }; _page.Children.Add(Ui.Card(motion));
        _page.Children.Add(Ui.Text("Overlay rendering", 22));
        var rate = new ComboBox { ItemsSource = new[] { 60, 90, 120, 144, 165, 240 }, SelectedItem = _config.OverlayFps, Margin = new Thickness(0, 4, 0, 12) };
        System.Windows.Automation.AutomationProperties.SetName(rate, "Overlay target frames per second");
        rate.SelectionChanged += (_, _) => { if (rate.SelectedItem is int fps) { _config.OverlayFps = fps; Overlay.TargetFrameRate = fps; Save(); } }; _page.Children.Add(rate);
        _overlayMetrics = Ui.Text(Overlay.PerformanceSummary, 13, "#A6ACC5"); _page.Children.Add(_overlayMetrics);
        _page.Children.Add(Ui.Text("60 FPS by default; higher targets follow the display's composition cadence. Measured rendering can be lower under load or on a slower display.", 12, "#A6ACC5"));
        _page.Children.Add(Ui.Text("Nexus 0.2.0 · native Windows / .NET 10\n" + Diagnostics.Summary() + "\nLast local error: " + _diagnostics.LastError, 13, "#A6ACC5"));
        _page.Children.Add(Ui.Button("Open local settings and diagnostics", () => Ui.Launch(_store.DirectoryPath)));
        _page.Children.Add(Ui.Text("Signed updates", 22)); _page.Children.Add(Ui.Text("Publisher setup required: an HTTPS signed manifest and trusted RSA public key. No update server is configured in this preview.", 13, "#A6ACC5"));
        var automatic = new CheckBox { Content = "Check automatically once per day (download and installation remain your choice)", IsChecked = _config.AutoCheckUpdates };
        automatic.Click += (_, _) => { _config.AutoCheckUpdates = automatic.IsChecked == true; Save(); }; _page.Children.Add(automatic);
        var url = new TextBox { Text = _config.UpdateUrl, Margin = new Thickness(0, 8, 0, 8) }; System.Windows.Automation.AutomationProperties.SetName(url, "Update manifest HTTPS URL"); _page.Children.Add(url);
        var key = new TextBox { Text = _config.UpdatePublicKey, AcceptsReturn = true, MinHeight = 80, MaxHeight = 160, TextWrapping = TextWrapping.Wrap }; System.Windows.Automation.AutomationProperties.SetName(key, "Trusted RSA public key PEM"); _page.Children.Add(key);
        var check = Ui.Button("Save and check for updates", () => { });
        check.Click += async (_, _) =>
        {
            check.IsEnabled = false;
            try
            {
                if ((_config.Grants & Capability.Network) == 0) throw new InvalidOperationException("Grant network access first.");
                _config.UpdateUrl = url.Text.Trim(); _config.UpdatePublicKey = key.Text; Save();
                var release = await UpdateService.Check(_config.UpdateUrl, _config.UpdatePublicKey, _networkOperations.Token);
                _config.LastUpdateCheck = DateTimeOffset.UtcNow; Save();
                if (Version.Parse(release.Version) <= new Version(0, 2, 0)) { _notice.Text = "You have the latest release."; return; }
                if (MessageBox.Show($"Download verified Nexus {release.Version} and open its installer?", "Nexus update", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
                string path = await UpdateService.Download(release, Path.Combine(_store.DirectoryPath, "updates"), _networkOperations.Token);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path, "--install") { UseShellExecute = true }); Exit();
            }
            catch (Exception e) { _notice.Text = e.Message; _diagnostics.Log("updates", e); }
            finally { check.IsEnabled = true; }
        };
        _page.Children.Add(check); _page.Children.Add(Ui.Button("Exit Nexus", Exit));
    }
    private void Save() => _store.Save(_config);
    private async Task BackgroundUpdateCheck()
    {
        if (_exiting || _updateChecking || !_config.AutoCheckUpdates || (_config.Grants & Capability.Network) == 0 || string.IsNullOrWhiteSpace(_config.UpdateUrl) || string.IsNullOrWhiteSpace(_config.UpdatePublicKey)) return;
        var elapsed = DateTimeOffset.UtcNow - _config.LastUpdateCheck;
        if (elapsed >= TimeSpan.Zero && elapsed < TimeSpan.FromDays(1)) return;
        _updateChecking = true;
        try
        {
            // Record attempts too, so a broken/offline endpoint is never polled every minute.
            _config.LastUpdateCheck = DateTimeOffset.UtcNow; Save();
            var release = await UpdateService.Check(_config.UpdateUrl, _config.UpdatePublicKey, _networkOperations.Token);
            if (Version.Parse(release.Version) > new Version(0, 2, 0)) _notice.Text = $"Nexus {release.Version} is available. Open Settings & updates to review it.";
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { _diagnostics.Log("update-check", e); }
        finally { _updateChecking = false; }
    }
    private void Guard(Action action) { try { action(); } catch (Exception e) { _notice.Text = e.Message; _diagnostics.Log("action", e); } }
    private void UpdateTray() { if (_tray is not null) _tray.Text = $"Nexus · {_manager.Entries.Count(e => e.Status == ModuleStatus.Running)} modules active"; }
    private void ShowHub() { Show(); WindowState = WindowState.Normal; Activate(); }
    private void OnClosing(object? sender, CancelEventArgs e) { if (!_exiting) { e.Cancel = true; Hide(); } }
    public void Exit()
    {
        if (_exiting) return; _exiting = true; _statsTimer.Stop(); _updateTimer.Stop(); _networkOperations.Cancel(); _networkOperations.Dispose(); _manager.Dispose();
        foreach (var binding in _bindings) binding.Dispose(); _hotkeys.Dispose(); _input.Dispose(); _tray.Dispose();
        Save(); Close(); System.Windows.Application.Current.Shutdown();
    }
    public void SaveScreenshot(string path)
    {
        var bitmap = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(this);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(path); png.Save(stream);
    }
    public void SaveScreenGallery(string directory)
    {
        foreach (string section in new[] { "All modules", "Permissions", "Profiles", "Hotkeys", "Settings & updates" })
        {
            Navigate(section); UpdateLayout(); SaveScreenshot(Path.Combine(directory, "nexus-" + section.ToLowerInvariant().Replace(" & ", "-").Replace(' ', '-') + ".png"));
        }
        foreach (var entry in _manager.Entries) { FeaturePage(entry); UpdateLayout(); }
        Navigate("Overview"); UpdateLayout(); SaveScreenshot(Path.Combine(directory, "nexus-dashboard.png"));
    }
}
