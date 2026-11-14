namespace Nexus.Core;

public interface INexusModule : IDisposable
{
    Manifest Manifest { get; }
    IReadOnlyList<Option> Schema { get; }
    bool SupportsConfiguration => true;
    string? RuntimeError => null;
    void Start(ModuleContext context, ModuleOptions options);
    void Stop();
    System.Windows.FrameworkElement? CreatePanel(ModuleOptions options) => null;
}
public sealed record ModuleContext(InputService Input, HotkeyManager Hotkeys, Diagnostics Diagnostics, Func<bool> ReducedMotion, Func<Capability, bool>? Granted = null);
public abstract class NexusModule(Manifest manifest, params Option[] schema) : INexusModule
{
    public Manifest Manifest { get; } = manifest;
    public IReadOnlyList<Option> Schema { get; } = schema;
    public virtual bool SupportsConfiguration => true;
    public virtual string? RuntimeError => null;
    protected ModuleContext Context { get; private set; } = null!;
    protected ModuleOptions Options { get; private set; } = null!;
    protected List<IDisposable> Resources { get; } = [];
    public void Start(ModuleContext context, ModuleOptions options)
    {
        Stop(); Context = context; Options = options; options.Validate();
        try { OnStart(); } catch { Stop(); throw; }
    }
    protected abstract void OnStart();
    public virtual void Stop()
    {
        foreach (var resource in Resources.AsEnumerable().Reverse()) { try { resource.Dispose(); } catch { /* Continue cleanup of remaining resources. */ } }
        Resources.Clear();
    }
    public virtual System.Windows.FrameworkElement? CreatePanel(ModuleOptions options) => null;
    public void Dispose() { Stop(); GC.SuppressFinalize(this); }
}
public sealed class ModuleEntry(INexusModule module)
{
    public INexusModule Module { get; } = module;
    public Manifest Manifest => Module.Manifest;
    public ModuleStatus Status { get; set; }
    public string Error { get; set; } = "";
}
public sealed class ModuleManager(AppConfig config, ModuleContext context, Action save) : IDisposable
{
    public List<ModuleEntry> Entries { get; } = [];
    public event Action? Changed;
    public void Register(INexusModule module)
    {
        if (module.Manifest.ApiVersion != 1) throw new ArgumentException("Incompatible module API version.");
        if (Entries.Any(x => x.Manifest.Id == module.Manifest.Id)) throw new ArgumentException("Duplicate module id.");
        config.Modules.TryAdd(module.Manifest.Id, new());
        Entries.Add(new(module)); Refresh(module.Manifest.Id);
    }
    public ModuleState State(string id) => config.Modules[id];
    public void SetEnabled(string id, bool enabled)
    {
        var state = State(id);
        if (!state.Installed && enabled) throw new InvalidOperationException("Install the feature first.");
        state.Enabled = enabled; Refresh(id); save(); Changed?.Invoke();
    }
    public void Install(string id, bool installed)
    {
        var entry = Entries.Single(x => x.Manifest.Id == id); entry.Module.Stop();
        config.Modules[id] = new ModuleState { Installed = installed };
        Refresh(id); save(); Changed?.Invoke();
    }
    public void Refresh(string id)
    {
        var entry = Entries.Single(x => x.Manifest.Id == id); var state = State(id);
        entry.Module.Stop(); entry.Error = "";
        state.Options.Validate();
        foreach (var option in entry.Module.Schema)
        {
            var property = typeof(ModuleOptions).GetProperty(option.Key)!;
            if (option.Choices is { } choices && !choices.Contains((string)property.GetValue(state.Options)!)) property.SetValue(state.Options, choices[0]);
            else if (!option.IsText && !option.IsToggle && option.Choices is null) property.SetValue(state.Options, Math.Clamp((double)property.GetValue(state.Options)!, option.Min, option.Max));
        }
        if (!state.Installed) entry.Status = ModuleStatus.Available;
        else if (!state.Enabled) entry.Status = ModuleStatus.Disabled;
        else if ((config.Grants & entry.Manifest.Permissions) != entry.Manifest.Permissions)
        {
            entry.Status = ModuleStatus.Blocked; entry.Error = "Grant the required capabilities on the Permissions page.";
        }
        else
        {
            try { entry.Module.Start(context, state.Options); entry.Status = ModuleStatus.Running; }
            catch (Exception e) { entry.Module.Stop(); entry.Status = ModuleStatus.Faulted; entry.Error = e.Message; context.Diagnostics.Log(entry.Manifest.Id, e); }
        }
    }
    public void RefreshAll() { foreach (var e in Entries) Refresh(e.Manifest.Id); save(); Changed?.Invoke(); }
    public void CheckHealth()
    {
        bool changed = false;
        foreach (var entry in Entries.Where(e => e.Status == ModuleStatus.Running))
        {
            string? error;
            try { error = entry.Module.RuntimeError; } catch (Exception exception) { error = "Could not query the engine: " + exception.Message; }
            if (error is null) continue;
            entry.Module.Stop(); entry.Status = ModuleStatus.Faulted; entry.Error = error;
            context.Diagnostics.Log(entry.Manifest.Id, new InvalidOperationException(error)); changed = true;
        }
        if (changed) Changed?.Invoke();
    }
    public void PauseAll() { foreach (var e in Entries) { e.Module.Stop(); State(e.Manifest.Id).Enabled = false; e.Status = State(e.Manifest.Id).Installed ? ModuleStatus.Disabled : ModuleStatus.Available; } save(); Changed?.Invoke(); }
    public void Dispose() { foreach (var e in Entries) e.Module.Dispose(); GC.SuppressFinalize(this); }
}
