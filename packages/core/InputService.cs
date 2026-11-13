using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace Nexus.Core;

public sealed record MouseEvent(int Message, int X, int Y, int Delta, bool Injected);
public sealed record KeyboardEvent(int Key, bool Down);
/// <summary>Ref-counted hooks: no key collection unless KeyCast is running. Wheel filters must complete synchronously.</summary>
public sealed class InputService : IDisposable
{
    private readonly List<Action<MouseEvent>> _mouse = [];
    private readonly List<Action<KeyboardEvent>> _keyboard = [];
    private readonly List<Func<MouseEvent, bool>> _filters = [];
    private readonly Native.HookCallback _mouseCallback, _keyboardCallback;
    private nint _mouseHook, _keyHook;
    public InputService() { _mouseCallback = OnMouse; _keyboardCallback = OnKey; }
    public IDisposable Mouse(Action<MouseEvent> listener)
    {
        _mouse.Add(listener);
        try { Update(); } catch { _mouse.Remove(listener); throw; }
        return new ActionDisposable(() => { _mouse.Remove(listener); Update(); });
    }
    public IDisposable WheelFilter(Func<MouseEvent, bool> listener)
    {
        _filters.Add(listener);
        try { Update(); } catch { _filters.Remove(listener); throw; }
        return new ActionDisposable(() => { _filters.Remove(listener); Update(); });
    }
    public IDisposable Keyboard(Action<KeyboardEvent> listener)
    {
        _keyboard.Add(listener);
        try { Update(); } catch { _keyboard.Remove(listener); throw; }
        return new ActionDisposable(() => { _keyboard.Remove(listener); Update(); });
    }
    private void Update()
    {
        if (_mouse.Count + _filters.Count > 0 && _mouseHook == 0)
        {
            _mouseHook = Native.SetWindowsHookEx(14, _mouseCallback, Native.GetModuleHandle(null), 0);
            if (_mouseHook == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        if (_mouse.Count + _filters.Count == 0 && _mouseHook != 0) { Native.UnhookWindowsHookEx(_mouseHook); _mouseHook = 0; }
        if (_keyboard.Count > 0 && _keyHook == 0)
        {
            _keyHook = Native.SetWindowsHookEx(13, _keyboardCallback, Native.GetModuleHandle(null), 0);
            if (_keyHook == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        if (_keyboard.Count == 0 && _keyHook != 0) { Native.UnhookWindowsHookEx(_keyHook); _keyHook = 0; }
    }
    private nint OnMouse(int code, nint message, nint data)
    {
        if (code >= 0)
        {
            var value = Marshal.PtrToStructure<Native.MouseData>(data);
            var e = new MouseEvent((int)message, value.Point.X, value.Point.Y, unchecked((short)(value.Data >> 16)), (value.Flags & 1) != 0);
            try
            {
                bool suppress = false;
                if (e.Message == 0x020A && !e.Injected) foreach (var filter in _filters.ToArray()) suppress |= filter(e);
                foreach (var listener in _mouse.ToArray()) listener(e);
                if (suppress) return 1;
            }
            catch { /* Never allow managed exceptions across a native callback boundary. */ }
        }
        return Native.CallNextHookEx(_mouseHook, code, message, data);
    }
    private nint OnKey(int code, nint message, nint data)
    {
        if (code >= 0)
        {
            var key = Marshal.PtrToStructure<Native.KeyData>(data);
            if ((key.Flags & 0x10) == 0)
            {
                var e = new KeyboardEvent((int)key.Key, message == 0x100 || message == 0x104);
                try { foreach (var listener in _keyboard.ToArray()) listener(e); } catch { }
            }
        }
        return Native.CallNextHookEx(_keyHook, code, message, data);
    }
    public void Dispose()
    {
        _mouse.Clear(); _filters.Clear(); _keyboard.Clear(); Update(); GC.SuppressFinalize(this);
    }
}
