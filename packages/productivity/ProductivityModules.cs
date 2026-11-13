using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Nexus.Core;

namespace Nexus.Productivity;

public static class ProductivityCatalog
{
    public static INexusModule[] Create() => [new ScreenRuler(), new KeyCast(), new Glassify(), new QuickDock()];
}
public sealed class ScreenRuler() : NexusModule(new("screen-ruler", "ScreenRuler", "Productivity", "Measure pixels, inspect coordinates and copy a color from the screen.", Capability.Screen),
    new("Mode", "Overlay", Choices: ["Crosshair", "Ruler", "Measure"]), new("Accent", "Guide color", IsText: true))
{
    private Native.Point? _anchor;
    private string _color = "#------";
    protected override void OnStart()
    {
        Resources.Add(Context.Hotkeys.Register("Ctrl+Alt+R", () => { Native.GetCursorPos(out var p); _anchor = _anchor is null ? p : null; }));
        Resources.Add(Context.Hotkeys.Register("Ctrl+Alt+C", Pick));
        Resources.Add(new Overlay(Draw, "ScreenRuler")); Resources.Add(new ActionDisposable(() => _anchor = null));
    }
    private void Pick()
    {
        try
        {
            Native.GetCursorPos(out var p);
            using var bitmap = new System.Drawing.Bitmap(1, 1);
            using (var graphics = System.Drawing.Graphics.FromImage(bitmap)) graphics.CopyFromScreen(p.X, p.Y, 0, 0, bitmap.Size);
            var color = bitmap.GetPixel(0, 0); _color = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            System.Windows.Clipboard.SetText(_color);
        }
        catch (Exception e) { Context.Diagnostics.Log("color-picker", e); }
    }
    private void Draw(OverlayFrame frame)
    {
        Native.GetCursorPos(out var cursor); var p = frame.Point(cursor.X, cursor.Y); var pen = new Pen(OverlayFrame.Brush(Options.Accent, .75), 1);
        frame.Drawing.DrawLine(pen, new(p.X, 0), new(p.X, frame.Bounds.Height));
        frame.Drawing.DrawLine(pen, new(0, p.Y), new(frame.Bounds.Width, p.Y));
        if (Options.Mode == "Ruler")
        {
            for (int tick = 0; tick < frame.Bounds.Width; tick += 10)
            {
                double length = tick % 100 == 0 ? 18 : 7;
                frame.Drawing.DrawLine(pen, new(tick, p.Y), new(tick, p.Y + length));
                if (tick % 100 == 0) frame.Text($"{tick * frame.Scale:F0}", new(tick + 3, p.Y + 20), 11);
            }
        }
        string text = $"{cursor.X}, {cursor.Y} px   {_color}\nCtrl+Alt+R: anchor · Ctrl+Alt+C: copy color";
        if (_anchor is { } anchor)
        {
            var a = frame.Point(anchor.X, anchor.Y); var rectangle = new Rect(a, p);
            frame.Drawing.DrawRectangle(OverlayFrame.Brush(Options.Accent, .1), pen, rectangle);
            text = $"{Math.Abs(cursor.X - anchor.X)} × {Math.Abs(cursor.Y - anchor.Y)} px\n" + text;
        }
        if (frame.Bounds.Contains(p))
        {
            double x = Math.Clamp(p.X + 20, 0, Math.Max(0, frame.Bounds.Width - 330));
            double y = Math.Clamp(p.Y + 24, 0, Math.Max(0, frame.Bounds.Height - 90));
            frame.Drawing.DrawRoundedRectangle(OverlayFrame.Brush("#121525", .96), null, new(x, y, 330, 82), 10, 10); frame.Text(text, new(x + 12, y + 10), 12);
        }
    }
}
public sealed class KeyCast() : NexusModule(new("key-cast", "KeyCast", "Productivity", "Display shortcuts and mouse clicks while presenting. Never saves keystrokes.", Capability.Keyboard | Capability.Mouse),
    new("Mode", "Key visibility", Choices: ["Shortcuts only", "All keys (visible to recordings)"]), new("Size", "Text size", 16, 48), new("Duration", "Display seconds", .3, 3), new("Accent", "Text color", IsText: true))
{
    private string _label = "KeyCast ready";
    private long _time;
    protected override void OnStart()
    {
        Resources.Add(Context.Input.Keyboard(e =>
        {
            if (!e.Down) return;
            bool ctrl = (Native.GetAsyncKeyState(0x11) & 0x8000) != 0, alt = (Native.GetAsyncKeyState(0x12) & 0x8000) != 0;
            bool shift = (Native.GetAsyncKeyState(0x10) & 0x8000) != 0, win = (Native.GetAsyncKeyState(0x5B) & 0x8000) != 0;
            if (Options.Mode != "All keys (visible to recordings)" && !ctrl && !alt && !win && e.Key is >= 0x30 and <= 0x5A) return;
            _label = (ctrl ? "Ctrl + " : "") + (alt ? "Alt + " : "") + (shift ? "Shift + " : "") + (win ? "Win + " : "") + KeyInterop.KeyFromVirtualKey(e.Key);
            _time = Stopwatch.GetTimestamp();
        }));
        Resources.Add(Context.Input.Mouse(e => { if (e.Message is 0x201 or 0x204 or 0x207) { _label = e.Message == 0x201 ? "Left click" : e.Message == 0x204 ? "Right click" : "Middle click"; _time = Stopwatch.GetTimestamp(); } }));
        Resources.Add(new Overlay(frame =>
        {
            if (Stopwatch.GetElapsedTime(_time).TotalSeconds > Options.Duration) return;
            var rect = new Rect(40, frame.Bounds.Height - 110, 500, 65);
            frame.Drawing.DrawRoundedRectangle(OverlayFrame.Brush("#121525", .93), new Pen(OverlayFrame.Brush("#3D415A"), 1), rect, 14, 14);
            frame.Text(_label, new(rect.X + 20, rect.Y + 12), Options.Size, Options.Accent);
        }, "KeyCast"));
        Resources.Add(new ActionDisposable(() => { _label = ""; _time = 0; }));
    }
}
public sealed class Glassify() : NexusModule(new("glassify", "Glassify", "Productivity", "Set active or selected apps' opacity and topmost state; restore on disable.", Capability.Windows),
    new("Intensity", "Window opacity", .2, 1), new("AlwaysOnTop", "Always on top", IsToggle: true), new("Mode", "Target", Choices: ["Active window", "Process list"]), new("Location", "Process names, separated by commas", IsText: true))
{
    private readonly Dictionary<nint, WindowStyleLease> _originals = [];
    private readonly HashSet<nint> _unsupported = [];
    protected override void OnStart()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        timer.Tick += (_, _) =>
        {
            var window = Native.ExternalForeground(); if (window == 0 || _originals.ContainsKey(window) || _unsupported.Contains(window)) return;
            if ((Native.GetWindowLongPtr(window, -16).ToInt64() & 0x00C00000) == 0) return;
            if (Options.Mode == "Process list" && !Options.Location.Split(',', StringSplitOptions.TrimEntries).Contains(Native.ForegroundProcess(), StringComparer.OrdinalIgnoreCase)) return;
            try { _originals[window] = new WindowStyleLease(window, (byte)(Options.Intensity * 255), Options.AlwaysOnTop); }
            catch (Exception e) { _unsupported.Add(window); Context.Diagnostics.Log("glassify", e); }
        };
        timer.Start(); Resources.Add(new ActionDisposable(() => { timer.Stop(); Restore(); }));
    }
    private void Restore()
    {
        foreach (var lease in _originals.Values) lease.Dispose();
        _originals.Clear(); _unsupported.Clear();
    }
}
public sealed class QuickDock() : NexusModule(new("quick-dock", "QuickDock", "Productivity", "A floating dock for media, volume and Windows device controls.", Capability.None),
    new Option("AlwaysOnTop", "Keep dock above windows", IsToggle: true))
{
    private Window? _window;
    protected override void OnStart()
    {
        Resources.Add(Context.Hotkeys.Register("Ctrl+Alt+Q", Show)); Show(); Resources.Add(new ActionDisposable(() => { _window?.Close(); _window = null; }));
    }
    private void Show()
    {
        if (_window is not null) { _window.Activate(); return; }
        var panel = new StackPanel(); panel.Children.Add(Ui.Text("QUICKDOCK", 21)); panel.Children.Add(Ui.Text("Your desktop, within reach.", 12, "#A6ACC5"));
        var controls = new WrapPanel();
        controls.Children.Add(Ui.Button("Volume −", () => Ui.MediaKey(0xAE))); controls.Children.Add(Ui.Button("Mute", () => Ui.MediaKey(0xAD))); controls.Children.Add(Ui.Button("Volume +", () => Ui.MediaKey(0xAF)));
        controls.Children.Add(Ui.Button("Play / pause", () => Ui.MediaKey(0xB3)));
        foreach (var (name, uri) in new[] { ("Sound mixer", "ms-settings:apps-volume"), ("Brightness / displays", "ms-settings:display"), ("Wi-Fi", "ms-settings:network-wifi"), ("Bluetooth", "ms-settings:bluetooth"), ("Accessibility", "ms-settings:easeofaccess"), ("Power", "ms-settings:powersleep") })
            controls.Children.Add(Ui.Button(name, () => Ui.Launch(uri)));
        panel.Children.Add(controls); _window = Ui.Floating("QuickDock", panel, 420, 360); _window.Topmost = Options.AlwaysOnTop;
        _window.Closed += (_, _) => _window = null;
    }
}
