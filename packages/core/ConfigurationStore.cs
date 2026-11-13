using System.Text.Json;

namespace Nexus.Core;

/// <summary>Atomic local storage. Never exports clipboard content or permission grants with profiles.</summary>
public sealed class ConfigurationStore(string directory)
{
    public string DirectoryPath { get; } = directory;
    public string RecoveryMessage { get; private set; } = "";
    private string FilePath => Path.Combine(DirectoryPath, "settings.json");
    public AppConfig Load()
    {
        if (!File.Exists(FilePath)) return new();
        try { return Read(FilePath); }
        catch (Exception e) when (e is JsonException or InvalidDataException or IOException or UnauthorizedAccessException)
        {
            RecoveryMessage = "Settings could not be read; a backup or defaults were loaded. " + e.Message;
            try { File.Copy(FilePath, FilePath + ".rejected-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString("N") + ".json"); } catch { /* Recovery remains possible if disk access is restricted. */ }
            try { return Read(FilePath + ".bak"); } catch { return new(); }
        }
    }
    private static AppConfig Read(string path)
    {
        if (new FileInfo(path).Length > 2_000_000) throw new InvalidDataException("Settings file is too large.");
        var config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path)) ?? throw new InvalidDataException("Empty configuration.");
        config.Validate(); return config;
    }
    public void Save(AppConfig config)
    {
        config.Validate(); Directory.CreateDirectory(DirectoryPath);
        string temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(config, AppConfig.JsonOptions));
        if (File.Exists(FilePath)) File.Replace(temporary, FilePath, FilePath + ".bak");
        else File.Move(temporary, FilePath);
    }
    public static Profile Snapshot(string name, AppConfig config) => new() { Name = name.Trim(), Modules = Clone(config.Modules) };
    public static Dictionary<string, ModuleState> Clone(Dictionary<string, ModuleState> source) => JsonSerializer.Deserialize<Dictionary<string, ModuleState>>(JsonSerializer.Serialize(source))!;
    public static Profile Import(string path)
    {
        if (new FileInfo(path).Length > 1_000_000) throw new InvalidDataException("Profile exceeds 1 MB.");
        var profile = JsonSerializer.Deserialize<Profile>(File.ReadAllText(path)) ?? throw new InvalidDataException("Empty profile.");
        new AppConfig { Profiles = [profile] }.Validate(); return profile;
    }
}
