using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Runtime.InteropServices;
using VK_UI3.Services;
using VK_UI3.Services.Player;
using Windows.Graphics;

namespace VK_UI3.Views;

public sealed partial class MusicIslandWindow : Window
{
    private readonly MainWindow owner;
    private readonly IntPtr handle;
    private readonly DispatcherTimer update = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private readonly DispatcherTimer collapse = new() { Interval = TimeSpan.FromMilliseconds(650) };
    private bool shown, syncing, expanded;
    private string imageUrl;
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int ellipseWidth, int ellipseHeight);
    [DllImport("user32.dll")] private static extern int SetWindowRgn(IntPtr window, IntPtr region, bool redraw);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr region);

    public MusicIslandWindow(MainWindow owner)
    {
        this.owner = owner;
        InitializeComponent();
        handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false); presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false; presenter.IsMaximizable = false; presenter.IsMinimizable = false;
        }
        AppWindow.IsShownInSwitchers = false;
        update.Tick += UpdateTick;
        collapse.Tick += CollapseTick;
        Closed += (_, _) => { update.Stop(); collapse.Stop(); update.Tick -= UpdateTick; collapse.Tick -= CollapseTick; };
    }
    public void ShowIsland()
    {
        if (!shown)
        {
            shown = true; expanded = false; Details.Visibility = Visibility.Collapsed;
            UpdateTrack(); PositionIsland(); AppWindow.Show(false); update.Start();
        }
        else PositionIsland();
    }
    public void HideIsland()
    {
        if (!shown) return;
        shown = false; update.Stop(); collapse.Stop(); AppWindow.Hide();
    }
    private void PositionIsland()
    {
        var display = DisplayArea.GetFromWindowId(owner.AppWindow.Id, DisplayAreaFallback.Primary);
        var area = display.WorkArea;
        var scale = Math.Max(1, GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(owner)) / 96d);
        int width = Math.Min((int)(420 * scale), Math.Max(260, area.Width - 24));
        int height = (int)((expanded ? 164 : 70) * scale);
        var target = new RectInt32(area.X + (area.Width - width) / 2, area.Y + (int)(12 * scale), width, height);
        if (AppWindow.Position.X != target.X || AppWindow.Position.Y != target.Y || AppWindow.Size.Width != width || AppWindow.Size.Height != height)
        {
            AppWindow.MoveAndResize(target);
            int radius = (int)(56 * scale);
            var region = CreateRoundRectRgn(0, 0, width + 1, height + 1, radius, radius);
            if (region != IntPtr.Zero && SetWindowRgn(handle, region, true) == 0) DeleteObject(region);
        }
    }
    private void IslandEntered(object sender, PointerRoutedEventArgs e)
    {
        collapse.Stop(); expanded = true; Details.Visibility = Visibility.Visible; PositionIsland();
    }
    private void IslandExited(object sender, PointerRoutedEventArgs e) => collapse.Start();
    private void CollapseTick(object sender, object e)
    {
        collapse.Stop();
        if (Seek.FocusState == FocusState.Keyboard || Volume.FocusState == FocusState.Keyboard || Seek.PointerCaptures.Count != 0 || Volume.PointerCaptures.Count != 0) return;
        expanded = false; Details.Visibility = Visibility.Collapsed; PositionIsland();
    }
    private void UpdateTick(object sender, object e) => UpdateTrack();
    private static string Time(TimeSpan value) => value.TotalHours >= 1 ? value.ToString(@"h\:mm\:ss") : value.ToString(@"m\:ss");
    private void UpdateTrack()
    {
        var audio = MediaPlayerService.PlayingTrack?.audio;
        TrackTitle.Text = audio?.Title ?? "Музыка"; TrackArtist.Text = audio?.Artist ?? "";
        Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(TrackTitle, TrackTitle.Text);
        var url = audio?.Album?.Thumb?.Photo300 ?? audio?.Album?.Thumb?.Photo270 ?? audio?.Album?.Thumb?.Photo135;
        if (imageUrl != url)
        {
            imageUrl = url;
            Cover.Source = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? new BitmapImage(uri) : null;
        }
        var session = MediaPlayerService.MediaPlayer.PlaybackSession;
        PlayIcon.Glyph = session.PlaybackState == Windows.Media.Playback.MediaPlaybackState.Playing ? "\uE769" : "\uE768";
        syncing = true;
        try
        {
            Seek.Maximum = Math.Max(1, session.NaturalDuration.TotalSeconds);
            Seek.IsEnabled = session.CanSeek && session.NaturalDuration.TotalSeconds > 0;
            if (Seek.PointerCaptures.Count == 0) Seek.Value = Math.Clamp(session.Position.TotalSeconds, 0, Seek.Maximum);
            Volume.Value = MediaPlayerService.Volume * 100;
            Elapsed.Text = Time(session.Position); Duration.Text = Time(session.NaturalDuration);
        }
        finally { syncing = false; }
    }
    private async void PlayPauseClicked(object sender, RoutedEventArgs e)
    {
        try { await MediaPlayerService.TogglePlayPause(); UpdateTrack(); }
        catch (Exception error) { DiagnosticsService.Record("island-playback", error); }
    }
    private void PreviousClicked(object sender, RoutedEventArgs e) => MediaPlayerService.ExecuteAction(PlayerAction.PreviousTrack);
    private void NextClicked(object sender, RoutedEventArgs e) => MediaPlayerService.PlayNextTrack();
    private void OpenClicked(object sender, RoutedEventArgs e) { HideIsland(); owner.ShowWindowAgain(); owner.Activate(); }
    private void SeekChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!syncing && MediaPlayerService.MediaPlayer.PlaybackSession.CanSeek)
            MediaPlayerService.MediaPlayer.PlaybackSession.Position = TimeSpan.FromSeconds(e.NewValue);
    }
    private void VolumeChanged(object sender, RangeBaseValueChangedEventArgs e) { if (!syncing) MediaPlayerService.Volume = e.NewValue / 100; }
}
