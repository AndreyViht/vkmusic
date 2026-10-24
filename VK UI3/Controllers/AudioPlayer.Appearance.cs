using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using VK_UI3.Services;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace VK_UI3.Controllers;
public sealed partial class AudioPlayer
{
    private Storyboard shimmer;
    private void InitializeProgressAppearance()
    {
        AppThemeService.ThemeChanged -= PlayerThemeChanged;
        AppThemeService.ThemeChanged += PlayerThemeChanged;
        MediaPlayerService.MediaPlayer.CurrentStateChanged -= ProgressPlaybackChanged;
        MediaPlayerService.MediaPlayer.CurrentStateChanged += ProgressPlaybackChanged;
        // MainView can be cached and loaded again after reauthorization.
        MediaPlayerService.oniVKUpdate -= AudioPlayer_oniVKUpdate;
        MediaPlayerService.oniVKUpdate += AudioPlayer_oniVKUpdate;
        MediaPlayerService.AudioPlayedChangeEvent -= AudioPlayer_AudioPlayedChange;
        MediaPlayerService.AudioPlayedChangeEvent += AudioPlayer_AudioPlayedChange;
        MediaPlayerService.VolumeChanged -= MediaPlayerService_VolumeChanged;
        MediaPlayerService.VolumeChanged += MediaPlayerService_VolumeChanged;
        MediaPlayerService.PositionChanged -= MediaPlayerService_PositionChanged;
        MediaPlayerService.PositionChanged += MediaPlayerService_PositionChanged;
        MediaPlayerService.MediaPlayer.CurrentStateChanged -= MediaPlayer_CurrentStateChanged;
        MediaPlayerService.MediaPlayer.CurrentStateChanged += MediaPlayer_CurrentStateChanged;
        UpdateProgressAppearance();
    }
    private void StopProgressAppearance()
    {
        shimmer?.Stop(); progressWidthAnimation?.Stop();
        AppThemeService.ThemeChanged -= PlayerThemeChanged;
        MediaPlayerService.oniVKUpdate -= AudioPlayer_oniVKUpdate;
        MediaPlayerService.MediaPlayer.CurrentStateChanged -= ProgressPlaybackChanged;
    }
    private void PlayerThemeChanged(object sender, EventArgs e) => UpdateProgressAppearance();
    private void ProgressPlaybackChanged(Windows.Media.Playback.MediaPlayer sender, object args) => DispatcherQueue.TryEnqueue(UpdateProgressAppearance);
    private static Color Blend(Color a, Color b, double weight) => Color.FromArgb(255,
        (byte)(a.R * (1 - weight) + b.R * weight), (byte)(a.G * (1 - weight) + b.G * weight), (byte)(a.B * (1 - weight) + b.B * weight));
    private void UpdateProgressAppearance()
    {
        shimmer?.Stop();
        var accent = AppThemeService.CurrentAccentColor;
        var blue = SurfaceTheme.Color("7296C1"); var teal = SurfaceTheme.Color("62A3A4");
        ProgressStart.Color = Blend(accent, blue, .60);
        ProgressMiddle.Color = Blend(accent, SurfaceTheme.Color("A294C2"), .65);
        ProgressEnd.Color = Blend(accent, teal, .65);
        RedRectangle.Opacity = ActualTheme == ElementTheme.Light ? .14 : .20;
        if (!IsLoaded || !new UISettings().AnimationsEnabled || new AccessibilitySettings().HighContrast ||
            MediaPlayerService.MediaPlayer.CurrentState != Windows.Media.Playback.MediaPlayerState.Playing) return;
        shimmer = new Storyboard();
        void Animate(Microsoft.UI.Xaml.Media.GradientStop stop, Color target, int seconds)
        {
            var animation = new ColorAnimation { To = target, Duration = TimeSpan.FromSeconds(seconds), AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EnableDependentAnimation = true };
            Storyboard.SetTarget(animation, stop); Storyboard.SetTargetProperty(animation, "Color"); shimmer.Children.Add(animation);
        }
        Animate(ProgressStart, Blend(accent, teal, .65), 9);
        Animate(ProgressMiddle, Blend(accent, blue, .70), 12);
        Animate(ProgressEnd, Blend(accent, blue, .65), 10);
        shimmer.Begin();
    }
}
