using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using Nexus.Core;

namespace Nexus;

public partial class App : System.Windows.Application
{
    private Mutex? _mutex;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Contains("--uninstall-worker")) { Installer.UninstallWorker(e.Args); Shutdown(); return; }
        if (e.Args.Contains("--install") || Path.GetFileNameWithoutExtension(Environment.ProcessPath!).EndsWith("Setup", StringComparison.OrdinalIgnoreCase)) { Installer.ShowInstall(); Shutdown(); return; }
        if (e.Args.Contains("--uninstall")) { Installer.Uninstall(); Shutdown(); return; }
        _mutex = new Mutex(true, "Local\\Nexus.Desktop.User." + Environment.UserName, out bool created);
        if (!created) { MessageBox.Show("Nexus is already running. Open it from the notification area.", "Nexus"); Shutdown(); return; }
        string data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nexus");
        if (e.Args.Contains("--smoke")) data = Path.Combine(e.Args.SkipWhile(x => x != "--smoke").Skip(1).FirstOrDefault() ?? Path.GetTempPath(), "smoke-settings");
        var diagnostics = new Diagnostics(data);
        DispatcherUnhandledException += (_, args) => { diagnostics.Log("ui", args.Exception); args.Handled = true; MessageBox.Show(args.Exception.Message, "Nexus could not complete that action"); };
        AppDomain.CurrentDomain.UnhandledException += (_, args) => diagnostics.Log("crash", args.ExceptionObject as Exception ?? new Exception("Unknown crash"));
        try
        {
            var window = new MainWindow(data, diagnostics); MainWindow = window; window.Show();
            if (e.Args.Contains("--smoke"))
            {
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    try
                    {
                        string directory = e.Args.SkipWhile(x => x != "--smoke").Skip(1).FirstOrDefault() ?? data; Directory.CreateDirectory(directory);
                        window.SaveScreenGallery(directory);
                        File.WriteAllText(Path.Combine(directory, "smoke-result.json"), System.Text.Json.JsonSerializer.Serialize(new { started = true, modules = window.ModuleCount, nativeWpf = true }));
                        window.Exit();
                    }
                    catch (Exception exception) { diagnostics.Log("smoke", exception); window.Exit(); Environment.ExitCode = 1; }
                };
                timer.Start();
            }
        }
        catch (Exception exception) { diagnostics.Log("startup", exception); MessageBox.Show(exception.Message, "Nexus startup failed"); Shutdown(1); }
    }
    protected override void OnExit(ExitEventArgs e) { _mutex?.Dispose(); base.OnExit(e); }
}
