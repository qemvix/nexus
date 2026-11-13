using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nexus.Core;

[Flags]
public enum Capability { None = 0, Mouse = 1, Keyboard = 2, Screen = 4, Clipboard = 8, Windows = 16, Network = 32 }
public enum ModuleStatus { Available, Disabled, Running, Blocked, Faulted }
public sealed record Manifest(string Id, string Name, string Category, string Description, Capability Permissions, int ApiVersion = 1, string Version = "0.1.0");
public sealed record Option(string Key, string Label, double Min = 0, double Max = 100, string[]? Choices = null, bool IsText = false, bool IsToggle = false);
public sealed record ScrollProfile(string Process, double Sensitivity = 1, bool Invert = false, double Momentum = 0.75);
public sealed record WindowZone(double X, double Y, double Width, double Height);

/// <summary>Shared, typed feature configuration. Only fields declared by a feature's schema appear in its editor.</summary>
public sealed class ModuleOptions
{
    public double Size { get; set; } = 44;
    public double Intensity { get; set; } = 0.45;
    public double Speed { get; set; } = 1;
    public double Duration { get; set; } = 0.55;
    public double Zoom { get; set; } = 2;
    public double Sensitivity { get; set; } = 1;
    public double Momentum { get; set; } = 0.75;
    public bool Invert { get; set; }
    public bool Sound { get; set; }
    public bool AlwaysOnTop { get; set; } = true;
    public string Mode { get; set; } = "Glow";
    public string Shape { get; set; } = "Circle";
    public string Accent { get; set; } = "#A497FF";
    public string Location { get; set; } = "51.5,-0.12";
    public List<ScrollProfile> ScrollProfiles { get; set; } = [];
    public List<WindowZone> Zones { get; set; } = [new(0, 0, .5, 1), new(.5, 0, .5, 1), new(0, 0, 1, 1), new(.25, .1, .5, .8)];
    public ModuleOptions Clone() => JsonSerializer.Deserialize<ModuleOptions>(JsonSerializer.Serialize(this))!;
    public void Validate()
    {
        Size = Bound(Size, 16, 400, 44); Intensity = Bound(Intensity, 0.05, 1, .45);
        Speed = Bound(Speed, .1, 5, 1); Duration = Bound(Duration, .1, 3, .55);
        Zoom = Bound(Zoom, 1.1, 8, 2); Sensitivity = Bound(Sensitivity, .1, 5, 1);
        Momentum = Bound(Momentum, 0, .95, .75);
        if (Accent is null || !System.Text.RegularExpressions.Regex.IsMatch(Accent, "^#[0-9A-Fa-f]{6}$")) Accent = "#A497FF";
        Mode = (Mode ?? "Glow")[..Math.Min(Mode?.Length ?? 4, 40)];
        Shape = Shape is "Square" ? "Square" : "Circle";
        Location = (Location ?? "51.5,-0.12")[..Math.Min(Location?.Length ?? 10, 100)];
        ScrollProfiles ??= [];
        ScrollProfiles = ScrollProfiles.Take(100).Where(p => p is not null && !string.IsNullOrWhiteSpace(p.Process) && double.IsFinite(p.Sensitivity) && p.Sensitivity is >= .1 and <= 5 && p.Momentum is >= 0 and <= .95).ToList();
        Zones ??= [];
        Zones = Zones.Take(4).Where(z => z is not null && double.IsFinite(z.X) && double.IsFinite(z.Y) && double.IsFinite(z.Width) && double.IsFinite(z.Height) && z.X is >= 0 and < 1 && z.Y is >= 0 and < 1 && z.Width >= .1 && z.Height >= .1 && z.X + z.Width <= 1.001 && z.Y + z.Height <= 1.001).ToList();
    }
    private static double Bound(double value, double min, double max, double fallback) => double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
}

public sealed class ModuleState
{
    public bool Installed { get; set; } = true;
    public bool Enabled { get; set; }
    public ModuleOptions Options { get; set; } = new();
}
public sealed class Profile
{
    public string Name { get; set; } = "Default";
    public Dictionary<string, ModuleState> Modules { get; set; } = [];
}
public sealed class AppConfig
{
    public int SchemaVersion { get; set; } = 1;
    public bool ReducedMotion { get; set; }
    public Capability Grants { get; set; }
    public Dictionary<string, ModuleState> Modules { get; set; } = [];
    public Dictionary<string, string> Hotkeys { get; set; } = [];
    public List<Profile> Profiles { get; set; } = [];
    public string UpdateUrl { get; set; } = "";
    public string UpdatePublicKey { get; set; } = "";
    public bool AutoCheckUpdates { get; set; }
    public DateTimeOffset LastUpdateCheck { get; set; }
    [JsonIgnore] public static JsonSerializerOptions JsonOptions { get; } = new() { WriteIndented = true };
    public void Validate()
    {
        if (SchemaVersion != 1) throw new InvalidDataException("This configuration requires a different Nexus version.");
        Modules ??= []; Hotkeys ??= []; Profiles ??= [];
        if (Modules.Count > 100 || Profiles.Count > 50 || Hotkeys.Count > 100) throw new InvalidDataException("Configuration exceeds limits.");
        foreach (var state in Modules.Values)
        {
            if (state is null) throw new InvalidDataException("Invalid module state.");
            state.Options ??= new(); state.Options.Validate();
            if (!state.Installed) state.Enabled = false;
        }
        foreach (var profile in Profiles)
        {
            if (profile is null || string.IsNullOrWhiteSpace(profile.Name) || profile.Name.Length > 80 || profile.Modules is null || profile.Modules.Count > 100) throw new InvalidDataException("Invalid profile.");
            foreach (var state in profile.Modules.Values)
            {
                if (state is null) throw new InvalidDataException("Invalid profile state.");
                state.Options ??= new(); state.Options.Validate();
                if (!state.Installed) state.Enabled = false;
            }
        }
    }
}
