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
        var panel = new StackPanel(); panel.Children.Add(Ui.Text("◈  nexus", 34)); panel.Children.Add(Ui.Text("Your desktop, refined.", 21));
        panel.Children.Add(Ui.Text("Install the complete Nexus hub and module library for your Windows account.\n\nNo administrator access, startup entry or network connection is required. All features start disabled.\n\nLocation: " + InstallDirectory, 14, "#B4BAD1"));
        var status = Ui.Text("", 13, "#72E3CA"); panel.Children.Add(status);
        var window = new Window { Title = "Install Nexus", Width = 560, Height = 470, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterScreen, Content = new Border { Padding = new Thickness(32), Child = panel } };
        window.Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri("pack://application:,,,/Assets/nexus.ico"));
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
        panel.Children.Add(install); panel.Children.Add(Ui.Button("Cancel", window.Close)); window.ShowDialog();
    }
    public static void InstallFiles(string source, string destination, bool register)
    {
        Directory.CreateDirectory(destination); string target = Path.Combine(destination, "Nexus.exe");
        if (!string.Equals(Path.GetFullPath(source), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
        {
            string stage = target + ".new"; File.Copy(source, stage, true); File.Move(stage, target, true);
        }
        if (!register) return;
        Directory.CreateDirectory(Path.GetDirectoryName(Shortcut)!);
        var type = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("Windows shortcut service unavailable.");
        dynamic shell = Activator.CreateInstance(type)!; dynamic shortcut = shell.CreateShortcut(Shortcut);
        try { shortcut.TargetPath = target; shortcut.WorkingDirectory = destination; shortcut.Description = "Nexus desktop enhancement ecosystem"; shortcut.Save(); }
        finally { Marshal.FinalReleaseComObject(shortcut); Marshal.FinalReleaseComObject(shell); }
        using var key = Registry.CurrentUser.CreateSubKey(RegistryPath);
        key.SetValue("DisplayName", "Nexus"); key.SetValue("DisplayVersion", "0.1.0"); key.SetValue("Publisher", "Nexus contributors"); key.SetValue("InstallLocation", destination);
        key.SetValue("UninstallString", "\"" + target + "\" --uninstall"); key.SetValue("DisplayIcon", target); key.SetValue("NoModify", 1); key.SetValue("NoRepair", 1);
        key.SetValue("EstimatedSize", (int)(new FileInfo(target).Length / 1024));
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
