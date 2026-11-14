namespace Nexus.Core;

/// <summary>One build version drives UI, installer metadata and update comparisons.</summary>
public static class NexusRelease
{
    public static Version Current { get; } = new(typeof(NexusRelease).Assembly.GetName().Version!.ToString(3));
    public static string Version => Current.ToString(3);
    public const string ManifestUrl = "https://raw.githubusercontent.com/qemvix/nexus/main/updates/stable.json";
}
