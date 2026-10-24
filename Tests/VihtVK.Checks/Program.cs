using System.Net;
using System.Security.Cryptography;
using SetupLib;
using SetupLib.Services;

var passed = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); passed++; }
Check(UpdatePolicy.ParseVersion("v0.3.10") > UpdatePolicy.ParseVersion("0.3.9.8"), "Numeric version ordering");
Check(UpdatePolicy.ParseVersion("1.2") == UpdatePolicy.ParseVersion("1.2.0.0"), "Equivalent versions");
Check(!UpdatePolicy.TryVersion("1.2-beta", out _), "Exclude prerelease tag");
Check(!UpdatePolicy.TryVersion("broken", out _), "Ignore invalid tag");
Check(UpdatePolicy.MatchesAsset("VK_UI3_x64.zip", "X64", PackageType.ZIP), "Select x64 ZIP");
Check(!UpdatePolicy.MatchesAsset("VK_UI3_x64.zip", "X86", PackageType.ZIP), "Reject wrong architecture");
Check(!UpdatePolicy.MatchesAsset("VK_UI3_x64.zip", "X64", PackageType.MSIX), "Reject wrong format");
Check(UpdatePolicy.MatchesAsset("VK_0.4_ARM64.msix", "ARM64", PackageType.MSIX), "Select ARM64");
var folder = Path.Combine(Path.GetTempPath(), "VihtChecks-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(folder);
try
{
    async Task Download(HttpStatusCode status, HttpContent content, string file)
    {
        var service = new FileDownloadService(() => new HttpClient(new ResponseHandler(status, content)));
        await service.DownloadFileAsync("https://unit-test.invalid/file", file);
    }
    var file = Path.Combine(folder, "payload.bin");
    var payload = new byte[] { 80, 75, 3, 4, 5, 6 };
    await Download(HttpStatusCode.OK, new StreamContent(new NonSeekable(payload)), file);
    Check(File.ReadAllBytes(file).SequenceEqual(payload), "Download without Content-Length");
    var asset = new PackageAsset { Size = payload.Length, Sha256 = Convert.ToHexString(SHA256.HashData(payload)) };
    await InstallationService.VerifyAsync(file, asset); passed++;
    asset.Sha256 = new string('0', 64);
    try { await InstallationService.VerifyAsync(file, asset); throw new Exception("Accepted corrupt file"); }
    catch (InvalidDataException) { passed++; }
    var missing = Path.Combine(folder, "missing.bin");
    try { await Download(HttpStatusCode.NotFound, new StringContent("missing"), missing); throw new Exception("Accepted HTTP 404"); }
    catch (HttpRequestException) { Check(!File.Exists(missing), "Do not save HTTP error"); }
    var html = new StringContent("<!doctype html>"); html.Headers.ContentType = new("text/html");
    try { await Download(HttpStatusCode.OK, html, missing); throw new Exception("Accepted HTML file"); }
    catch (InvalidDataException) { Check(!File.Exists(missing + ".partial"), "Clean partial download"); }
    var script = InstallationService.BuildZipScript(Path.Combine(folder, "test.zip"), Path.Combine(folder, "app's folder"), 0);
    Check(script.Contains("app''s folder") && script.Contains(".previous-") && !script.Contains("Stop-Process") && !script.Contains("Read-Host"), "Safe transactional update script");
}
finally { Directory.Delete(folder, true); }
Console.WriteLine($"Passed: {passed}");

sealed class ResponseHandler(HttpStatusCode status, HttpContent content) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(status) { Content = content });
}
sealed class NonSeekable(byte[] bytes) : Stream
{
    private readonly MemoryStream inner = new(bytes);
    public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default) => inner.ReadAsync(buffer, token);
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
