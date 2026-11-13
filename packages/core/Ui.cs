using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Nexus.Core;

public static class Ui
{
    public static FrameworkElement JsonEditor<T>(string label, T value, Action<T> apply)
    {
        var panel = new StackPanel(); panel.Children.Add(Text(label, 13));
        var box = new TextBox { Text = System.Text.Json.JsonSerializer.Serialize(value, AppConfig.JsonOptions), AcceptsReturn = true, MinHeight = 100, MaxHeight = 240, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        System.Windows.Automation.AutomationProperties.SetName(box, label);
        var status = Text("Use Apply JSON, then Apply changes to save.", 12, "#A6ACC5");
        panel.Children.Add(box); panel.Children.Add(Button("Apply JSON", () =>
        {
            try { var parsed = System.Text.Json.JsonSerializer.Deserialize<T>(box.Text) ?? throw new InvalidDataException("Empty value."); apply(parsed); status.Text = "JSON applied. Use Apply changes to save."; }
            catch (Exception e) { status.Text = "Invalid data: " + e.Message; }
        })); panel.Children.Add(status); return panel;
    }
    public static System.Windows.Media.Brush Brush(string color) => OverlayFrame.Brush(color);
    public static TextBlock Text(string text, double size = 14, string color = "#ECEAF7") => new() { Text = text, FontSize = size, Foreground = Brush(color), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) };
    public static Button Button(string text, Action action)
    {
        var button = new Button { Content = text, Margin = new Thickness(0, 3, 8, 3), Padding = new Thickness(14, 9, 14, 9), MinHeight = 36 };
        System.Windows.Automation.AutomationProperties.SetName(button, text); button.Click += (_, _) => action(); return button;
    }
    public static Border Card(UIElement content) => new() { Background = Brush("#171A28"), BorderBrush = Brush("#2C3044"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(20), Margin = new Thickness(0, 0, 0, 14), Child = content };
    public static Window Floating(string title, UIElement content, double width = 310, double height = 240)
    {
        var window = new Window { Title = "Nexus · " + title, Width = width, Height = height, MinWidth = 230, MinHeight = 130, Topmost = true, ShowInTaskbar = false, Background = Brush("#111320"), Foreground = Brush("#ECEAF7"), FontFamily = new FontFamily("Segoe UI"), Content = new Border { Padding = new Thickness(18), Child = content }, WindowStartupLocation = WindowStartupLocation.CenterScreen };
        window.Show(); return window;
    }
    public static void Launch(string uri)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri) { UseShellExecute = true }); }
        catch (Exception e) { MessageBox.Show(e.Message, "Unable to open Windows control", MessageBoxButton.OK, MessageBoxImage.Information); }
    }
    public static void MediaKey(byte key) { Native.keybd_event(key, 0, 0, 0); Native.keybd_event(key, 0, 2, 0); }
}
