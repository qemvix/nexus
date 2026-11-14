using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;
using Nexus.Core;

namespace Nexus.Cursor;

/// <summary>Runs the exact executable supplied by qemvix; Nexus owns only the process it starts.</summary>
public sealed class CursorBlur() : NexusModule(new("cursor-blur", "CursorBlur", "Cursor & mouse", "The original CursorBlur engine, started and stopped with Nexus. No extra setup.", Capability.Mouse))
{
    private CursorBlurProcess? _engine;
    public override bool SupportsConfiguration => false;
    public override string? RuntimeError => _engine is { HasExited: true } ? "CursorBlur stopped unexpectedly. Enable it again to restart the engine." : null;
    protected override void OnStart() { _engine = CursorBlurProcess.Start(); Resources.Add(_engine); }
    public override void Stop() { base.Stop(); _engine = null; }
}

/// <summary>A kill-on-close job prevents a native overlay surviving a Nexus crash or forced exit.</summary>
internal sealed class CursorBlurProcess : IDisposable
{
    internal const string ExpectedSha256 = "1def15d2653a09c21826d430cab3a7c8f24778d385dd0e15177b786582207f2c";
    private readonly Process _process;
    private readonly SafeFileHandle _job;
    private bool _disposed;
    internal int Id => _process.Id;
    internal bool HasExited => _disposed || _process.HasExited;
    private CursorBlurProcess(Process process, SafeFileHandle job) { _process = process; _job = job; }
    internal static byte[] ReadBundledEngine()
    {
        using var stream = typeof(CursorBlur).Assembly.GetManifestResourceStream("Nexus.Cursor.NativeCursorBlur.exe") ?? throw new FileNotFoundException("The bundled CursorBlur engine is missing. Reinstall Nexus.");
        using var memory = new MemoryStream(); stream.CopyTo(memory); var bytes = memory.ToArray();
        if (!string.Equals(Convert.ToHexString(SHA256.HashData(bytes)), ExpectedSha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The bundled CursorBlur engine did not pass its integrity check.");
        return bytes;
    }
    internal static string PrepareEngine(string root)
    {
        string directory = Path.Combine(root, "engines", "cursorblur-" + ExpectedSha256[..12]);
        Directory.CreateDirectory(directory); string path = Path.Combine(directory, "CursorBlur.exe"); byte[] bytes = ReadBundledEngine();
        if (File.Exists(path) && string.Equals(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), ExpectedSha256, StringComparison.OrdinalIgnoreCase)) return path;
        string stage = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllBytes(stage, bytes); File.Move(stage, path, true); }
        finally { if (File.Exists(stage)) File.Delete(stage); }
        return path;
    }
    internal static CursorBlurProcess Start()
    {
        VerifyRuntime(name => File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), name)));
        var existing = Process.GetProcessesByName("CursorBlur");
        try { if (existing.Any(p => !p.HasExited)) throw new InvalidOperationException("CursorBlur is already running outside this module. Exit that copy before enabling Nexus CursorBlur."); }
        finally { foreach (var process in existing) process.Dispose(); }
        string path = PrepareEngine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nexus"));
        var job = new SafeFileHandle(CreateJobObject(0, null), true);
        if (job.IsInvalid) { job.Dispose(); throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create the CursorBlur lifetime job."); }
        Process? child = null;
        try
        {
            var limits = new ExtendedLimits { Basic = new BasicLimits { LimitFlags = 0x2000 } };
            if (!SetInformationJobObject(job, 9, ref limits, (uint)Marshal.SizeOf<ExtendedLimits>())) throw new Win32Exception(Marshal.GetLastWin32Error());
            child = Process.Start(new ProcessStartInfo(path) { WorkingDirectory = Path.GetDirectoryName(path)!, UseShellExecute = false, CreateNoWindow = true }) ?? throw new InvalidOperationException("Windows could not start CursorBlur.");
            if (!AssignProcessToJobObject(job, child.Handle)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not attach CursorBlur to Nexus.");
            if (child.WaitForExit(200)) throw new InvalidOperationException($"CursorBlur exited during startup (code {child.ExitCode}). Close any standalone copy and try again.");
            return new(child, job);
        }
        catch
        {
            if (child is not null) { if (!child.HasExited) child.Kill(); child.Dispose(); }
            job.Dispose(); throw;
        }
    }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true; _job.Dispose();
        if (!_process.HasExited && !_process.WaitForExit(2000)) _process.Kill();
        _process.Dispose();
    }
    internal static void VerifyRuntime(Func<string, bool> present)
    {
        if (new[] { "MSVCP140.dll", "VCRUNTIME140.dll", "VCRUNTIME140_1.dll" }.Any(name => !present(name))) throw new InvalidOperationException("CursorBlur needs the Microsoft Visual C++ v14 x64 runtime. Install or repair it using Runtime help, then enable the module again.");
    }
    [StructLayout(LayoutKind.Sequential)] private struct BasicLimits { public long PerProcessTime, PerJobTime; public uint LimitFlags; public nuint MinimumWorkingSet, MaximumWorkingSet; public uint ActiveProcessLimit; public nuint Affinity; public uint PriorityClass, SchedulingClass; }
    [StructLayout(LayoutKind.Sequential)] private struct IoCounters { public ulong ReadOperations, WriteOperations, OtherOperations, ReadBytes, WriteBytes, OtherBytes; }
    [StructLayout(LayoutKind.Sequential)] private struct ExtendedLimits { public BasicLimits Basic; public IoCounters Io; public nuint ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemory, PeakJobMemory; }
    [DllImport("kernel32.dll", EntryPoint = "CreateJobObjectW", SetLastError = true, CharSet = CharSet.Unicode)] private static extern nint CreateJobObject(nint attributes, string? name);
    [DllImport("kernel32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetInformationJobObject(SafeFileHandle job, int informationClass, ref ExtendedLimits information, uint length);
    [DllImport("kernel32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool AssignProcessToJobObject(SafeFileHandle job, nint process);
}
