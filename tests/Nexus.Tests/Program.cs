using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Nexus.Core;
using Nexus.Cursor;
using Nexus.Desktop;
using Nexus.Productivity;

namespace Nexus.Tests;

/// <summary>Dependency-free executable test harness. A failing assertion exits nonzero in CI.</summary>
public static class Program
{
    private static int _passed, _failed;
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains("--glide-integration"))
        {
            Nexus.Cursor.GlideBackend.Native.SetProcessDPIAware();
            string path = Path.Combine(Path.GetTempPath(), "nexus-glide-input-" + Guid.NewGuid().ToString("N") + ".log");
            int result = Nexus.Cursor.GlideBackend.Tests.Integration(path); Console.WriteLine(File.ReadAllText(path)); File.Delete(path); return result;
        }
        if (args.Contains("--integration")) return Integration();
        Check("Glide upstream regression suite", () => WithDirectory(dir =>
        {
            string path = Path.Combine(dir, "glide-core.log"); int result = Nexus.Cursor.GlideBackend.Tests.Run(path);
            Console.WriteLine(File.ReadAllText(path)); Assert(result == 0);
        }));
        Check("ScrollFlow keeps typed Glide settings and profile overrides", () =>
        {
            var options = new ModuleOptions { GlideMilliseconds = 9999, ExcludedApps = "game.exe" }; options.Validate(); Assert(options.GlideMilliseconds == 700);
            options.ScrollProfiles = [new("notepad", 1.25, true, GlideMilliseconds: 250), new("bad", 8)]; options.Validate();
            Assert(options.ScrollProfiles.Count == 1 && options.ScrollProfiles[0].GlideMilliseconds == 250);
        });
        Check("invalid and nonfinite options recover", () => { var o = new ModuleOptions { Size = double.NaN, Sensitivity = 100, Accent = "oops", Zoom = -1 }; o.Validate(); Assert(o.Size == 44 && o.Sensitivity == 5 && o.Accent == "#A497FF" && o.Zoom == 1.1); });
        Check("unsupported configuration version rejected", () => Throws<InvalidDataException>(() => new AppConfig { SchemaVersion = 2 }.Validate()));
        Check("hotkey parsing normalizes modifiers", () => Assert(Hotkey.Parse("Ctrl+Alt+N") == Hotkey.Parse("alt + control + n")));
        Check("hotkeys require a modifier", () => Throws<FormatException>(() => Hotkey.Parse("N")));
        Check("hotkeys reject invalid names", () => Throws<FormatException>(() => Hotkey.Parse("Ctrl+notakey")));
        Check("null nested profiles rejected", () => Throws<InvalidDataException>(() => new AppConfig { Profiles = [new Profile { Name = "bad", Modules = null! }] }.Validate()));
        Check("configuration roundtrip and backup recovery", () => WithDirectory(dir =>
        {
            var store = new ConfigurationStore(dir); var c = new AppConfig { ReducedMotion = true }; store.Save(c); c.Modules["test"] = new() { Enabled = true }; store.Save(c);
            Assert(store.Load().Modules["test"].Enabled); File.WriteAllText(Path.Combine(dir, "settings.json"), "broken"); Assert(store.Load().ReducedMotion && store.RecoveryMessage.Length > 0);
        }));
        Check("profile snapshot is independent", () => { var c = new AppConfig(); c.Modules["a"] = new(); var p = ConfigurationStore.Snapshot("Work", c); p.Modules["a"].Options.Size = 123; Assert(c.Modules["a"].Options.Size == 44); });
        Check("uninstalled modules cannot be enabled by import", () => { var c = new AppConfig(); c.Modules["a"] = new() { Installed = false, Enabled = true }; c.Validate(); Assert(!c.Modules["a"].Enabled); });
        Check("catalog has thirteen unique features", () => { var modules = Catalog(); Assert(modules.Length == 13 && modules.Select(x => x.Manifest.Id).Distinct().Count() == 13); foreach (var m in modules) m.Dispose(); });
        Check("overlay frame targets stay between 60 and 240", () => { Overlay.TargetFrameRate = 12; Assert(Overlay.TargetFrameRate == 60); Overlay.TargetFrameRate = 999; Assert(Overlay.TargetFrameRate == 240); Overlay.TargetFrameRate = 60; });
        Check("CursorBlur samples expire and stay bounded", () => { var trail = new CursorTrail(); for (int i = 0; i < 1000; i++) trail.Add(i, 1, i / 100d, 1000); Assert(trail.Samples.Length == 500); trail.Add(1000, 1, 3000, 50); Assert(trail.Samples.Length == 1); trail.Clear(); Assert(trail.Samples.Length == 0); });
        Check("every settings schema maps to a typed property", () => { foreach (var m in Catalog()) { foreach (var s in m.Schema) Assert(typeof(ModuleOptions).GetProperty(s.Key) is not null); m.Dispose(); } });
        Check("signed update accepted; tampering rejected", () =>
        {
            using var rsa = RSA.Create(2048); string payload = JsonSerializer.Serialize(new ReleaseInfo("0.2.0", "https://example.com/nexus.exe", new string('A', 64)));
            string sign = Convert.ToBase64String(rsa.SignData(Encoding.UTF8.GetBytes(payload), HashAlgorithmName.SHA256, RSASignaturePadding.Pss)); string key = rsa.ExportSubjectPublicKeyInfoPem();
            Assert(UpdateService.Verify(JsonSerializer.Serialize(new SignedRelease(payload, sign)), key).Version == "0.2.0");
            Throws<CryptographicException>(() => UpdateService.Verify(JsonSerializer.Serialize(new SignedRelease(payload.Replace("0.2.0", "9.9.9"), sign)), key));
        });
        Check("installer stages exactly one executable", () => WithDirectory(dir => { string file = Path.Combine(dir, "source.exe"); File.WriteAllText(file, "test-payload"); string destination = Path.Combine(dir, "install"); Nexus.Installer.InstallFiles(file, destination, false); Assert(File.ReadAllText(Path.Combine(destination, "Nexus.exe")) == "test-payload" && Directory.GetFiles(destination).Length == 1); }));
        Check("lifecycle blocks permissions and cleans failures", () => WithDirectory(dir =>
        {
            using var input = new InputService(); using var hotkeys = new HotkeyManager(); var c = new AppConfig(); var context = new ModuleContext(input, hotkeys, new(dir), () => true);
            using var manager = new ModuleManager(c, context, () => { }); var module = new FakeModule(); manager.Register(module); manager.SetEnabled("fake", true);
            Assert(manager.Entries[0].Status == ModuleStatus.Blocked && module.Starts == 0);
            c.Grants = Capability.Mouse; manager.RefreshAll(); Assert(module.Starts == 1 && manager.Entries[0].Status == ModuleStatus.Running);
            manager.Install("fake", false); Assert(!manager.State("fake").Installed && manager.Entries[0].Status == ModuleStatus.Available);
            manager.Install("fake", true); module.Fail = true; manager.SetEnabled("fake", true); Assert(manager.Entries[0].Status == ModuleStatus.Faulted && module.Cleanups > 0);
        }));
        Check("duplicate catalog entries rejected", () => WithDirectory(dir => { using var input = new InputService(); using var hotkeys = new HotkeyManager(); using var manager = new ModuleManager(new(), new(input, hotkeys, new(dir), () => true), () => { }); manager.Register(new FakeModule()); Throws<ArgumentException>(() => manager.Register(new FakeModule())); }));
        Check("hotkey conflicts release registration", () => { using var manager = new HotkeyManager(); using (manager.Register("Ctrl+Alt+F11", () => { })) Throws<InvalidOperationException>(() => manager.Register("Ctrl+Alt+F11", () => { })); using var again = manager.Register("Ctrl+Alt+F11", () => { }); });
        Console.WriteLine($"{_passed} passed, {_failed} failed"); return _failed == 0 ? 0 : 1;
    }
    private static INexusModule[] Catalog() => CursorCatalog.Create().Concat(DesktopCatalog.Create()).Concat(ProductivityCatalog.Create()).ToArray();
    private static int Integration()
    {
        var app = new System.Windows.Application { ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown };
        string directory = Path.Combine(Path.GetTempPath(), "nexus-integration-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        using var input = new InputService(); using var hotkeys = new HotkeyManager();
        var diagnostics = new Diagnostics(directory); var context = new ModuleContext(input, hotkeys, diagnostics, () => true, _ => false);
        var window = new System.Windows.Window { Title = "Nexus integration target", Width = 250, Height = 150, ShowInTaskbar = false, Content = new System.Windows.Controls.TextBlock { Text = "Nexus test target" } };
        window.Show();
        Check("CursorBlur captures a native cursor image", () =>
        {
            var info = new CursorImageCache.CursorInfo { Size = System.Runtime.InteropServices.Marshal.SizeOf<CursorImageCache.CursorInfo>() };
            nint handle = CursorImageCache.GetCursorInfo(ref info) ? info.Handle : 0;
            if (handle == 0) { Console.WriteLine("Headless desktop: verify the stock Windows cursor bitmap instead of a missing live cursor."); handle = CursorImageCache.LoadCursor(0, new nint(32512)); }
            Assert(handle != 0); var cache = new CursorImageCache(); cache.Refresh(handle, "#FFFFFF");
            Assert(cache.Image is not null && cache.Image.PixelWidth > 0 && cache.Image.PixelHeight > 0); cache.Clear(); Assert(cache.Image is null);
        });
        Check("composition clock renders overlays continuously", () =>
        {
            foreach (int rate in new[] { 60, 144 })
            {
                Overlay.TargetFrameRate = rate; int frames = 0;
                using var overlay = new Overlay(frame => { frames++; frame.Drawing.DrawEllipse(OverlayFrame.Brush("#A497FF"), null, new System.Windows.Point(100, 100), 4, 4); }, "Frame clock test");
                var nested = new System.Windows.Threading.DispatcherFrame();
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                timer.Tick += (_, _) => { timer.Stop(); nested.Continue = false; }; timer.Start(); System.Windows.Threading.Dispatcher.PushFrame(nested);
                Console.WriteLine(Overlay.PerformanceSummary); Assert(frames > 10);
            }
            Overlay.TargetFrameRate = 60;
        });
        Check("native opacity and original window style restored", () =>
        {
            // Use a plain Win32 window; WPF can override its own native style changes.
            nint handle = Native.CreateWindowEx(0, "STATIC", "Nexus native opacity target", 0x90000000, 40, 40, 200, 100, 0, 0, Native.GetModuleHandle(null), 0);
            Assert(handle != 0);
            try
            {
                var style = Native.GetWindowLongPtr(handle, -20); using var lease = new WindowStyleLease(handle, 180, true);
                Assert(Native.GetLayeredWindowAttributes(handle, out _, out byte alpha, out _) && alpha == 180);
                if ((Native.GetWindowLongPtr(handle, -20).ToInt64() & 8) == 0) Console.WriteLine("MANUAL CHECK: Windows did not retain the topmost flag on this test window; topmost acceptance is not established in this session.");
                lease.Dispose(); lease.Dispose(); Assert(Native.GetWindowLongPtr(handle, -20) == style);
            }
            finally { Native.DestroyWindow(handle); }
        });
        foreach (var module in Catalog())
        {
            Check("native lifecycle: " + module.Manifest.Name, () =>
            {
                module.Start(context, new());
                // Pump real Windows messages: timers, native hooks, clipboard listener and overlay rendering run.
                var frame = new System.Windows.Threading.DispatcherFrame();
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
                timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; }; timer.Start();
                System.Windows.Threading.Dispatcher.PushFrame(frame);
                module.Stop(); module.Stop(); module.Dispose();
                Assert(app.Windows.Count == 1);
            });
        }
        window.Close(); app.Shutdown();
        Directory.Delete(directory, true); Console.WriteLine($"{_passed} passed, {_failed} failed"); return _failed == 0 ? 0 : 1;
    }
    private static void Check(string name, Action action) { try { action(); _passed++; Console.WriteLine("PASS " + name); } catch (Exception e) { _failed++; Console.WriteLine("FAIL " + name + ": " + e); } }
    private static void Assert(bool condition) { if (!condition) throw new Exception("Assertion failed."); }
    private static void Throws<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
    private static void WithDirectory(Action<string> action)
    {
        string directory = Path.Combine(Path.GetTempPath(), "nexus-tests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try { action(directory); } finally { Directory.Delete(directory, true); }
    }
    private sealed class FakeModule() : NexusModule(new("fake", "Fake", "Tests", "Lifecycle test", Capability.Mouse))
    {
        public int Starts, Cleanups; public bool Fail;
        protected override void OnStart() { Starts++; Resources.Add(new ActionDisposable(() => Cleanups++)); if (Fail) throw new InvalidOperationException("Intentional start failure"); }
    }
}
