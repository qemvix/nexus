using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Nexus.Core;

namespace Nexus.Desktop;

public sealed class ClipVault() : NexusModule(new("clip-vault", "ClipVault", "Desktop", "Search and pin a private, memory-only clipboard history with rich-text copy.", Capability.Clipboard))
{
    private sealed record Clip(string Text, string? Rtf) { public bool Pinned { get; set; } }
    private readonly List<Clip> _items = [];
    private HwndSource? _source;
    private Window? _window;
    private Action? _refresh;
    protected override void OnStart()
    {
        _source = new(new HwndSourceParameters("Nexus clipboard") { ParentWindow = -3, Width = 0, Height = 0 }); _source.AddHook(Message);
        if (!Native.AddClipboardFormatListener(_source.Handle)) { _source.Dispose(); _source = null; throw new InvalidOperationException("Clipboard notifications unavailable."); }
        Resources.Add(new ActionDisposable(() => { Native.RemoveClipboardFormatListener(_source.Handle); _source.Dispose(); _source = null; _window?.Close(); _window = null; _items.Clear(); _refresh = null; }));
        Resources.Add(Context.Hotkeys.Register("Ctrl+Alt+V", Show));
    }
    private nint Message(nint hwnd, int message, nint wparam, nint lparam, ref bool handled)
    {
        if (message == 0x031D)
        {
            try
            {
                if (!System.Windows.Clipboard.ContainsText()) return 0;
                string text = System.Windows.Clipboard.GetText();
                if (string.IsNullOrWhiteSpace(text) || text.Length > 32_768) return 0;
                if (_items.Any(i => i.Text == text)) return 0;
                string? rtf = System.Windows.Clipboard.ContainsText(TextDataFormat.Rtf) ? System.Windows.Clipboard.GetText(TextDataFormat.Rtf) : null;
                if (rtf?.Length > 131_072) rtf = null;
                _items.Insert(0, new(text, rtf));
                while (_items.Count > 100)
                {
                    var remove = _items.LastOrDefault(i => !i.Pinned);
                    if (remove is null) { _items.RemoveAt(0); break; }
                    _items.Remove(remove);
                }
                _refresh?.Invoke();
            }
            catch (ExternalException) { /* Another application owns the clipboard. Wait for its next notification. */ }
        }
        return 0;
    }
    public override FrameworkElement? CreatePanel(ModuleOptions options) => Ui.Button("Open ClipVault · Ctrl+Alt+V", Show);
    private void Show()
    {
        if (_source is null) { MessageBox.Show("Enable ClipVault and grant clipboard permission first.", "Nexus"); return; }
        if (_window is not null) { _window.Activate(); return; }
        var panel = new DockPanel(); var top = new StackPanel(); DockPanel.SetDock(top, Dock.Top);
        top.Children.Add(Ui.Text("CLIPVAULT", 22)); top.Children.Add(Ui.Text("Memory only · cleared when disabled or Nexus exits", 12, "#A6ACC5"));
        var search = new TextBox { Margin = new Thickness(0, 8, 0, 10), MinHeight = 32 }; System.Windows.Automation.AutomationProperties.SetName(search, "Search clipboard history");
        top.Children.Add(search); top.Children.Add(Ui.Button("Clear all history", () => { _items.Clear(); _refresh?.Invoke(); }));
        panel.Children.Add(top); var list = new StackPanel(); panel.Children.Add(new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        _refresh = () =>
        {
            list.Children.Clear();
            foreach (var item in _items.Where(i => i.Text.Contains(search.Text, StringComparison.OrdinalIgnoreCase)).OrderByDescending(i => i.Pinned))
            {
                var row = new StackPanel(); row.Children.Add(Ui.Text(item.Text.Length > 250 ? item.Text[..250] + "…" : item.Text));
                var buttons = new WrapPanel();
                buttons.Children.Add(Ui.Button("Copy", () => Copy(item, false)));
                buttons.Children.Add(Ui.Button("Plain text", () => Copy(item, true)));
                buttons.Children.Add(Ui.Button(item.Pinned ? "Unpin" : "Pin", () => { item.Pinned = !item.Pinned; _refresh?.Invoke(); }));
                buttons.Children.Add(Ui.Button("Delete", () => { _items.Remove(item); _refresh?.Invoke(); }));
                row.Children.Add(buttons); list.Children.Add(Ui.Card(row));
            }
            if (list.Children.Count == 0) list.Children.Add(Ui.Text("Copy some text in another app to start your history.", 14, "#A6ACC5"));
        };
        search.TextChanged += (_, _) => _refresh?.Invoke(); _refresh();
        _window = Ui.Floating("ClipVault", panel, 560, 600); _window.Closed += (_, _) => { _window = null; _refresh = null; };
    }
    private static void Copy(Clip clip, bool plain)
    {
        try
        {
            var data = new DataObject(); data.SetText(clip.Text, TextDataFormat.UnicodeText);
            if (!plain && clip.Rtf is not null) data.SetText(clip.Rtf, TextDataFormat.Rtf);
            System.Windows.Clipboard.SetDataObject(data, true);
        }
        catch (ExternalException) { MessageBox.Show("The clipboard is busy. Try again.", "Nexus"); }
    }
}
