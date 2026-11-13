using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Nexus.Core;

namespace Nexus.Cursor;

/// <summary>Port of qemvix/CursorBlur's real cursor-image trail, interpolation, speed tint and time-based fade.</summary>
public sealed class CursorBlur() : NexusModule(new("cursor-blur", "CursorBlur", "Cursor & mouse", "Motion blur made from your actual cursor image, with speed-sensitive fade and tint.", Capability.Mouse),
    new("BlurSensitivity", "Speed sensitivity", .001, 1), new("TrailFadeMilliseconds", "Trail fade (milliseconds)", 1, 1000),
    new("TrailAlpha", "Maximum trail alpha (1–255)", 1, 255), new("CursorTint", "Cursor trail tint", IsText: true))
{
    private readonly CursorTrail _trail = new();
    private readonly CursorImageCache _cursor = new();
    private long _sampleFrame = -1;
    protected override void OnStart()
    {
        Resources.Add(new Overlay(Draw, "CursorBlur"));
        Resources.Add(new ActionDisposable(() => { _trail.Clear(); _cursor.Clear(); _sampleFrame = -1; }));
    }
    private void Draw(OverlayFrame frame)
    {
        if (Context.ReducedMotion()) { _trail.Clear(); return; }
        var info = new CursorImageCache.CursorInfo { Size = Marshal.SizeOf<CursorImageCache.CursorInfo>() };
        if (!CursorImageCache.GetCursorInfo(ref info) || info.Flags != 1 || info.Handle == 0) { _trail.Clear(); return; }
        double now = System.Diagnostics.Stopwatch.GetTimestamp() * 1000d / System.Diagnostics.Stopwatch.Frequency;
        if (_sampleFrame != frame.FrameId)
        {
            _sampleFrame = frame.FrameId; _trail.Add(info.Position.X, info.Position.Y, now, Options.TrailFadeMilliseconds);
            _cursor.Refresh(info.Handle, Options.CursorTint);
        }
        if (_cursor.Image is not { } image) return;
        var samples = _trail.Samples; int budget = 4096;
        // As upstream, draw newer segments first, interpolating the actual cursor between samples.
        for (int i = samples.Length - 2; i >= 0 && budget > 0; i--)
        {
            var from = samples[i]; var to = samples[i + 1]; double age = now - from.Time;
            if (age > Options.TrailFadeMilliseconds) continue;
            double dx = to.X - from.X, dy = to.Y - from.Y, distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance < 1) continue;
            int steps = Math.Min(512, (int)Math.Ceiling(distance));
            for (int j = steps; j >= 0 && budget-- > 0; j--)
            {
                double t = (double)j / steps;
                double alpha = Options.TrailAlpha * Math.Max(0, 1 - (age + age * t * .1) / Options.TrailFadeMilliseconds) * Math.Clamp(distance * Options.BlurSensitivity, 0, 1);
                if (alpha < 3) continue;
                var p = frame.Point((int)Math.Round(from.X + dx * t) - _cursor.HotX, (int)Math.Round(from.Y + dy * t) - _cursor.HotY);
                var rect = new Rect(p.X, p.Y, image.PixelWidth / frame.Scale, image.PixelHeight / frame.Scale);
                if (!rect.IntersectsWith(frame.Bounds)) continue;
                frame.Drawing.PushOpacity(alpha / 255); frame.Drawing.DrawImage(image, rect); frame.Drawing.Pop();
            }
        }
    }
}

internal sealed class CursorTrail
{
    internal readonly record struct Sample(int X, int Y, double Time);
    private readonly Queue<Sample> _samples = [];
    internal Sample[] Samples => _samples.ToArray();
    internal void Add(int x, int y, double time, double fade)
    {
        if (_samples.Count == 0 || _samples.Last().X != x || _samples.Last().Y != y) _samples.Enqueue(new(x, y, time));
        while (_samples.Count > 500 || (_samples.TryPeek(out var first) && time - first.Time > fade + 50)) _samples.Dequeue();
    }
    internal void Clear() => _samples.Clear();
}

internal sealed class CursorImageCache
{
    [StructLayout(LayoutKind.Sequential)] internal struct CursorInfo { internal int Size, Flags; internal nint Handle; internal Native.Point Position; }
    [StructLayout(LayoutKind.Sequential)] private struct IconInfo { [MarshalAs(UnmanagedType.Bool)] internal bool Icon; internal uint X, Y; internal nint Mask, Color; }
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetCursorInfo(ref CursorInfo info);
    [DllImport("user32.dll", EntryPoint = "LoadCursorW")] internal static extern nint LoadCursor(nint instance, nint name);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetIconInfo(nint icon, out IconInfo info);
    private nint _handle;
    private string _tint = "";
    internal BitmapSource? Image { get; private set; }
    internal int HotX { get; private set; }
    internal int HotY { get; private set; }
    internal void Clear() { _handle = 0; _tint = ""; Image = null; }
    internal void Refresh(nint handle, string tint)
    {
        if (handle == _handle && tint == _tint && Image is not null) return;
        if (!GetIconInfo(handle, out var icon)) return;
        try
        {
            var source = Imaging.CreateBitmapSourceFromHIcon(handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            var converted = new FormatConvertedBitmap(source, PixelFormats.Pbgra32, null, 0);
            int stride = converted.PixelWidth * 4; byte[] pixels = new byte[stride * converted.PixelHeight]; converted.CopyPixels(pixels, stride, 0);
            var color = (Color)ColorConverter.ConvertFromString(tint);
            for (int i = 0; i < pixels.Length; i += 4) { pixels[i] = (byte)(pixels[i] * color.B / 255); pixels[i + 1] = (byte)(pixels[i + 1] * color.G / 255); pixels[i + 2] = (byte)(pixels[i + 2] * color.R / 255); }
            Image = BitmapSource.Create(converted.PixelWidth, converted.PixelHeight, 96, 96, PixelFormats.Pbgra32, null, pixels, stride); Image.Freeze();
            _handle = handle; _tint = tint; HotX = (int)icon.X; HotY = (int)icon.Y;
        }
        finally { if (icon.Mask != 0) Native.DeleteObject(icon.Mask); if (icon.Color != 0) Native.DeleteObject(icon.Color); }
    }
}
