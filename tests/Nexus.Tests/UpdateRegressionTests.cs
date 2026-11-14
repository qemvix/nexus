using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Nexus.Core;

namespace Nexus.Tests;

internal static class UpdateRegressionTests
{
    internal static void Run(Action<string, Action> check)
    {
        check("verified updates are cached without another network request", () => InDirectory(async dir =>
        {
            byte[] bytes = Encoding.UTF8.GetBytes("Nexus release test payload"); var release = Release(bytes);
            int requests = 0;
            using var client = new HttpClient(new Handler(_ => { requests++; return new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) }; }));
            string path = await UpdateService.DownloadUsingClient(release, dir, client);
            Assert(File.ReadAllBytes(path).SequenceEqual(bytes)); Assert(await UpdateService.DownloadUsingClient(release, dir, client) == path && requests == 1);
        }));
        check("bad update hashes discard partials and preserve other cached files", () => InDirectory(async dir =>
        {
            string previous = Path.Combine(dir, "previous.exe"); File.WriteAllText(previous, "previous working release");
            using var client = Client(Encoding.UTF8.GetBytes("tampered"));
            await Throws<CryptographicException>(() => UpdateService.DownloadUsingClient(Release(Encoding.UTF8.GetBytes("trusted")), dir, client));
            Assert(Directory.GetFiles(dir).SequenceEqual(new[] { previous }));
        }));
        check("cancelled update transfers leave no partial executable", () => InDirectory(async dir =>
        {
            using var cancel = new CancellationTokenSource(); byte[] bytes = new byte[200_000];
            using var client = new HttpClient(new Handler(_ => new(HttpStatusCode.OK) { Content = new StreamContent(new CancellingStream(bytes, cancel)) }));
            await Throws<OperationCanceledException>(() => UpdateService.DownloadUsingClient(Release(bytes), dir, client, cancel.Token));
            Assert(Directory.GetFiles(dir).Length == 0);
        }));
        check("oversized updates and HTTPS downgrades are rejected", () => InDirectory(async dir =>
        {
            byte[] bytes = [1, 2, 3];
            using var large = new HttpClient(new Handler(_ => { var content = new ByteArrayContent(bytes); content.Headers.ContentLength = 500_000_001; return new(HttpStatusCode.OK) { Content = content }; }));
            await Throws<InvalidDataException>(() => UpdateService.DownloadUsingClient(Release(bytes), dir, large));
            using var insecure = new HttpClient(new Handler(_ => new(HttpStatusCode.OK) { RequestMessage = new(HttpMethod.Get, "http://invalid.example/update.exe"), Content = new ByteArrayContent(bytes) }));
            await Throws<InvalidDataException>(() => UpdateService.DownloadUsingClient(Release(bytes), dir, insecure));
            Assert(Directory.GetFiles(dir).Length == 0);
        }));
        check("signed malformed release metadata is rejected", () =>
        {
            using var rsa = RSA.Create(2048);
            foreach (var release in new[] { new ReleaseInfo("bad", "https://example.com/a.exe", new string('A', 64)), new("0.2.1", "http://example.com/a.exe", new string('A', 64)), new("0.2.1", "https://example.com/a.exe", null!) })
            {
                string payload = JsonSerializer.Serialize(release); string signature = Convert.ToBase64String(rsa.SignData(Encoding.UTF8.GetBytes(payload), HashAlgorithmName.SHA256, RSASignaturePadding.Pss));
                bool rejected = false; try { UpdateService.Verify(JsonSerializer.Serialize(new SignedRelease(payload, signature)), rsa.ExportSubjectPublicKeyInfoPem()); } catch (InvalidDataException) { rejected = true; }
                Assert(rejected);
            }
        });
        check("official update defaults include the publisher trust anchor", () =>
        {
            var config = new AppConfig(); Assert(new Uri(config.UpdateUrl).Scheme == "https" && !config.AutoCheckUpdates && config.Grants == Capability.None);
            using var rsa = RSA.Create(); rsa.ImportFromPem(config.UpdatePublicKey); Assert(rsa.KeySize >= 3072);
            Assert(NexusRelease.Current > new Version(0, 2, 0));
        });
    }
    private static ReleaseInfo Release(byte[] bytes) => new("0.2.1", "https://example.com/Nexus.exe", Convert.ToHexString(SHA256.HashData(bytes)));
    private static HttpClient Client(byte[] bytes) => new(new Handler(_ => new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) }));
    private static void InDirectory(Func<string, Task> action)
    {
        string dir = Path.Combine(Path.GetTempPath(), "nexus-update-tests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try { action(dir).GetAwaiter().GetResult(); } finally { Directory.Delete(dir, true); }
    }
    private static async Task Throws<T>(Func<Task> action) where T : Exception { try { await action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
    private static void Assert(bool condition) { if (!condition) throw new Exception("Update regression assertion failed."); }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(respond(request));
    }
    private sealed class CancellingStream(byte[] bytes, CancellationTokenSource cancellation) : MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var result = base.ReadAsync(buffer[..Math.Min(buffer.Length, 100)], cancellationToken); cancellation.Cancel(); return result;
        }
    }
}
