using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows.Threading;
using Nexus.Core;

namespace Nexus.Cursor;

public static class CursorCatalog
{
    public static INexusModule[] Create() => [new CursorAura(), new ClickFx(), new ScrollFlow(), new CursorLens(), new CursorBlur()];
}
public sealed class CursorAura() : NexusModule(new("cursor-aura", "Cursor Aura", "Cursor & mouse", "Glow, trails, particles and a spotlight around the pointer.", Capability.Mouse),
    new("Mode", "Effect", Choices: ["Glow", "Blur", "Trail", "Particles", "Spotlight"]), new("Size", "Radius", 16, 180), new("Intensity", "Opacity", .05, 1), new("Accent", "Accent color", IsText: true))
{
    private readonly Queue<(int X, int Y)> _trail = [];
    protected override void OnStart()
    {
        Resources.Add(Context.Input.Mouse(e => { if (e.Message != 0x200) return; _trail.Enqueue((e.X, e.Y)); while (_trail.Count > 22) _trail.Dequeue(); }));
        Resources.Add(new Overlay(Draw, "Cursor Aura"));
        Resources.Add(new ActionDisposable(_trail.Clear));
    }
    private void Draw(OverlayFrame frame)
    {
        Native.GetCursorPos(out var cursor); var p = frame.Point(cursor.X, cursor.Y); var d = frame.Drawing;
        double radius = Options.Size;
        if (Options.Mode == "Spotlight")
        {
            var outside = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(frame.Bounds), new EllipseGeometry(p, radius * 2, radius * 2));
            d.DrawGeometry(OverlayFrame.Brush("#000000", Options.Intensity), null, outside); return;
        }
        if (Options.Mode is "Trail" or "Particles" && !Context.ReducedMotion())
        {
            var points = _trail.ToArray();
            for (int i = 0; i < points.Length; i++)
            {
                var point = frame.Point(points[i].X, points[i].Y);
                if (Options.Mode == "Particles") point.Offset(Math.Sin(i * 2.4) * radius / 2, Math.Cos(i * 1.7) * radius / 2);
                double r = Math.Max(2, (double)(i + 1) / points.Length * radius / 5);
                d.DrawEllipse(OverlayFrame.Brush(Options.Accent, Options.Intensity * (i + 1) / points.Length), null, point, r, r);
            }
        }
        else
        {
            var gradient = new RadialGradientBrush();
            var color = (Color)ColorConverter.ConvertFromString(Options.Accent);
            gradient.GradientStops.Add(new(Color.FromArgb((byte)(Options.Intensity * 200), color.R, color.G, color.B), 0));
            gradient.GradientStops.Add(new(Colors.Transparent, 1));
            d.DrawEllipse(gradient, null, p, radius, radius);
            if (Options.Mode != "Blur") d.DrawEllipse(null, new Pen(OverlayFrame.Brush(Options.Accent, .65), 1), p, radius / 3, radius / 3);
        }
    }
}
public sealed class ClickFx() : NexusModule(new("click-fx", "ClickFX", "Cursor & mouse", "Visible ripples, rings and shockwaves for mouse clicks.", Capability.Mouse),
    new("Mode", "Animation", Choices: ["Ripple", "Rings", "Shockwave"]), new("Size", "Maximum radius", 16, 180), new("Duration", "Duration (seconds)", .1, 2), new("Sound", "Play system click sound", IsToggle: true), new("Accent", "Left click color", IsText: true))
{
    private readonly List<(int X, int Y, bool Right, long Time)> _clicks = [];
    protected override void OnStart()
    {
        Resources.Add(Context.Input.Mouse(e =>
        {
            if (e.Message is not (0x201 or 0x204)) return;
            _clicks.Add((e.X, e.Y, e.Message == 0x204, Stopwatch.GetTimestamp()));
            if (_clicks.Count > 30) _clicks.RemoveAt(0);
            if (Options.Sound) System.Media.SystemSounds.Asterisk.Play();
        }));
        Resources.Add(new Overlay(Draw, "ClickFX")); Resources.Add(new ActionDisposable(_clicks.Clear));
    }
    private void Draw(OverlayFrame frame)
    {
        _clicks.RemoveAll(c => Stopwatch.GetElapsedTime(c.Time).TotalSeconds > Options.Duration);
        foreach (var click in _clicks)
        {
            double t = Stopwatch.GetElapsedTime(click.Time).TotalSeconds / Options.Duration;
            double radius = Context.ReducedMotion() ? Options.Size / 2 : 5 + Options.Size * (1 - Math.Pow(1 - t, 3));
            string color = click.Right ? "#67E8DF" : Options.Accent; var point = frame.Point(click.X, click.Y);
            var pen = new Pen(OverlayFrame.Brush(color, 1 - t), Options.Mode == "Shockwave" ? 6 * (1 - t) + 1 : 2);
            frame.Drawing.DrawEllipse(Options.Mode == "Ripple" ? OverlayFrame.Brush(color, .18 * (1 - t)) : null, pen, point, radius, radius);
            if (Options.Mode == "Rings") frame.Drawing.DrawEllipse(null, pen, point, radius * .65, radius * .65);
        }
    }
}
/// <summary>Hosts the user's Glide engine with Nexus-owned settings and lifecycle. No second smoothing layer is added.</summary>
public sealed class ScrollFlow() : NexusModule(new("scroll-flow", "ScrollFlow", "Cursor & mouse", "Glide-powered touchpad-style scrolling, app exclusions and fullscreen pausing.", Capability.Mouse),
    new("Sensitivity", "Scroll speed (1 = 100%)", .25, 2.5), new("GlideMilliseconds", "Glide duration (milliseconds)", 100, 700),
    new("NativeTouchpad", "Use native touchpad gestures when available", IsToggle: true), new("PauseFullscreen", "Pause in fullscreen apps", IsToggle: true),
    new("ExcludedApps", "Excluded processes (comma-separated, .exe optional)", IsText: true), new("Invert", "Invert scrolling", IsToggle: true))
{
    private GlideBackend.ScrollEngine? _engine;
    protected override void OnStart()
    {
        var standalone = Process.GetProcessesByName("Glide");
        bool running = standalone.Any(); foreach (var process in standalone) process.Dispose();
        if (running) throw new InvalidOperationException("Exit standalone Glide before enabling ScrollFlow, so the two engines do not transform the same wheel input.");
        var settings = new GlideBackend.Settings
        {
            Enabled = true,
            NativeTouchpad = Options.NativeTouchpad,
            PauseFullscreen = Options.PauseFullscreen,
            SpeedPercent = (int)Math.Round(Options.Sensitivity * 100),
            GlideMilliseconds = (int)Math.Round(Options.GlideMilliseconds),
            Invert = Options.Invert,
            ExcludedApps = Options.ExcludedApps,
            Profiles = Options.ScrollProfiles.ToArray()
        };
        settings.Validate(); _engine = new GlideBackend.ScrollEngine(settings, false); Resources.Add(_engine);
        Resources.Add(new ActionDisposable(() => _engine = null));
    }
    public override FrameworkElement? CreatePanel(ModuleOptions options)
    {
        var panel = new System.Windows.Controls.StackPanel();
        panel.Children.Add(Ui.Text("Powered by your Glide engine. Run only one of Glide or ScrollFlow at a time.", 13, "#B8A8FF"));
        var status = Ui.Text(_engine?.Status ?? "Enable ScrollFlow to start Glide.", 13, "#72E3CA"); panel.Children.Add(status);
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => status.Text = _engine is null ? "ScrollFlow is disabled." : _engine.Status + "\n" + _engine.OutputMode + (_engine.Error is null ? "" : "\n" + _engine.Error);
        panel.Loaded += (_, _) => timer.Start(); panel.Unloaded += (_, _) => timer.Stop();
        panel.Children.Add(Ui.JsonEditor("Per-app profiles: Process, Sensitivity, Invert, optional GlideMilliseconds", options.ScrollProfiles, (List<ScrollProfile> profiles) => { options.ScrollProfiles = profiles; options.Validate(); }));
        return panel;
    }
}
public sealed class CursorLens() : NexusModule(new("cursor-lens", "Cursor Lens", "Cursor & mouse", "A mouse-following magnifier with circular or square framing.", Capability.Screen),
    new("Size", "Lens size", 80, 320), new("Zoom", "Magnification", 1.1, 8), new("Shape", "Shape", Choices: ["Circle", "Square"]), new("AlwaysOnTop", "Presentation crosshair", IsToggle: true))
{
    private WriteableBitmap? _image;
    private System.Drawing.Bitmap? _captureSurface;
    private long _captureFrame = -1;
    protected override void OnStart()
    {
        Resources.Add(new Overlay(Draw, "Cursor Lens")); Resources.Add(new ActionDisposable(() => { _captureSurface?.Dispose(); _captureSurface = null; _image = null; _captureFrame = -1; }));
    }
    private void Draw(OverlayFrame frame)
    {
        Native.GetCursorPos(out var point);
        var p = frame.Point(point.X, point.Y);
        if (!frame.Bounds.Contains(p)) return;
        if (_captureFrame != frame.FrameId)
        {
            _captureFrame = frame.FrameId;
            int sourceSize = Math.Max(12, (int)(Options.Size / Options.Zoom));
            var monitor = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(point.X, point.Y)).Bounds;
            int left = Math.Clamp(point.X - sourceSize / 2, monitor.Left, Math.Max(monitor.Left, monitor.Right - sourceSize));
            int top = Math.Clamp(point.Y - sourceSize / 2, monitor.Top, Math.Max(monitor.Top, monitor.Bottom - sourceSize));
            if (_captureSurface is null || _captureSurface.Width != sourceSize)
            {
                _captureSurface?.Dispose(); _captureSurface = new System.Drawing.Bitmap(sourceSize, sourceSize, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
                _image = new WriteableBitmap(sourceSize, sourceSize, 96, 96, PixelFormats.Bgr32, null);
            }
            using (var graphics = System.Drawing.Graphics.FromImage(_captureSurface)) graphics.CopyFromScreen(left, top, 0, 0, _captureSurface.Size);
            var data = _captureSurface.LockBits(new System.Drawing.Rectangle(0, 0, sourceSize, sourceSize), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
            try { _image!.WritePixels(new Int32Rect(0, 0, sourceSize, sourceSize), data.Scan0, data.Stride * sourceSize, data.Stride); }
            finally { _captureSurface.UnlockBits(data); }
        }
        double size = Options.Size;
        double x = Math.Clamp(p.X + 30, 4, Math.Max(4, frame.Bounds.Width - size - 4));
        double y = Math.Clamp(p.Y + 30, 4, Math.Max(4, frame.Bounds.Height - size - 4));
        if (!frame.Bounds.Contains(p)) return;
        var rect = new Rect(x, y, size, size);
        Geometry clip = Options.Shape == "Square" ? new RectangleGeometry(rect, 14, 14) : new EllipseGeometry(rect);
        frame.Drawing.PushClip(clip); frame.Drawing.DrawImage(_image, rect); frame.Drawing.Pop();
        frame.Drawing.DrawGeometry(null, new Pen(OverlayFrame.Brush(Options.Accent), 3), clip);
        if (Options.AlwaysOnTop)
        {
            var center = new Point(x + size / 2, y + size / 2); var pen = new Pen(OverlayFrame.Brush("#FFFFFF", .7), 1);
            frame.Drawing.DrawLine(pen, new(center.X - 8, center.Y), new(center.X + 8, center.Y));
            frame.Drawing.DrawLine(pen, new(center.X, center.Y - 8), new(center.X, center.Y + 8));
        }
    }
}
