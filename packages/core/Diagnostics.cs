using System.Diagnostics;

namespace Nexus.Core;

public sealed class Diagnostics(string directory)
{
    public string LastError { get; private set; } = "None";
    public void Log(string area, Exception exception)
    {
        LastError = $"{area}: {exception.GetType().Name}: {exception.Message}";
        try
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "diagnostics.log");
            if (File.Exists(path) && new FileInfo(path).Length > 512_000) File.Move(path, path + ".previous", true);
            // Intentionally exclude input events, clipboard values, window titles and memory dumps.
            File.AppendAllText(path, $"{DateTimeOffset.UtcNow:O} {LastError}{Environment.NewLine}");
        }
        catch { /* Disk failures must not crash the UI error handler. */ }
    }
    public static string Summary()
    {
        using var process = Process.GetCurrentProcess();
        return $"{process.WorkingSet64 / 1024 / 1024} MB working set · {process.Threads.Count} threads · {Environment.Is64BitProcess switch { true => "x64", false => "x86" }}";
    }
}
public sealed class ActionDisposable(Action action) : IDisposable
{
    private Action? _action = action;
    public void Dispose() => Interlocked.Exchange(ref _action, null)?.Invoke();
}
