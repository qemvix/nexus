using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Nexus.Core;

namespace Nexus;

/// <summary>One self-contained executable doubles as a per-user installer. Settings live separately and survive uninstall.</summary>
public static class Installer
{
    public static string InstallDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Nexus");
    private static string Shortcut => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "Nexus.lnk");
    private const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Nexus";
    public static void ShowInstall()
    {
        var panel = new StackPanel(); var brand = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 20) }; brand.Children.Add(Ui.NexusMark()); brand.Children.Add(Ui.Text("Nexus", 28)); panel.Children.Add(brand);
        panel.Children.Add(Ui.Text("Install the complete Nexus hub and module library for your Windows account.\n\nNo administrator access, startup entry or network connection is required. All features start disabled.\n\nLocation: " + InstallDirectory, 14, "#B4BAD1"));
        var status = Ui.Text("", 13, "#72E3CA"); panel.Children.Add(status);
        var window = new Window { Title = "Install Nexus", Width = 560, Height = 600, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterScreen, Content = new Border { Padding = new Thickness(32), Child = panel } };
        window.Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri("pack://application:,,,/Nexus;component/Assets/nexus.ico"));
        var install = Ui.Button("Install Nexus", () =>
        {
            try
            {
                var others = Process.GetProcessesByName("Nexus").Where(p => p.Id != Environment.ProcessId).ToArray();
                try { if (others.Any()) throw new InvalidOperationException("Exit the running Nexus app from its tray menu, then try again."); }
                finally { foreach (var p in others) p.Dispose(); }
                InstallFiles(Environment.ProcessPath!, InstallDirectory, true); status.Text = "Installed. Nexus is ready.";
                Process.Start(new ProcessStartInfo(Path.Combine(InstallDirectory, "Nexus.exe")) { UseShellExecute = true }); window.Close();
            }
            catch (Exception e) { status.Text = "Installation could not finish: " + e.Message; }
        });
        if (File.Exists(Path.Combine(InstallDirectory, "Nexus.exe"))) { install.Content = "Update Nexus"; panel.Children.Add(Ui.Text("An existing installation will be updated in place. Your settings are kept.", 13, "#72E3CA")); }
        panel.Children.Add(Ui.Button("View Nexus license", LicenseNotice.Show)); panel.Children.Add(install); panel.Children.Add(Ui.Button("Cancel", window.Close)); window.ShowDialog();
    }
    public static void InstallFiles(string source, string destination, bool register)
    {
        Directory.CreateDirectory(destination); string target = Path.Combine(destination, "Nexus.exe");
        if (!string.Equals(Path.GetFullPath(source), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
        {
            string stage = target + ".new";
            try { File.Copy(source, stage, true); File.Move(stage, target, true); }
            finally { if (File.Exists(stage)) File.Delete(stage); }
        }
        if (!register) return;
        Directory.CreateDirectory(Path.GetDirectoryName(Shortcut)!);
        var type = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("Windows shortcut service unavailable.");
        dynamic shell = Activator.CreateInstance(type)!; dynamic shortcut = shell.CreateShortcut(Shortcut);
        try { shortcut.TargetPath = target; shortcut.WorkingDirectory = destination; shortcut.Description = "Nexus desktop utilities"; shortcut.Save(); }
        finally { Marshal.FinalReleaseComObject(shortcut); Marshal.FinalReleaseComObject(shell); }
        using var key = Registry.CurrentUser.CreateSubKey(RegistryPath);
        key.SetValue("DisplayName", "Nexus"); key.SetValue("DisplayVersion", NexusRelease.Version); key.SetValue("Publisher", "qemvix"); key.SetValue("InstallLocation", destination);
        key.SetValue("UninstallString", "\"" + target + "\" --uninstall"); key.SetValue("DisplayIcon", target); key.SetValue("NoModify", 1); key.SetValue("NoRepair", 1);
        key.SetValue("EstimatedSize", (int)(new FileInfo(target).Length / 1024));
    }
    public static void ApplyUpdateWorker(string[] arguments)
    {
        try
        {
            int index = Array.IndexOf(arguments, "--apply-update");
            if (index < 0 || index + 1 >= arguments.Length || !int.TryParse(arguments[index + 1], out int pid) || pid <= 0 || pid == Environment.ProcessId) throw new InvalidDataException("Invalid update parent.");
            try { using var parent = Process.GetProcessById(pid); if (!parent.WaitForExit(30_000)) throw new IOException("Nexus is still closing. Exit it from the tray and run the installer again."); } catch (ArgumentException) { }
            var others = Process.GetProcessesByName("Nexus").Where(p => p.Id != Environment.ProcessId).ToArray();
            try { if (others.Any()) throw new IOException("Another Nexus instance is running. Close it before applying the update."); }
            finally { foreach (var process in others) process.Dispose(); }
            InstallFiles(Environment.ProcessPath!, InstallDirectory, true);
            Process.Start(new ProcessStartInfo(Path.Combine(InstallDirectory, "Nexus.exe")) { UseShellExecute = true });
        }
        catch (Exception exception) { MessageBox.Show("The update could not be applied: " + exception.Message + "\nYour settings were kept. You can run the downloaded installer again.", "Nexus update"); Environment.ExitCode = 1; }
    }
    public static void Uninstall()
    {
        if (MessageBox.Show("Remove Nexus from this account? Local settings are kept. Exit any other running Nexus instance first.", "Uninstall Nexus", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        string worker = Path.Combine(Path.GetTempPath(), "Nexus-uninstall-" + Guid.NewGuid().ToString("N") + ".exe");
        File.Copy(Environment.ProcessPath!, worker);
        Process.Start(new ProcessStartInfo(worker, "--uninstall-worker " + Environment.ProcessId) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden });
    }
    public static void UninstallWorker(string[] arguments)
    {
        int index = Array.IndexOf(arguments, "--uninstall-worker");
        if (index < 0 || index + 1 >= arguments.Length || !int.TryParse(arguments[index + 1], out int pid)) return;
        try { using var parent = Process.GetProcessById(pid); if (!parent.WaitForExit(15_000)) return; } catch (ArgumentException) { }
        try
        {
            // Delete only our fixed, known files. Never recursively delete a caller-supplied path.
            string binary = Path.Combine(InstallDirectory, "Nexus.exe");
            if (File.Exists(binary)) File.Delete(binary);
            if (File.Exists(Shortcut)) File.Delete(Shortcut);
            Registry.CurrentUser.DeleteSubKeyTree(RegistryPath, false);
            if (Directory.Exists(InstallDirectory) && !Directory.EnumerateFileSystemEntries(InstallDirectory).Any()) Directory.Delete(InstallDirectory);
            MessageBox.Show("Nexus was removed. Your settings remain in %LOCALAPPDATA%\\Nexus. The temporary uninstaller is cleaned by Windows temporary-file maintenance.", "Nexus");
        }
        catch (Exception e) { MessageBox.Show("Uninstall could not finish: " + e.Message, "Nexus"); }
    }
}
