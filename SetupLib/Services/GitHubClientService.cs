using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json;
using SetupLib.Interfaces;

namespace SetupLib.Services;

public class GitHubClientService : IGitHubClientService
{
    public async Task<ReleaseInfo> GetLatestReleaseInfo(string owner, string repo, string currentVersion, PackageType? requiredPackage = null)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("VihtMusicVK/1.0");
        using var document = await client.GetFromJsonAsync<JsonDocument>($"https://api.github.com/repos/{owner}/{repo}/releases?per_page=100");
        var architecture = RuntimeInformation.OSArchitecture.ToString();
        var releases = document!.RootElement.EnumerateArray()
            .Where(r => !r.GetProperty("draft").GetBoolean() && !r.GetProperty("prerelease").GetBoolean())
            .Where(r => UpdatePolicy.TryVersion(r.GetProperty("tag_name").GetString(), out _))
            .OrderByDescending(r => UpdatePolicy.ParseVersion(r.GetProperty("tag_name").GetString()));
        foreach (var release in releases)
        {
            var tag = release.GetProperty("tag_name").GetString();
            if (UpdatePolicy.ParseVersion(tag) <= UpdatePolicy.ParseVersion(currentVersion)) continue;
            var info = new ReleaseInfo
            {
                Version = tag!, Name = release.GetProperty("name").GetString() ?? tag!,
                Body = release.GetProperty("body").GetString() ?? "", IsNewVersionAvailable = true
            };
            foreach (var asset in release.GetProperty("assets").EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString() ?? "";
                if (name.EndsWith(".cer", StringComparison.OrdinalIgnoreCase)) info.CertificateUrl = asset.GetProperty("browser_download_url").GetString()!;
                foreach (var kind in new[] { PackageType.ZIP, PackageType.MSIX })
                {
                    if (!UpdatePolicy.MatchesAsset(name, architecture, kind)) continue;
                    info.Assets[kind] = new PackageAsset
                    {
                        Url = asset.GetProperty("browser_download_url").GetString()!, Size = asset.GetProperty("size").GetInt32(), Architecture = architecture,
                        Sha256 = asset.TryGetProperty("digest", out var digest) ? digest.GetString()?.Replace("sha256:", "") : null
                    };
                }
            }
            if (info.Assets.Count != 0 && (requiredPackage == null || info.Assets.ContainsKey(requiredPackage.Value))) return info;
        }
        return new ReleaseInfo { IsNewVersionAvailable = false, Version = currentVersion };
    }
}
