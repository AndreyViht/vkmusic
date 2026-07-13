using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using VK_UI3.Services;
using WinRT.Interop;
using MusicX.Core.Models;
using VK_UI3.VKs;
using Windows.Graphics;

namespace VK_UI3.Views
{
    public sealed partial class OverlayWindow : Window
    {
        private AppWindow m_AppWindow;
        public ObservableCollection<Audio> NextTracks { get; set; } = new ObservableCollection<Audio>();

        public OverlayWindow()
        {
            this.InitializeComponent();

            // Set up transparent backdrop
            this.SystemBackdrop = new DesktopAcrylicBackdrop();
            this.ExtendsContentIntoTitleBar = true;

            // Configure AppWindow for Topmost and frameless
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
            m_AppWindow = AppWindow.GetFromWindowId(wndId);
            
            var presenter = m_AppWindow.Presenter as OverlappedPresenter;
            if (presenter != null)
            {
                presenter.IsAlwaysOnTop = true;
                presenter.HasBorder = false;
                presenter.HasTitleBar = false;
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
                m_AppWindow.Resize(new SizeInt32(60, 60)); // initial size
            }

            NextTracksList.ItemsSource = NextTracks;

            // Subscribe to media events
            MediaPlayerService.AudioPlayedChangeEvent += MediaPlayerService_onTrackChange;
            if (MediaPlayerService.MediaPlayer?.PlaybackSession != null)
            {
                MediaPlayerService.MediaPlayer.PlaybackSession.PlaybackStateChanged += PlaybackSession_PlaybackStateChanged;
            }

            this.Closed += OverlayWindow_Closed;

            UpdateUI();
        }

        private void OverlayWindow_Closed(object sender, WindowEventArgs args)
        {
            MediaPlayerService.AudioPlayedChangeEvent -= MediaPlayerService_onTrackChange;
            if (MediaPlayerService.MediaPlayer?.PlaybackSession != null)
            {
                MediaPlayerService.MediaPlayer.PlaybackSession.PlaybackStateChanged -= PlaybackSession_PlaybackStateChanged;
            }
        }

        private void PlaybackSession_PlaybackStateChanged(Windows.Media.Playback.MediaPlaybackSession sender, object args)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                bool isPlaying = sender.PlaybackState == Windows.Media.Playback.MediaPlaybackState.Playing;
                PlayPauseIcon.Glyph = isPlaying ? "&#xE769;" : "&#xE768;";
            });
        }

        private void MediaPlayerService_onTrackChange(object sender, EventArgs e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                UpdateUI();
            });
        }

        private void UpdateUI()
        {
            if (MediaPlayerService.PlayingTrack != null)
            {
                var track = MediaPlayerService.PlayingTrack.audio;
                CurrentTrackTitle.Text = track.Title;
                CurrentTrackArtist.Text = track.Artist;
                
                if (track.Album?.Thumb?.Photo300 != null)
                {
                    CurrentTrackImage.Source = new BitmapImage(new Uri(track.Album.Thumb.Photo300));
                }
                else
                {
                    CurrentTrackImage.Source = null;
                }

                bool isPlaying = MediaPlayerService.MediaPlayer?.PlaybackSession?.PlaybackState == Windows.Media.Playback.MediaPlaybackState.Playing;
                PlayPauseIcon.Glyph = isPlaying ? "&#xE769;" : "&#xE768;";

                // Update next 5 tracks
                NextTracks.Clear();
                if (MediaPlayerService.iVKGetAudio != null && MediaPlayerService.iVKGetAudio.listAudio != null)
                {
                    var list = MediaPlayerService.iVKGetAudio.listAudio;
                    var currentIndex = list.IndexOf(MediaPlayerService.PlayingTrack);
                    if (currentIndex >= 0 && currentIndex < list.Count - 1)
                    {
                        var nextTracks = list.Skip(currentIndex + 1).Take(5);
                        foreach (var t in nextTracks)
                        {
                            NextTracks.Add(t.audio);
                        }
                    }
                }
            }
        }

        private void ToggleExpand_Click(object sender, RoutedEventArgs e)
        {
            if (CollapsedButton.Visibility == Visibility.Visible)
            {
                CollapsedButton.Visibility = Visibility.Collapsed;
                ExpandedPanel.Visibility = Visibility.Visible;
                m_AppWindow.Resize(new SizeInt32(320, 420));
                UpdateUI();
            }
            else
            {
                CollapsedButton.Visibility = Visibility.Visible;
                ExpandedPanel.Visibility = Visibility.Collapsed;
                m_AppWindow.Resize(new SizeInt32(60, 60));
            }
        }

        private void PlayPause_Click(object sender, RoutedEventArgs e)
        {
            var session = MediaPlayerService.MediaPlayer?.PlaybackSession;
            if (session != null)
            {
                if (session.PlaybackState == Windows.Media.Playback.MediaPlaybackState.Playing)
                    MediaPlayerService.MediaPlayer.Pause();
                else
                    MediaPlayerService.MediaPlayer.Play();
            }
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            // Reflection since method might be private or we need to enqueue it
            typeof(MediaPlayerService).GetMethod("PlayNextTrack", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.Invoke(null, null);
        }

        private void Prev_Click(object sender, RoutedEventArgs e)
        {
            typeof(MediaPlayerService).GetMethod("HandlePreviousTrack", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.Invoke(null, null);
        }

        // Native dragging mechanism
        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var properties = e.GetCurrentPoint(RootGrid).Properties;
            if (properties.IsLeftButtonPressed)
            {
                IntPtr hWnd = WindowNative.GetWindowHandle(this);
                ReleaseCapture();
                SendMessage(hWnd, 0xA1, (IntPtr)0x2 /* HTCAPTION */, IntPtr.Zero);
            }
        }
    }
}
