using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Nexus.Core;

/// <summary>Restores opacity/topmost only while the original window still belongs to the same process.</summary>
public sealed class WindowStyleLease : IDisposable
{
    private readonly nint _window, _style;
    private readonly uint _pid, _key, _flags;
    private readonly byte _alpha;
    private bool _disposed;
    public WindowStyleLease(nint window, byte opacity, bool topmost)
    {
        if (!Native.IsWindow(window)) throw new ArgumentException("Window is no longer available.");
        _window = window; _style = Native.GetWindowLongPtr(window, -20); Native.GetWindowThreadProcessId(window, out _pid);
        bool layered = Native.GetLayeredWindowAttributes(window, out _key, out _alpha, out _flags);
        if ((_style.ToInt64() & 0x80000) != 0 && !layered) throw new InvalidOperationException("This window uses per-pixel layering; its transparency is left unchanged.");
        if (!layered) { _alpha = 255; _flags = 2; }
        Native.SetWindowLongPtr(window, -20, _style | 0x80000);
        if (!Native.SetLayeredWindowAttributes(window, 0, opacity, 2)) { int error = Marshal.GetLastWin32Error(); Dispose(); throw new Win32Exception(error, "Window opacity is unavailable (Windows error " + error + ")."); }
        if (!Native.SetWindowPos(window, new nint(topmost ? -1 : -2), 0, 0, 0, 0, 0x13))
        {
            int error = Marshal.GetLastWin32Error(); Dispose(); throw new Win32Exception(error, "Window topmost control is unavailable (Windows error " + error + ").");
        }
    }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        Native.GetWindowThreadProcessId(_window, out uint pid);
        if (!Native.IsWindow(_window) || pid != _pid) return;
        Native.SetLayeredWindowAttributes(_window, _key, _alpha, _flags);
        Native.SetWindowLongPtr(_window, -20, _style);
        Native.SetWindowPos(_window, (_style.ToInt64() & 8) != 0 ? -1 : -2, 0, 0, 0, 0, 0x33);
        GC.SuppressFinalize(this);
    }
}
