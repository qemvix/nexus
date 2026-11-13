using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Nexus.Core;

/// <summary>Auditable Win32 boundary. All handles stay in their owning service.</summary>
public static class Native
{
    [StructLayout(LayoutKind.Sequential)] public struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; public readonly int Width => Right - Left; public readonly int Height => Bottom - Top; }
    [StructLayout(LayoutKind.Sequential)] public struct MouseData { public Point Point; public uint Data, Flags, Time; public nuint Extra; }
    [StructLayout(LayoutKind.Sequential)] public struct KeyData { public uint Key, Scan, Flags, Time; public nuint Extra; }
    [StructLayout(LayoutKind.Sequential)] public struct MouseInput { public int X, Y; public uint Data, Flags, Time; public nuint Extra; }
    [StructLayout(LayoutKind.Explicit)] public struct InputUnion { [FieldOffset(0)] public MouseInput Mouse; }
    [StructLayout(LayoutKind.Sequential)] public struct Input { public uint Type; public InputUnion Value; }
    [StructLayout(LayoutKind.Sequential)]
    public struct MemoryStatus
    {
        public uint Length, Load; public ulong TotalPhysical, AvailablePhysical, TotalPage, AvailablePage, TotalVirtual, AvailableVirtual, AvailableExtended;
    }
    public delegate nint HookCallback(int code, nint message, nint data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] public static extern nint CreateWindowEx(uint extendedStyle, string className, string title, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static extern bool DestroyWindow(nint window);
    [DllImport("user32.dll", SetLastError = true)] public static extern nint SetWindowsHookEx(int id, HookCallback callback, nint instance, uint thread);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] public static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern nint GetModuleHandle(string? name);
    [DllImport("user32.dll")] public static extern nint GetForegroundWindow();
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static extern bool GetWindowRect(nint window, out Rect rect);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static extern bool IsWindow(nint window);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(nint window, out uint process);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] public static extern uint SendInput(uint count, Input[] input, int size);
    [DllImport("user32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] public static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static extern bool UnregisterHotKey(nint window, int id);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] public static extern nint GetWindowLongPtr(nint window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)] public static extern nint SetWindowLongPtr(nint window, int index, nint value);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] public static extern bool SetLayeredWindowAttributes(nint window, uint key, byte alpha, uint flags);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static extern bool GetLayeredWindowAttributes(nint window, out uint key, out byte alpha, out uint flags);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static extern bool ShowWindow(nint window, int command);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static extern bool SetWindowDisplayAffinity(nint window, uint affinity);
    [DllImport("gdi32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static extern bool DeleteObject(nint handle);
    [DllImport("kernel32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
    [DllImport("kernel32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static extern bool GetSystemTimes(out long idle, out long kernel, out long user);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static extern bool AddClipboardFormatListener(nint window);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static extern bool RemoveClipboardFormatListener(nint window);
    [DllImport("user32.dll")] public static extern void keybd_event(byte key, byte scan, uint flags, nuint extra);
    public delegate void WinEventCallback(nint hook, uint eventId, nint window, int objectId, int childId, uint thread, uint time);
    [DllImport("user32.dll")] public static extern nint SetWinEventHook(uint min, uint max, nint module, WinEventCallback callback, uint process, uint thread, uint flags);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static extern bool UnhookWinEvent(nint hook);
    [DllImport("advapi32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool OpenProcessToken(nint process, uint access, out Microsoft.Win32.SafeHandles.SafeAccessTokenHandle token);
    [DllImport("advapi32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetTokenInformation(Microsoft.Win32.SafeHandles.SafeAccessTokenHandle token, int informationClass, out int value, int length, out int required);
    public static bool CanTransformForeground()
    {
        GetWindowThreadProcessId(GetForegroundWindow(), out uint pid);
        try
        {
            using var target = Process.GetProcessById((int)pid); using var current = Process.GetCurrentProcess();
            return !Elevated(target) || Elevated(current);
        }
        catch { return false; } // Inaccessible/protected targets keep their original wheel input.
    }
    private static bool Elevated(Process process)
    {
        if (!OpenProcessToken(process.Handle, 8, out var token)) throw new Win32Exception(Marshal.GetLastWin32Error());
        using (token)
        {
            if (!GetTokenInformation(token, 20, out int value, sizeof(int), out _)) throw new Win32Exception(Marshal.GetLastWin32Error());
            return value != 0;
        }
    }
    public static nint ExternalForeground()
    {
        var window = GetForegroundWindow(); GetWindowThreadProcessId(window, out uint pid);
        return pid == Environment.ProcessId ? 0 : window;
    }
    public static string ForegroundProcess()
    {
        GetWindowThreadProcessId(GetForegroundWindow(), out var pid);
        try { using var process = Process.GetProcessById((int)pid); return process.ProcessName; } catch { return ""; }
    }
    public static bool SendWheel(int delta)
    {
        Input[] inputs = [new() { Type = 0, Value = new() { Mouse = new() { Data = unchecked((uint)delta), Flags = 0x0800 } } }];
        return SendInput(1, inputs, Marshal.SizeOf<Input>()) == 1;
    }
}
