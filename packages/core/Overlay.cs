using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Diagnostics;
using Forms = System.Windows.Forms;

namespace Nexus.Core;

public sealed class OverlayFrame(DrawingContext drawing, Rect bounds, Func<Native.Point, Point> point, double scale)
{
    public long FrameId => Overlay.FrameId;
    public DrawingContext Drawing { get; } = drawing;
    public Rect Bounds { get; } = bounds;
    public Point Point(int x, int y) => point(new() { X = x, Y = y });
    public double Scale { get; } = scale;
    public Rect Rect(Native.Rect rect) { var p = Point(rect.Left, rect.Top); return new(p.X, p.Y, rect.Width / Scale, rect.Height / Scale); }
    public static SolidColorBrush Brush(string color, double opacity = 1)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)) { Opacity = Math.Clamp(opacity, 0, 1) }; brush.Freeze(); return brush;
    }
    public void Text(string text, Point at, double size = 16, string color = "#EDEBFF")
    {
        var formatted = new FormattedText(text, System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, Brush(color), Scale);
        Drawing.DrawText(formatted, at);
    }
}
/// <summary>One transparent, non-activating, click-through window per monitor; no input interception.</summary>
public sealed class Overlay : IDisposable
{
    private static readonly List<Overlay> Active = [];
    private static TimeSpan _lastRendering = TimeSpan.MinValue;
    private static double _frameCredit;
    private static int _targetFrameRate = 60;
    public static long FrameId { get; private set; }
    public static int TargetFrameRate { get => _targetFrameRate; set => _targetFrameRate = Math.Clamp(value, 60, 240); }
    public static string PerformanceSummary => $"Target: {TargetFrameRate} FPS · composition synchronized · {Active.Count} overlays\n" + string.Join("\n", Active.Select(x => $"{x._name}: {x._actualFps:F0} rendered FPS"));
    private readonly List<OverlayWindow> _windows = [];
    private readonly Action<OverlayFrame> _paint;
    private readonly string _name;
    private readonly Stopwatch _measurement = Stopwatch.StartNew();
    private int _frames;
    private double _actualFps;
    private bool _disposed;
    public Overlay(Action<OverlayFrame> paint, string name = "Overlay")
    {
        _paint = paint; _name = name; BuildWindows();
        if (Active.Count == 0) { _lastRendering = TimeSpan.MinValue; _frameCredit = 0; CompositionTarget.Rendering += RenderAll; }
        Active.Add(this);
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += DisplayChanged;
    }
    private void BuildWindows()
    {
        foreach (var window in _windows) window.Close(); _windows.Clear();
        foreach (var screen in Forms.Screen.AllScreens)
        {
            bool measure = _windows.Count == 0;
            var window = new OverlayWindow(_paint, screen.Bounds, measure ? RecordFrame : () => { }); _windows.Add(window); window.Show();
        }
    }
    private static void RenderAll(object? sender, EventArgs args)
    {
        if (args is not RenderingEventArgs frame || frame.RenderingTime == _lastRendering) return;
        double interval = 1d / TargetFrameRate;
        double elapsed = _lastRendering == TimeSpan.MinValue ? interval : Math.Max(0, (frame.RenderingTime - _lastRendering).TotalSeconds);
        _lastRendering = frame.RenderingTime;
        // Preserve fractional frame credit: resetting the timestamp would turn a 60 FPS target on a 144 Hz display into 48 FPS.
        _frameCredit = Math.Min(_frameCredit + elapsed, interval * 2);
        if (_frameCredit + .0002 < interval) return;
        _frameCredit = Math.Max(0, _frameCredit - interval); FrameId++;
        foreach (var overlay in Active.ToArray()) foreach (var window in overlay._windows) window.InvalidateVisual();
    }
    private void RecordFrame()
    {
        _frames++;
        if (_measurement.Elapsed.TotalSeconds < 1) return;
        _actualFps = _frames / _measurement.Elapsed.TotalSeconds; _frames = 0; _measurement.Restart();
    }
    private void DisplayChanged(object? sender, EventArgs args) => System.Windows.Application.Current.Dispatcher.BeginInvoke(() => { if (!_disposed) BuildWindows(); });
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= DisplayChanged;
        Active.Remove(this);
        if (Active.Count == 0) CompositionTarget.Rendering -= RenderAll;
        foreach (var window in _windows) window.Close(); _windows.Clear(); GC.SuppressFinalize(this);
    }
    private sealed class OverlayWindow : Window
    {
        private readonly Action<OverlayFrame> _paint;
        private readonly System.Drawing.Rectangle _screen;
        private readonly Action _recordFrame;
        public OverlayWindow(Action<OverlayFrame> paint, System.Drawing.Rectangle screen, Action recordFrame)
        {
            _paint = paint; _screen = screen; _recordFrame = recordFrame;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            ShowInTaskbar = false; ShowActivated = false; Topmost = true; ResizeMode = ResizeMode.NoResize; IsHitTestVisible = false;
            Left = screen.Left; Top = screen.Top; Width = Math.Max(1, screen.Width); Height = Math.Max(1, screen.Height);
            SourceInitialized += (_, _) =>
            {
                var handle = new WindowInteropHelper(this).Handle;
                var style = Native.GetWindowLongPtr(handle, -20);
                Native.SetWindowLongPtr(handle, -20, style | 0x20 | 0x80 | 0x08000000);
                Native.SetWindowDisplayAffinity(handle, 0x11);
                Native.SetWindowPos(handle, -1, screen.Left, screen.Top, screen.Width, screen.Height, 0x10);
            };
        }
        protected override void OnRender(DrawingContext drawing)
        {
            base.OnRender(drawing);
            _recordFrame();
            var dpi = VisualTreeHelper.GetDpi(this);
            var frame = new OverlayFrame(drawing, new Rect(0, 0, ActualWidth, ActualHeight), p => new Point((p.X - _screen.Left) / dpi.DpiScaleX, (p.Y - _screen.Top) / dpi.DpiScaleY), dpi.DpiScaleX);
            try { _paint(frame); } catch { /* Rendering may fail during display teardown. The overlay remains click-through. */ }
        }
    }
}
