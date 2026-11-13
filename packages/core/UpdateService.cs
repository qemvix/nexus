using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Nexus.Core;

public sealed record ReleaseInfo(string Version, string Url, string Sha256);
public sealed record SignedRelease(string Payload, string Signature);
/// <summary>Updates are opt-in. A signed manifest authenticates the version, download URL and binary digest.</summary>
public sealed class UpdateService
{
    public static ReleaseInfo Verify(string json, string publicKey)
    {
        var envelope = JsonSerializer.Deserialize<SignedRelease>(json) ?? throw new InvalidDataException("Invalid release envelope.");
        using var rsa = RSA.Create(); rsa.ImportFromPem(publicKey);
        if (!rsa.VerifyData(Encoding.UTF8.GetBytes(envelope.Payload), Convert.FromBase64String(envelope.Signature), HashAlgorithmName.SHA256, RSASignaturePadding.Pss)) throw new CryptographicException("Release signature is invalid.");
        var info = JsonSerializer.Deserialize<ReleaseInfo>(envelope.Payload) ?? throw new InvalidDataException("Invalid release information.");
        if (!System.Version.TryParse(info.Version, out _) || !Uri.TryCreate(info.Url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !System.Text.RegularExpressions.Regex.IsMatch(info.Sha256, "^[a-fA-F0-9]{64}$")) throw new InvalidDataException("Invalid release metadata.");
        return info;
    }
    public static async Task<ReleaseInfo> Check(string url, string publicKey, CancellationToken token = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https") throw new InvalidOperationException("Configure an HTTPS update manifest URL and a trusted RSA public key first.");
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token); response.EnsureSuccessStatusCode();
        using var stream = await response.Content.ReadAsStreamAsync(token); using var memory = new MemoryStream();
        await CopyBounded(stream, memory, 64_000, token);
        return Verify(Encoding.UTF8.GetString(memory.ToArray()), publicKey);
    }
    public static async Task<string> Download(ReleaseInfo release, string directory, CancellationToken token = default)
    {
        if (!Uri.TryCreate(release.Url, UriKind.Absolute, out var uri) || uri.Scheme != "https") throw new InvalidDataException("HTTPS is required.");
        Directory.CreateDirectory(directory); string path = Path.Combine(directory, "Nexus-update.exe");
        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token); response.EnsureSuccessStatusCode();
        using var source = await response.Content.ReadAsStreamAsync(token);
        try
        {
            await using (var target = File.Create(path)) await CopyBounded(source, target, 500_000_000, token);
            await using var file = File.OpenRead(path); string digest = Convert.ToHexString(await SHA256.HashDataAsync(file, token));
            if (!string.Equals(digest, release.Sha256, StringComparison.OrdinalIgnoreCase)) throw new CryptographicException("Update hash mismatch.");
        }
        catch { File.Delete(path); throw; }
        return path;
    }
    private static async Task CopyBounded(Stream source, Stream target, long limit, CancellationToken token)
    {
        byte[] buffer = new byte[81920]; long total = 0; int count;
        while ((count = await source.ReadAsync(buffer, token)) > 0)
        {
            total += count; if (total > limit) throw new InvalidDataException("Download exceeds size limit.");
            await target.WriteAsync(buffer.AsMemory(0, count), token);
        }
    }
}
