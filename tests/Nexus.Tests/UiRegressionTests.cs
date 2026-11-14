using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Nexus.Core;

namespace Nexus.Tests;

internal static class UiRegressionTests
{
    internal static void Run(Action<string, Action> check, string directory)
    {
        using var hub = new Nexus.MainWindow(Path.Combine(directory, "ui"), new Diagnostics(directory), true);
        hub.Show();
        void Page(string name) { typeof(Nexus.MainWindow).GetMethod("Navigate", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(hub, [name]); hub.UpdateLayout(); }
        var manager = (ModuleManager)typeof(Nexus.MainWindow).GetField("_manager", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(hub)!;
        check("CursorBlur card offers enable and uninstall without Configure", () =>
        {
            Page("All modules"); var name = Descendants<TextBlock>(hub).Single(t => t.Text == "CursorBlur"); var card = (StackPanel)name.Parent;
            var buttons = Descendants<Button>(card).Select(b => b.Content?.ToString()).ToArray();
            Assert(buttons.Contains("Enable") && buttons.Contains("Uninstall feature") && !buttons.Contains("Configure"));
            Assert(Descendants<Button>(hub).Count(b => b.Content?.ToString() == "Configure") == 12);
        });
        check("editing options does not apply them until Apply changes", () =>
        {
            var entry = manager.Entries.Single(e => e.Manifest.Id == "scroll-flow");
            typeof(Nexus.MainWindow).GetMethod("FeaturePage", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(hub, [entry]); hub.UpdateLayout();
            double saved = manager.State(entry.Manifest.Id).Options.Sensitivity;
            var slider = Descendants<Slider>(hub).First(); slider.Value = 1.8;
            Assert(manager.State(entry.Manifest.Id).Options.Sensitivity == saved);
            Descendants<Button>(hub).Single(b => b.Content?.ToString() == "Apply changes").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert(Math.Abs(manager.State(entry.Manifest.Id).Options.Sensitivity - 1.8) < .001);
        });
        check("update controls and official source are present in the settings hub", () =>
        {
            Page("Settings & updates");
            Assert(Descendants<Button>(hub).Any(b => b.Content?.ToString() == "Check for updates"));
            Assert(Descendants<TextBlock>(hub).Any(t => t.Text.Contains("CursorBlur uses its original native engine")));
            Assert(manager.State("cursor-blur").Options is not null);
        });
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject element) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
        {
            var child = VisualTreeHelper.GetChild(element, i); if (child is T value) yield return value;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
    private static void Assert(bool condition) { if (!condition) throw new Exception("UI regression assertion failed."); }
}
