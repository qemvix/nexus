using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Nexus.Core;

namespace Nexus.Desktop;

public static class DesktopCatalog
{
    public static INexusModule[] Create() => [new FloatWidget(), new WindowGlide(), new ClipVault(), new FocusFade()];
}
public sealed class FloatWidget() : NexusModule(new("float", "Float", "Desktop", "A compact live clock, CPU, memory and media widget with optional weather.", Capability.None),
    new("Location", "Weather latitude,longitude", IsText: true), new("AlwaysOnTop", "Keep above windows", IsToggle: true))
{
    private long _idle, _kernel, _user;
    private CancellationTokenSource? _weatherCancellation;
    protected override void OnStart()
    {
        var content = new StackPanel(); var time = Ui.Text("", 32); var stats = Ui.Text("Reading system…", 13, "#A6ACC5");
        var weather = Ui.Text("Weather needs network permission.", 12, "#A6ACC5");
        content.Children.Add(Ui.Text("FLOAT  /  LIVE", 11, "#A497FF")); content.Children.Add(time); content.Children.Add(stats);
        var buttons = new WrapPanel(); buttons.Children.Add(Ui.Button("Previous", () => Ui.MediaKey(0xB1))); buttons.Children.Add(Ui.Button("Play / pause", () => Ui.MediaKey(0xB3))); buttons.Children.Add(Ui.Button("Next", () => Ui.MediaKey(0xB0)));
        content.Children.Add(buttons); content.Children.Add(weather);
        var window = Ui.Floating("Float", content, 350, 280); window.Topmost = Options.AlwaysOnTop;
        Resources.Add(new ActionDisposable(window.Close));
        Native.GetSystemTimes(out _idle, out _kernel, out _user);
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) =>
        {
            time.Text = DateTime.Now.ToString("HH:mm:ss");
            Native.GetSystemTimes(out long idle, out long kernel, out long user); double total = kernel - _kernel + user - _user;
            double cpu = total > 0 ? (1 - (idle - _idle) / total) * 100 : 0; _idle = idle; _kernel = kernel; _user = user;
            var memory = new Native.MemoryStatus { Length = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Native.MemoryStatus>() }; Native.GlobalMemoryStatusEx(ref memory);
            stats.Text = $"CPU {Math.Clamp(cpu, 0, 100):F0}%   ·   RAM {memory.Load}%\n{Environment.OSVersion.VersionString} · {Environment.ProcessorCount} logical cores";
        };
        timer.Start(); Resources.Add(new ActionDisposable(timer.Stop));
        if (Context.Granted?.Invoke(Capability.Network) == true)
        {
            _weatherCancellation = new(); var token = _weatherCancellation.Token;
            _ = LoadWeather(weather, token);
            Resources.Add(new ActionDisposable(() => { _weatherCancellation?.Cancel(); _weatherCancellation?.Dispose(); _weatherCancellation = null; }));
        }
    }
    private async Task LoadWeather(TextBlock label, CancellationToken token)
    {
        try
        {
            var pieces = Options.Location.Split(',');
            if (pieces.Length != 2 || !double.TryParse(pieces[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double latitude) || !double.TryParse(pieces[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double longitude) || latitude is < -90 or > 90 || longitude is < -180 or > 180) throw new FormatException("Use latitude,longitude.");
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            string url = FormattableString.Invariant($"https://api.open-meteo.com/v1/forecast?latitude={latitude}&longitude={longitude}&current=temperature_2m");
            using var data = JsonDocument.Parse(await client.GetStringAsync(url, token));
            if (!token.IsCancellationRequested) label.Text = $"{data.RootElement.GetProperty("current").GetProperty("temperature_2m").GetDouble():F0} °C · Open-Meteo · updated {DateTime.Now:HH:mm}";
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { if (!token.IsCancellationRequested) label.Text = "Weather unavailable: " + e.Message; }
    }
}
public sealed class WindowGlide() : NexusModule(new("window-glide", "WindowGlide", "Desktop", "Snap windows with shortcuts, layouts and magnetic screen edges.", Capability.Windows),
    new("Mode", "Layout", Choices: ["Halves", "Quarters", "Thirds", "Custom"]), new("Size", "Magnetic edge distance (pixels)", 16, 100), new("Duration", "Animation seconds", .1, 1))
{
    private Native.WinEventCallback? _callback;
    private DispatcherTimer? _animation;
    protected override void OnStart()
    {
        Resources.Add(Context.Hotkeys.Register("Ctrl+Alt+Left", () => Snap(Native.ExternalForeground(), 0)));
        Resources.Add(Context.Hotkeys.Register("Ctrl+Alt+Right", () => Snap(Native.ExternalForeground(), 1)));
        Resources.Add(Context.Hotkeys.Register("Ctrl+Alt+Up", () => Snap(Native.ExternalForeground(), 2)));
        Resources.Add(Context.Hotkeys.Register("Ctrl+Alt+Down", () => Snap(Native.ExternalForeground(), 3)));
        _callback = (_, _, window, _, _, _, _) => System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            if (_callback is null || window == 0 || window != Native.ExternalForeground() || !Native.GetWindowRect(window, out var rect)) return;
            var area = System.Windows.Forms.Screen.FromHandle(window).WorkingArea;
            if (Math.Abs(rect.Left - area.Left) < Options.Size) Snap(window, 0);
            else if (Math.Abs(rect.Right - area.Right) < Options.Size) Snap(window, 1);
        });
        var hook = Native.SetWinEventHook(0xB, 0xB, 0, _callback, 0, 0, 2);
        if (hook == 0) throw new InvalidOperationException("Windows move notification hook unavailable.");
        Resources.Add(new ActionDisposable(() => { Native.UnhookWinEvent(hook); _callback = null; _animation?.Stop(); }));
    }
    private void Snap(nint window, int direction)
    {
        if (window == 0 || !Native.GetWindowRect(window, out var old)) return;
        if ((Native.GetWindowLongPtr(window, -16).ToInt64() & 0x00C00000) == 0) return;
        var area = System.Windows.Forms.Screen.FromHandle(window).WorkingArea;
        int columns = Options.Mode == "Thirds" ? 3 : 2; int width = area.Width / columns, height = area.Height;
        int x = direction == 1 ? area.Right - width : area.Left, y = area.Top;
        if (Options.Mode == "Quarters") { height /= 2; if (direction == 3) y += height; }
        else if (direction == 2) { x = area.Left; width = area.Width; }
        else if (direction == 3) { x = area.Left + (area.Width - width) / 2; }
        if (Options.Mode == "Custom" && Options.Zones.Count > 0)
        {
            var zone = Options.Zones[direction % Options.Zones.Count]; x = area.Left + (int)(zone.X * area.Width); y = area.Top + (int)(zone.Y * area.Height);
            width = (int)(zone.Width * area.Width); height = (int)(zone.Height * area.Height);
        }
        Native.ShowWindow(window, 9); _animation?.Stop();
        if (Context.ReducedMotion()) { Native.SetWindowPos(window, 0, x, y, width, height, 0x14); return; }
        var started = System.Diagnostics.Stopwatch.StartNew();
        _animation = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _animation.Tick += (_, _) =>
        {
            double t = Math.Clamp(started.Elapsed.TotalSeconds / Options.Duration, 0, 1), ease = 1 - Math.Pow(1 - t, 3);
            Native.SetWindowPos(window, 0, (int)(old.Left + (x - old.Left) * ease), (int)(old.Top + (y - old.Top) * ease), (int)(old.Width + (width - old.Width) * ease), (int)(old.Height + (height - old.Height) * ease), 0x14);
            if (t >= 1 || !Native.IsWindow(window)) _animation.Stop();
        };
        _animation.Start();
    }
    public override FrameworkElement? CreatePanel(ModuleOptions options) => Ui.JsonEditor("Custom zones: normalized X, Y, Width, Height (left / right / up / down)", options.Zones, (List<WindowZone> zones) => { options.Zones = zones; options.Validate(); });
}
public sealed class FocusFade() : NexusModule(new("focus-fade", "FocusFade", "Desktop", "Dim distractions around your active window with a click-through overlay.", Capability.Windows),
    new("Intensity", "Background dimming", .05, .9), new("Size", "Highlight width", 16, 60), new("Accent", "Highlight color", IsText: true))
{
    protected override void OnStart() => Resources.Add(new Overlay(frame =>
    {
        var window = Native.ExternalForeground(); if (window == 0 || !Native.GetWindowRect(window, out var rect) || rect.Width < 10) return;
        var active = frame.Rect(rect);
        var geometry = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(frame.Bounds), new RectangleGeometry(active, 8, 8));
        frame.Drawing.DrawGeometry(OverlayFrame.Brush("#000000", Options.Intensity), null, geometry);
        frame.Drawing.DrawRoundedRectangle(null, new Pen(OverlayFrame.Brush(Options.Accent, .8), Options.Size / 16), active, 8, 8);
    }, "FocusFade"));
}
