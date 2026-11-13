using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace Nexus.Core;

public sealed class OverlayFrame(DrawingContext drawing, Rect bounds, Func<Native.Point, Point> point, double scale)
{
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
    private readonly List<OverlayWindow> _windows = [];
    private readonly DispatcherTimer _timer;
    private readonly Action<OverlayFrame> _paint;
    private bool _disposed;
    public Overlay(Action<OverlayFrame> paint, int fps = 30)
    {
        _paint = paint; BuildWindows();
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1d / fps) };
        _timer.Tick += Tick; _timer.Start();
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += DisplayChanged;
    }
    private void BuildWindows()
    {
        foreach (var window in _windows) window.Close(); _windows.Clear();
        foreach (var screen in Forms.Screen.AllScreens) { var window = new OverlayWindow(_paint, screen.Bounds); _windows.Add(window); window.Show(); }
    }
    private void Tick(object? sender, EventArgs args) { foreach (var window in _windows) window.InvalidateVisual(); }
    private void DisplayChanged(object? sender, EventArgs args) => System.Windows.Application.Current.Dispatcher.BeginInvoke(() => { if (!_disposed) BuildWindows(); });
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= DisplayChanged;
        _timer.Stop(); _timer.Tick -= Tick;
        foreach (var window in _windows) window.Close(); _windows.Clear(); GC.SuppressFinalize(this);
    }
    private sealed class OverlayWindow : Window
    {
        private readonly Action<OverlayFrame> _paint;
        private readonly System.Drawing.Rectangle _screen;
        public OverlayWindow(Action<OverlayFrame> paint, System.Drawing.Rectangle screen)
        {
            _paint = paint; _screen = screen;
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
            var dpi = VisualTreeHelper.GetDpi(this);
            var frame = new OverlayFrame(drawing, new Rect(0, 0, ActualWidth, ActualHeight), p => new Point((p.X - _screen.Left) / dpi.DpiScaleX, (p.Y - _screen.Top) / dpi.DpiScaleY), dpi.DpiScaleX);
            try { _paint(frame); } catch { /* Rendering may fail during display teardown. The overlay remains click-through. */ }
        }
    }
}
