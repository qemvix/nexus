using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace Nexus.Core;

public readonly record struct Hotkey(uint Modifiers, uint Key)
{
    public static Hotkey Parse(string text)
    {
        var parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) throw new FormatException("Use a modifier and key, for example Ctrl+Alt+N.");
        uint mods = 0;
        foreach (string modifier in parts[..^1]) mods |= modifier.ToUpperInvariant() switch
        {
            "CTRL" or "CONTROL" => 2u,
            "ALT" => 1u,
            "SHIFT" => 4u,
            "WIN" => 8u,
            _ => throw new FormatException("Unknown hotkey modifier.")
        };
        if (!Enum.TryParse<Key>(parts[^1], true, out var key) || !Enum.IsDefined(key) || key == System.Windows.Input.Key.None) throw new FormatException("Unknown key name.");
        return new(mods, (uint)KeyInterop.VirtualKeyFromKey(key));
    }
}
public sealed class HotkeyManager : IDisposable
{
    private readonly HwndSource _source = new(new HwndSourceParameters("Nexus hotkeys") { WindowStyle = 0, Width = 0, Height = 0, ParentWindow = new nint(-3) });
    private readonly Dictionary<int, (Hotkey Key, Action Action)> _bindings = [];
    private int _next = 100;
    public HotkeyManager() => _source.AddHook(Hook);
    public IDisposable Register(string combination, Action action)
    {
        var key = Hotkey.Parse(combination);
        if (_bindings.Values.Any(x => x.Key == key)) throw new InvalidOperationException("This shortcut is already assigned inside Nexus.");
        int id = _next++;
        if (!Native.RegisterHotKey(_source.Handle, id, key.Modifiers | 0x4000, key.Key)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Shortcut is reserved or used by another application.");
        _bindings.Add(id, (key, action));
        return new ActionDisposable(() => { Native.UnregisterHotKey(_source.Handle, id); _bindings.Remove(id); });
    }
    private nint Hook(nint hwnd, int message, nint wparam, nint lparam, ref bool handled)
    {
        if (message == 0x0312 && _bindings.TryGetValue((int)wparam, out var binding)) { handled = true; binding.Action(); }
        return 0;
    }
    public void Dispose()
    {
        foreach (int id in _bindings.Keys) Native.UnregisterHotKey(_source.Handle, id);
        _bindings.Clear(); _source.Dispose(); GC.SuppressFinalize(this);
    }
}
