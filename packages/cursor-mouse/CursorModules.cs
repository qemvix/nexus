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
    public static INexusModule[] Create() => [new CursorAura(), new ClickFx(), new ScrollFlow(), new CursorLens()];
}
public sealed class CursorAura() : NexusModule(new("cursor-aura", "Cursor Aura", "Cursor & mouse", "Give your pointer a glow, soft halo, trail, particles or spotlight.", Capability.Mouse),
    new("Mode", "Effect", Choices: ["Glow", "Blur", "Trail", "Particles", "Spotlight"]), new("Size", "Radius", 16, 180), new("Intensity", "Opacity", .05, 1), new("Accent", "Accent color", IsText: true))
{
    private readonly Queue<(int X, int Y)> _trail = [];
    protected override void OnStart()
    {
        Resources.Add(Context.Input.Mouse(e => { if (e.Message != 0x200) return; _trail.Enqueue((e.X, e.Y)); while (_trail.Count > 22) _trail.Dequeue(); }));
        Resources.Add(new Overlay(Draw, 30));
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
public sealed class ClickFx() : NexusModule(new("click-fx", "ClickFX", "Cursor & mouse", "Distinct left and right click ripples, rings and shockwaves.", Capability.Mouse),
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
        Resources.Add(new Overlay(Draw)); Resources.Add(new ActionDisposable(_clicks.Clear));
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
/// <summary>Integrates fractional wheel deltas with a decay curve. Injected packets never re-enter the filter.</summary>
public sealed class ScrollFlow() : NexusModule(new("scroll-flow", "ScrollFlow", "Cursor & mouse", "Smooth wheel momentum with acceleration, inversion and per-app profiles.", Capability.Mouse),
    new("Sensitivity", "Sensitivity", .1, 5), new("Momentum", "Momentum", 0, .95), new("Speed", "Acceleration", .1, 3), new("Invert", "Invert scrolling", IsToggle: true))
{
    private double _pending, _fraction, _momentum;
    private string _process = "";
    private long _lastWheel;
    private bool _canTransform;
    private long _lastFailure;
    protected override void OnStart()
    {
        var profileTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        profileTimer.Tick += (_, _) => { _process = Native.ForegroundProcess(); _canTransform = Native.CanTransformForeground(); }; profileTimer.Start();
        Resources.Add(new ActionDisposable(profileTimer.Stop));
        Resources.Add(Context.Input.WheelFilter(e =>
        {
            if (!_canTransform || (_lastFailure != 0 && Stopwatch.GetElapsedTime(_lastFailure).TotalSeconds < 5) || Native.ExternalForeground() == 0) return false;
            var profile = Options.ScrollProfiles.FirstOrDefault(p => string.Equals(p.Process, _process, StringComparison.OrdinalIgnoreCase));
            double sensitivity = profile?.Sensitivity ?? Options.Sensitivity;
            bool invert = profile?.Invert ?? Options.Invert;
            double elapsed = _lastWheel == 0 ? 1 : Stopwatch.GetElapsedTime(_lastWheel).TotalSeconds; _lastWheel = Stopwatch.GetTimestamp();
            double acceleration = elapsed < .08 ? Math.Min(2.5, 1 + Options.Speed * (.08 - elapsed) * 8) : 1;
            _pending = Math.Clamp(_pending + e.Delta * sensitivity * acceleration * (invert ? -1 : 1), -6000, 6000);
            _momentum = profile?.Momentum ?? Options.Momentum;
            return true;
        }));
        var timer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(8) };
        timer.Tick += (_, _) =>
        {
            if (Math.Abs(_pending) < .05) { _pending = 0; return; }
            double step = _pending * (1 - _momentum * .94); _pending -= step;
            _fraction += step; int delta = (int)_fraction; _fraction -= delta;
            if (delta != 0 && !Native.SendWheel(delta)) { _lastFailure = Stopwatch.GetTimestamp(); _pending = _fraction = 0; }
        };
        timer.Start(); Resources.Add(new ActionDisposable(() => { timer.Stop(); _pending = _fraction = 0; }));
    }
    public override FrameworkElement? CreatePanel(ModuleOptions options) => Ui.JsonEditor("Per-app profiles: Process, Sensitivity, Invert, Momentum", options.ScrollProfiles, (List<ScrollProfile> profiles) => { options.ScrollProfiles = profiles; options.Validate(); });
}
public sealed class CursorLens() : NexusModule(new("cursor-lens", "Cursor Lens", "Cursor & mouse", "A mouse-following magnifier with circular or square framing.", Capability.Screen),
    new("Size", "Lens size", 80, 320), new("Zoom", "Magnification", 1.1, 8), new("Shape", "Shape", Choices: ["Circle", "Square"]), new("AlwaysOnTop", "Presentation crosshair", IsToggle: true))
{
    private BitmapSource? _image;
    private long _capture;
    protected override void OnStart()
    {
        Resources.Add(new Overlay(Draw, 12)); Resources.Add(new ActionDisposable(() => _image = null));
    }
    private void Draw(OverlayFrame frame)
    {
        Native.GetCursorPos(out var point);
        if (_capture == 0 || Stopwatch.GetElapsedTime(_capture).TotalMilliseconds > 70)
        {
            _capture = Stopwatch.GetTimestamp();
            int sourceSize = Math.Max(12, (int)(Options.Size / Options.Zoom));
            var monitor = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(point.X, point.Y)).Bounds;
            int left = Math.Clamp(point.X - sourceSize / 2, monitor.Left, Math.Max(monitor.Left, monitor.Right - sourceSize));
            int top = Math.Clamp(point.Y - sourceSize / 2, monitor.Top, Math.Max(monitor.Top, monitor.Bottom - sourceSize));
            using var bitmap = new System.Drawing.Bitmap(sourceSize, sourceSize);
            using (var graphics = System.Drawing.Graphics.FromImage(bitmap)) graphics.CopyFromScreen(left, top, 0, 0, bitmap.Size);
            var handle = bitmap.GetHbitmap();
            try { _image = Imaging.CreateBitmapSourceFromHBitmap(handle, 0, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions()); _image.Freeze(); }
            finally { Native.DeleteObject(handle); }
        }
        var p = frame.Point(point.X, point.Y); double size = Options.Size;
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
