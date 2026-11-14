using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Nexus.Core;

public sealed record ReleaseInfo(string Version, string Url, string Sha256);
public sealed record SignedRelease(string Payload, string Signature);
public sealed record DownloadProgress(long Received, long? Total)
{
    public string Description => Total is > 0 ? $"Downloading update… {Received * 100 / Total}% ({Received / 1_000_000} / {Total / 1_000_000} MB)" : $"Downloading update… {Received / 1_000_000} MB";
}

/// <summary>RSA-PSS authenticates the manifest; bounded HTTPS downloads are published only after SHA-256 verification.</summary>
public sealed class UpdateService
{
    public static ReleaseInfo Verify(string json, string publicKey)
    {
        if (json.Length > 64_000) throw new InvalidDataException("Update manifest exceeds its size limit.");
        var envelope = JsonSerializer.Deserialize<SignedRelease>(json) ?? throw new InvalidDataException("Invalid release envelope.");
        if (string.IsNullOrEmpty(envelope.Payload) || string.IsNullOrEmpty(envelope.Signature)) throw new InvalidDataException("The release signature or payload is missing.");
        using var rsa = RSA.Create(); rsa.ImportFromPem(publicKey);
        if (!rsa.VerifyData(Encoding.UTF8.GetBytes(envelope.Payload), Convert.FromBase64String(envelope.Signature), HashAlgorithmName.SHA256, RSASignaturePadding.Pss)) throw new CryptographicException("Release signature is invalid.");
        var info = JsonSerializer.Deserialize<ReleaseInfo>(envelope.Payload) ?? throw new InvalidDataException("Invalid release information.");
        Validate(info); return info;
    }
    private static void Validate(ReleaseInfo release)
    {
        if (!Version.TryParse(release.Version, out _) || !Uri.TryCreate(release.Url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo) || !System.Text.RegularExpressions.Regex.IsMatch(release.Sha256 ?? "", "^[a-fA-F0-9]{64}$")) throw new InvalidDataException("Invalid release metadata.");
    }
    public static async Task<ReleaseInfo> Check(string url, string publicKey, CancellationToken token = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https") throw new InvalidOperationException("An HTTPS update manifest URL is required.");
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token); response.EnsureSuccessStatusCode(); EnsureHttps(response);
        using var stream = await response.Content.ReadAsStreamAsync(token); using var memory = new MemoryStream();
        await CopyBounded(stream, memory, 64_000, token);
        return Verify(Encoding.UTF8.GetString(memory.ToArray()), publicKey);
    }
    public static async Task<string> Download(ReleaseInfo release, string directory, CancellationToken token = default, IProgress<DownloadProgress>? progress = null)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        return await DownloadUsingClient(release, directory, client, token, progress);
    }
    internal static async Task<string> DownloadUsingClient(ReleaseInfo release, string directory, HttpClient client, CancellationToken token = default, IProgress<DownloadProgress>? progress = null)
    {
        Validate(release); token.ThrowIfCancellationRequested();
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"Nexus-update-{Version.Parse(release.Version)}-{release.Sha256[..12].ToLowerInvariant()}.exe");
        if (File.Exists(path) && await Matches(path, release.Sha256, token)) return path;
        string partial = path + "." + Guid.NewGuid().ToString("N") + ".partial";
        try
        {
            using var response = await client.GetAsync(release.Url, HttpCompletionOption.ResponseHeadersRead, token); response.EnsureSuccessStatusCode(); EnsureHttps(response);
            long? total = response.Content.Headers.ContentLength;
            if (total > 500_000_000) throw new InvalidDataException("Update exceeds its size limit.");
            using var source = await response.Content.ReadAsStreamAsync(token);
            await using (var target = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true)) await CopyBounded(source, target, 500_000_000, token, progress, total);
            if (!await Matches(partial, release.Sha256, token)) throw new CryptographicException("Update hash mismatch. The file has been discarded.");
            token.ThrowIfCancellationRequested(); File.Move(partial, path, true); return path;
        }
        finally { if (File.Exists(partial)) File.Delete(partial); }
    }
    private static async Task<bool> Matches(string path, string hash, CancellationToken token)
    {
        await using var file = File.OpenRead(path);
        return string.Equals(Convert.ToHexString(await SHA256.HashDataAsync(file, token)), hash, StringComparison.OrdinalIgnoreCase);
    }
    private static void EnsureHttps(HttpResponseMessage response)
    {
        if (response.RequestMessage?.RequestUri is { Scheme: not "https" }) throw new InvalidDataException("Update requests cannot redirect to an insecure connection.");
    }
    private static async Task CopyBounded(Stream source, Stream target, long limit, CancellationToken token, IProgress<DownloadProgress>? progress = null, long? length = null)
    {
        byte[] buffer = new byte[81920]; long total = 0, reported = -1;
        while (true)
        {
            int count = await source.ReadAsync(buffer, token); if (count == 0) break;
            total += count; if (total > limit) throw new InvalidDataException("Download exceeds size limit.");
            await target.WriteAsync(buffer.AsMemory(0, count), token);
            long bucket = total / 1_000_000;
            if (bucket != reported) { progress?.Report(new(total, length)); reported = bucket; }
        }
        progress?.Report(new(total, length));
    }
}
