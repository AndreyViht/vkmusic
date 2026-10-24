namespace VK_UI3.Services;
public static class MusicIslandPolicy
{
    public static bool ShouldShow(bool enabled, bool mainWindowHidden, bool hasTrack, bool playingOrPaused) =>
        enabled && mainWindowHidden && hasTrack && playingOrPaused;
}
