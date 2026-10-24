using VK_UI3.DB;
namespace VK_UI3.Services;
public static class PlaybackOptions
{
    public static string Filters(string equalizer)
    {
        if (SettingsTable.GetSetting("player.normalize", "0").settingValue != "1") return equalizer ?? "";
        return string.IsNullOrWhiteSpace(equalizer) ? "loudnorm=I=-16:TP=-1.5:LRA=11" : equalizer + ",loudnorm=I=-16:TP=-1.5:LRA=11";
    }
}
