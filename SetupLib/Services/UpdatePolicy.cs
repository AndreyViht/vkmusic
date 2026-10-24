using System.Text.RegularExpressions;

namespace SetupLib.Services;
public static class UpdatePolicy
{
    public static bool TryVersion(string? text, out Version version)
    {
        version = new Version(0, 0, 0, 0);
        var value = text?.Trim().TrimStart('v', 'V');
        if (value == null || !Regex.IsMatch(value, @"^\d+(\.\d+){1,3}$") || !Version.TryParse(value, out var parsed)) return false;
        version = new Version(parsed.Major, parsed.Minor, Math.Max(0, parsed.Build), Math.Max(0, parsed.Revision));
        return true;
    }
    public static Version ParseVersion(string? text) => TryVersion(text, out var version) ? version : throw new FormatException("Некорректная версия обновления.");
    public static bool MatchesAsset(string name, string architecture, PackageType kind) =>
        name.EndsWith(kind == PackageType.ZIP ? ".zip" : ".msix", StringComparison.OrdinalIgnoreCase) &&
        Regex.IsMatch(name, @"(^|[^a-z0-9])" + Regex.Escape(architecture) + @"([^a-z0-9]|$)", RegexOptions.IgnoreCase);
}
