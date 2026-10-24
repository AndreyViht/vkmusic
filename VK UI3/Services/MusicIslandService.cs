using Microsoft.UI.Xaml;
using System;
using System.Runtime.InteropServices;
using VK_UI3.DB;
using VK_UI3.Views;
using Windows.Media.Playback;

namespace VK_UI3.Services;

public static class MusicIslandService
{
    private static MainWindow owner;
    private static MusicIslandWindow island;
    private static readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    public static bool Enabled => SettingsTable.GetSetting("musicIslandEnabled", "1").settingValue == "1";
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);

    public static void Initialize(MainWindow window)
    {
        if (owner != null) return;
        owner = window;
        timer.Tick += Refresh;
        timer.Start();
        owner.Closed += OwnerClosed;
    }
    private static void OwnerClosed(object sender, WindowEventArgs e) { if (!e.Handled) Shutdown(); }
    public static void SetEnabled(bool enabled)
    {
        SettingsTable.SetSetting("musicIslandEnabled", enabled ? "1" : "0");
        Refresh(null, null);
    }
    private static void Refresh(object sender, object args)
    {
        if (owner == null) return;
        try
        {
            var handle = WinRT.Interop.WindowNative.GetWindowHandle(owner);
            var state = MediaPlayerService.MediaPlayer.PlaybackSession.PlaybackState;
            bool show = MusicIslandPolicy.ShouldShow(Enabled, IsIconic(handle) || !IsWindowVisible(handle),
                MediaPlayerService.PlayingTrack?.audio != null && MediaPlayerService.MediaPlayer.Source != null,
                state is MediaPlaybackState.Playing or MediaPlaybackState.Paused);
            if (show)
            {
                if (island == null)
                {
                    var created = new MusicIslandWindow(owner);
                    created.Closed += (_, _) => { if (ReferenceEquals(island, created)) island = null; };
                    island = created;
                }
                island.ShowIsland();
            }
            else island?.HideIsland();
        }
        catch (Exception error) { DiagnosticsService.Record("music-island", error); island?.HideIsland(); }
    }
    public static void Shutdown()
    {
        timer.Stop(); timer.Tick -= Refresh;
        if (owner != null) owner.Closed -= OwnerClosed;
        island?.Close(); island = null; owner = null;
    }
}
