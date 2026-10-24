using Microsoft.UI.Xaml;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using VK_UI3.DB;
using VK_UI3.Helpers;
using VK_UI3.VKs;
using VK_UI3.VKs.IVK;
using VkNet.Model.Attachments;

namespace VK_UI3.Services;

public static class PlaybackStateService
{
    public static ObservableCollection<ExtendedAudio> Pending { get; } = new();
    private static IVKGetAudio resumeSource;
    private static IVKGetAudio queuedSource;
    private static readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(5) };
    public static DateTimeOffset? SleepUntil { get; private set; }
    private static bool restoring;
    private static string Key => "playback.v2." + AccountsDB.activeAccount.id;
    public sealed record SavedTrack(long? Id, long? Owner, string Title, string Artist, string LocalPath);
    public sealed record State(SavedTrack[] Tracks, int Index, double Seconds, double Volume, bool Muted, SavedTrack[] Pending);

    public static void Initialize()
    {
        if (double.TryParse(SettingsTable.GetSetting("player.volume")?.settingValue, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var volume)) MediaPlayerService.Volume = Math.Clamp(volume, 0, 1);
        timer.Tick += Tick;
        timer.Start();
    }
    private static void Tick(object sender, object e)
    {
        if (SleepUntil.HasValue && DateTimeOffset.Now >= SleepUntil)
        {
            MediaPlayerService.StopPlayback(); SleepUntil = null;
        }
        Save();
    }
    public static void Stop() { timer.Stop(); timer.Tick -= Tick; }
    public static void ResetForAccount() { Pending.Clear(); resumeSource = null; queuedSource = null; }
    public static void SetSleepTimer(int minutes) => SleepUntil = minutes > 0 ? DateTimeOffset.Now.AddMinutes(minutes) : null;
    public static void Enqueue(ExtendedAudio track, bool next = false)
    {
        if (track?.audio == null) return;
        if (next) Pending.Insert(0, track); else Pending.Add(track);
        Save();
    }
    public static bool TryPlayQueued()
    {
        if (queuedSource != null && MediaPlayerService.iVKGetAudio != queuedSource) resumeSource = null;
        if (Pending.Count != 0)
        {
            resumeSource ??= MediaPlayerService.iVKGetAudio;
            var item = Pending[0]; Pending.RemoveAt(0);
            var source = new SimpleAudio(MainWindow.mainWindow.DispatcherQueue) { name = "Очередь", itsAll = true, countTracks = 1 };
            source.listAudio.Add(new ExtendedAudio(item.audio, source)); source.currentTrack = 0;
            queuedSource = source; MediaPlayerService.iVKGetAudio = source;
            return true;
        }
        if (resumeSource != null) { MediaPlayerService.iVKGetAudio = resumeSource; resumeSource = null; }
        queuedSource = null;
        return false;
    }
    private static SavedTrack Describe(ExtendedAudio track) => new(track.audio.Id, track.audio.OwnerId,
        track.audio.Title, track.audio.Artist, track.audio.Url?.IsFile == true ? track.audio.Url.LocalPath : null);
    public static void Save()
    {
        if (restoring || SettingsTable.GetSetting("player.remember", "1").settingValue != "1") return;
        try
        {
            SettingsTable.SetSetting("player.volume", MediaPlayerService.Volume.ToString(System.Globalization.CultureInfo.InvariantCulture));
            var source = MediaPlayerService.iVKGetAudio;
            if (source == null || source.listAudio.Count == 0) return;
            var state = new State(source.listAudio.Take(1000).Select(Describe).ToArray(), (int)(source.currentTrack ?? 0),
                MediaPlayerService.MediaPlayer.PlaybackSession.Position.TotalSeconds, MediaPlayerService.Volume,
                MediaPlayerService.IsMuted, Pending.Select(Describe).ToArray());
            SettingsTable.SetSetting(Key, JsonSerializer.Serialize(state));
        }
        catch (Exception error) { DiagnosticsService.Record("save-playback", error); }
    }
    public static void Forget() => SettingsTable.RemoveSetting(Key);
    public static async Task RestoreAsync()
    {
        if (restoring) return;
        restoring = true;
        var accountId = AccountsDB.activeAccount.id;
        try
        {
            var json = SettingsTable.GetSetting(Key)?.settingValue;
            if (string.IsNullOrWhiteSpace(json)) return;
            var state = JsonSerializer.Deserialize<State>(json);
            if (state?.Tracks == null || state.Tracks.Length == 0) return;
            var saved = state.Tracks.Concat(state.Pending ?? Array.Empty<SavedTrack>()).ToArray();
            var ids = saved.Where(t => t.LocalPath == null && t.Id.HasValue && t.Owner.HasValue)
                .Select(t => $"{t.Owner}_{t.Id}").Distinct().ToArray();
            var remote = new System.Collections.Generic.List<Audio>();
            foreach (var batch in ids.Chunk(100))
                remote.AddRange(await VK.api.Audio.GetByIdAsync(batch).WaitAsync(TimeSpan.FromSeconds(25)));
            if (accountId != AccountsDB.activeAccount.id) return;
            Audio Resolve(SavedTrack t) => t.LocalPath != null ? (File.Exists(t.LocalPath)
                ? new Audio { Title = t.Title, Artist = t.Artist, Url = new Uri(t.LocalPath) } : null)
                : remote.FirstOrDefault(a => a.Id == t.Id && a.OwnerId == t.Owner);
            var source = new SimpleAudio(MainWindow.mainWindow.DispatcherQueue) { name = "Продолжить прослушивание", itsAll = true };
            foreach (var track in state.Tracks)
            {
                var audio = Resolve(track); if (audio != null) source.listAudio.Add(new ExtendedAudio(audio, source));
            }
            if (source.listAudio.Count == 0) return;
            source.countTracks = source.listAudio.Count;
            var current = state.Tracks[Math.Clamp(state.Index, 0, state.Tracks.Length - 1)];
            var resolvedCurrent = Resolve(current);
            source.currentTrack = Math.Max(0, source.listAudio.ToList().FindIndex(t => ReferenceEquals(t.audio, resolvedCurrent)));
            Pending.Clear();
            foreach (var item in state.Pending ?? Array.Empty<SavedTrack>())
            {
                var audio = Resolve(item); if (audio != null) Pending.Add(new ExtendedAudio(audio, source));
            }
            MediaPlayerService.Volume = state.Volume; MediaPlayerService.IsMuted = state.Muted;
            await MediaPlayerService.RestorePausedAsync(source, TimeSpan.FromSeconds(Math.Max(0, state.Seconds)));
        }
        catch (Exception error) { DiagnosticsService.Record("restore-playback", error); SessionService.Handle(error); }
        finally { restoring = false; }
    }
}
