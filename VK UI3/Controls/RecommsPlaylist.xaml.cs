using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using VK_UI3.Controllers;
using VK_UI3.DB;
using VK_UI3.Helpers;
using VK_UI3.Helpers.Animations;
using VK_UI3.Views;
using VK_UI3.Views.ModalsPages;
using VK_UI3.VKs;
using VK_UI3.VKs.IVK;
using VkNet.Model.Attachments;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VK_UI3.Controls
{
    public sealed partial class RecommsPlaylist : UserControl
    {
        public RecommsPlaylist()
        {
            this.InitializeComponent();

            AnimationsChangeFontIcon = new AnimationsChangeFontIcon(PlayPause, this.DispatcherQueue);
            titleAnim = new AnimationsChangeText(Title, this.DispatcherQueue);
            descrAnim = new AnimationsChangeText(Subtitle, this.DispatcherQueue);

            this.Unloaded += PlaylistControl_Unloaded;
            this.Loaded += PlaylistControl_Loaded;

            DataContextChanged += RecommsPlaylist_DataContextChanged;


        }

        private void PlaylistControl_Loaded(object sender, RoutedEventArgs e)
        {
            Services.MediaPlayerService.oniVKUpdate += AudioPlayer_oniVKUpdate;
        }

        private void PlaylistControl_Unloaded(object sender, RoutedEventArgs e)
        {
            Services.MediaPlayerService.oniVKUpdate -= AudioPlayer_oniVKUpdate;

            this.Unloaded -= PlaylistControl_Unloaded;
            this.Loaded -= PlaylistControl_Loaded;

            DataContextChanged -= RecommsPlaylist_DataContextChanged;

        }

        private void AudioPlayer_oniVKUpdate(object sender, EventArgs e)
        {
            updatePlayState();
        }

        private void updatePlayState(bool prin = false, bool pause = false)
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                string icon = "\uE768"; // default icon

                if (prin)
                {
                    icon = pause ? "\uE768" : "\uE769";
                }
                else if (isThisPlayList_Now_Play && VK_UI3.Services.MediaPlayerService.MediaPlayer.PlaybackSession.PlaybackState != Windows.Media.Playback.MediaPlaybackState.Paused)
                {
                    icon = "\uE769";
                }

                if (!isThisPlayList_Now_Play)
                {
                    if (!entered)
                    {
                        FadeInAnimationGridPlayIcon.Pause();
                        FadeOutAnimationGridPlayIcon.Begin();
                    }
                }
                else
                {
                    FadeOutAnimationGridPlayIcon.Pause();
                    FadeInAnimationGridPlayIcon.Begin();
                }

                AnimationsChangeFontIcon.ChangeFontIconWithAnimation(icon);
            });
        }

        public bool isThisPlayList_Now_Play
        {
            get
            {
                if (_PlayList == null || VK_UI3.Services.MediaPlayerService.iVKGetAudio == null)
                    return false;

                if (!(VK_UI3.Services.MediaPlayerService.iVKGetAudio is PlayListVK))
                    return false;

                var playlist = (VK_UI3.Services.MediaPlayerService.iVKGetAudio as PlayListVK).playlist;

                if (playlist == _PlayList)
                    return true;

                if (playlist.Id == _PlayList.Id && playlist.OwnerId == _PlayList.OwnerId && playlist.AccessKey == _PlayList.AccessKey)
                    return true;

                if (playlist.Original != null)
                {
                    if (playlist.Id == playlist.Original.PlaylistId && playlist.OwnerId == playlist.Original.OwnerId && playlist.AccessKey == playlist.Original.AccessKey)
                        return true;
                }

                return false;
            }
        }


        AnimationsChangeText titleAnim { get; set; }
        AnimationsChangeText descrAnim { get; set; }





        public static readonly DependencyProperty PlaylistItemsProperty =
         DependencyProperty.Register(
            "_PlaylistItems",
             typeof(ObservableRangeCollection<AudioPlaylist>),
             typeof(PlaylistControl),
             new PropertyMetadata(default(ObservableRangeCollection<AudioPlaylist>)));

        public DependencyProperty PlaylistItems => PlaylistItemsProperty;
        public ObservableRangeCollection<AudioPlaylist> _PlaylistItems
        {
            get { return (ObservableRangeCollection<AudioPlaylist>)GetValue(PlaylistItemsProperty); }
            set { SetValue(PlaylistItemsProperty, value); }
        }



        public async void AddRemove_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_PlayList.IsFollowing)
                {
                    if (_PlayList.Original != null)
                    {
                        await VK.vkService.AddPlaylistAsync(_PlayList.Original.PlaylistId, _PlayList.Original.OwnerId, _PlayList.Original.AccessKey);
                        var js = await VK.vkService.AddPlaylistAsync(_PlayList.Original.PlaylistId, _PlayList.Original.OwnerId, _PlayList.Original.AccessKey);
                        _PlayList = await VK.api.Audio.GetPlaylistByIdAsync((long)js["owner_id"], (long)js["playlist_id"]);
                    }
                    else
                    {
                        await VK.vkService.AddPlaylistAsync(_PlayList.Id, _PlayList.OwnerId, _PlayList.AccessKey);
                        var js = await VK.vkService.AddPlaylistAsync(_PlayList.Id, _PlayList.OwnerId, _PlayList.AccessKey);
                        _PlayList = await VK.api.Audio.GetPlaylistByIdAsync((long)js["owner_id"], (long)js["playlist_id"]);
                    }
                }
                else
                {
                    if (_PlayList.Follower != null)
                    {
                        await VK.vkService.DeletePlaylistAsync(_PlayList.Follower.PlaylistId, _PlayList.Follower.OwnerId);
                    }
                    else
                    {
                        await VK.vkService.DeletePlaylistAsync(_PlayList.Id, _PlayList.OwnerId);
                    }
                    if (_PlayList.Original != null)
                        _PlayList = await VK.api.Audio.GetPlaylistByIdAsync(_PlayList.Original.OwnerId, _PlayList.Original.PlaylistId);
                }
            }
            catch (Exception ex)
            {
                // �������� ����� ��������� ����������, ��������, �����������
                Console.WriteLine(ex.Message);
            }
            updateAddedBTN();
        }


        AnimationsChangeFontIcon AnimationsChangeFontIcon;
        AnimationsChangeImage animationsChangeImage;

        AudioPlaylist _PlayList { get; set; }


        private void RecommsPlaylist_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (DataContext == null) return;
            _PlayList = (DataContext as AudioPlaylist);
            FadeOutAnimationGrid.Completed += FadeOutAnimationGrid_Completed;
            update();
        }

        private void FadeOutAnimationGrid_Completed(object sender, object e)
        {
            update();
        }

        private static Windows.UI.Color ParseHexColor(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Windows.UI.Color.FromArgb(255, 30, 30, 30);
            hex = hex.TrimStart('#');
            if (hex.Length == 6)
            {
                byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                return Windows.UI.Color.FromArgb(255, r, g, b);
            }
            if (hex.Length == 8)
            {
                byte a = Convert.ToByte(hex.Substring(0, 2), 16);
                byte r = Convert.ToByte(hex.Substring(2, 2), 16);
                byte g = Convert.ToByte(hex.Substring(4, 2), 16);
                byte b = Convert.ToByte(hex.Substring(6, 2), 16);
                return Windows.UI.Color.FromArgb(a, r, g, b);
            }
            return Windows.UI.Color.FromArgb(255, 30, 30, 30);
        }

        private static Windows.UI.Color DarkenColor(Windows.UI.Color color, float factor)
        {
            return Windows.UI.Color.FromArgb(
                color.A,
                (byte)Math.Clamp((int)(color.R * factor), 0, 255),
                (byte)Math.Clamp((int)(color.G * factor), 0, 255),
                (byte)Math.Clamp((int)(color.B * factor), 0, 255)
            );
        }

        private void SetFallbackGradient(AudioPlaylist playlist)
        {
            var brush = new Microsoft.UI.Xaml.Media.LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(0.85, 1)
            };

            var t = playlist?.Title?.ToLower() ?? "";
            if (t.Contains("для вас"))
            {
                brush.GradientStops.Add(new Microsoft.UI.Xaml.Media.GradientStop { Color = Windows.UI.Color.FromArgb(255, 0, 119, 255), Offset = 0.0 });
                brush.GradientStops.Add(new Microsoft.UI.Xaml.Media.GradientStop { Color = Windows.UI.Color.FromArgb(255, 0, 90, 224), Offset = 0.5 });
                brush.GradientStops.Add(new Microsoft.UI.Xaml.Media.GradientStop { Color = Windows.UI.Color.FromArgb(255, 0, 56, 168), Offset = 1.0 });
            }
            else if (t.Contains("открыти"))
            {
                brush.GradientStops.Add(new Microsoft.UI.Xaml.Media.GradientStop { Color = Windows.UI.Color.FromArgb(255, 224, 36, 195), Offset = 0.0 });
                brush.GradientStops.Add(new Microsoft.UI.Xaml.Media.GradientStop { Color = Windows.UI.Color.FromArgb(255, 184, 21, 176), Offset = 0.5 });
                brush.GradientStops.Add(new Microsoft.UI.Xaml.Media.GradientStop { Color = Windows.UI.Color.FromArgb(255, 107, 10, 128), Offset = 1.0 });
            }
            else if (t.Contains("новинк"))
            {
                brush.GradientStops.Add(new Microsoft.UI.Xaml.Media.GradientStop { Color = Windows.UI.Color.FromArgb(255, 0, 206, 209), Offset = 0.0 });
                brush.GradientStops.Add(new Microsoft.UI.Xaml.Media.GradientStop { Color = Windows.UI.Color.FromArgb(255, 0, 154, 166), Offset = 0.5 });
                brush.GradientStops.Add(new Microsoft.UI.Xaml.Media.GradientStop { Color = Windows.UI.Color.FromArgb(255, 0, 79, 94), Offset = 1.0 });
            }
            else if (t.Contains("дня"))
            {
                brush.GradientStops.Add(new Microsoft.UI.Xaml.Media.GradientStop { Color = Windows.UI.Color.FromArgb(255, 142, 45, 226), Offset = 0.0 });
                brush.GradientStops.Add(new Microsoft.UI.Xaml.Media.GradientStop { Color = Windows.UI.Color.FromArgb(255, 101, 31, 255), Offset = 0.5 });
                brush.GradientStops.Add(new Microsoft.UI.Xaml.Media.GradientStop { Color = Windows.UI.Color.FromArgb(255, 49, 27, 146), Offset = 1.0 });
            }
            else if (!string.IsNullOrEmpty(playlist?.MainColor))
            {
                var main = ParseHexColor(playlist.MainColor);
                brush.GradientStops.Add(new Microsoft.UI.Xaml.Media.GradientStop { Color = main, Offset = 0.0 });
                brush.GradientStops.Add(new Microsoft.UI.Xaml.Media.GradientStop { Color = DarkenColor(main, 0.75f), Offset = 0.5 });
                brush.GradientStops.Add(new Microsoft.UI.Xaml.Media.GradientStop { Color = DarkenColor(main, 0.45f), Offset = 1.0 });
            }
            else
            {
                int hash = Math.Abs(t.GetHashCode());
                byte r1 = (byte)(40 + (hash % 160));
                byte g1 = (byte)(40 + ((hash / 3) % 160));
                byte b1 = (byte)(110 + ((hash / 7) % 140));
                var c = Windows.UI.Color.FromArgb(255, r1, g1, b1);
                brush.GradientStops.Add(new Microsoft.UI.Xaml.Media.GradientStop { Color = c, Offset = 0.0 });
                brush.GradientStops.Add(new Microsoft.UI.Xaml.Media.GradientStop { Color = DarkenColor(c, 0.5f), Offset = 1.0 });
            }

            CardBackgroundGrid.Background = brush;
        }

        public void update()
        {
            if (_PlayList == null) return;

            AnimationsChangeFontIcon.ChangeFontIconWithAnimation("\uF5B0");

            if (!string.IsNullOrWhiteSpace(_PlayList.Subtitle))
            {
                descrAnim.ChangeTextWithAnimation(_PlayList.Subtitle);
            }
            else if (!string.IsNullOrWhiteSpace(_PlayList.Description))
            {
                descrAnim.ChangeTextWithAnimation(_PlayList.Description);
            }
            else if (_PlayList.MainArtists != null && _PlayList.MainArtists.Count != 0)
            {
                descrAnim.ChangeTextWithAnimation(string.Join(", ", _PlayList.MainArtists.Select(a => a.Name)));
            }
            else
            {
                descrAnim.ChangeTextWithAnimation(string.Empty);
            }

            titleAnim.ChangeTextWithAnimation(_PlayList.Title ?? string.Empty);

            SetFallbackGradient(_PlayList);

            bool isAlgorithmic = (_PlayList.Id < 0) ||
                                 (_PlayList.Title != null && (
                                     _PlayList.Title.ToLower().Contains("для вас") ||
                                     _PlayList.Title.ToLower().Contains("открытия") ||
                                     _PlayList.Title.ToLower().Contains("новинки") ||
                                     _PlayList.Title.ToLower().Contains("плейлист дня")));

            string coverUrl = _PlayList.VerticalCover ?? _PlayList.Cover;

            if (!string.IsNullOrEmpty(coverUrl))
            {
                GridThumbs.Stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill;
                GridThumbs.AddImagesToGrid(coverUrl);
            }
            else if (!isAlgorithmic && _PlayList.Thumbs != null && _PlayList.Thumbs.Count > 0)
            {
                List<string> list = new List<string>();
                foreach (var item in _PlayList.Thumbs)
                {
                    string photo = item.Photo600 ?? item.Photo1200 ?? item.Photo300 ?? item.Photo34 ?? item.Photo270 ?? item.Photo135 ?? item.Photo68;
                    if (!string.IsNullOrEmpty(photo))
                        list.Add(photo);
                }
                if (list.Count > 0)
                    GridThumbs.AddImagesToGrid(list);
            }

            updateAddedBTN();
            updatePlayState();

            FadeInAnimationGrid.Begin();
        }

        private void updateAddedBTN()
        {
            this.DispatcherQueue.TryEnqueue(async () =>
            {
                bool canEdit = _PlayList.Permissions.Edit;
                bool canDelete = _PlayList.Permissions.Delete;

                editAlbum.Visibility = canEdit ? Visibility.Visible : Visibility.Collapsed;
                DeleteAlbum.Visibility = canDelete ? Visibility.Visible : Visibility.Collapsed;

                AddRemove.Visibility = Visibility.Visible;

                if (_PlayList.Permissions.Edit)
                    AddRemove.Visibility = Visibility.Collapsed;

                if (!_PlayList.IsFollowing && !_PlayList.Permissions.Follow)
                    AddRemove.Visibility = Visibility.Collapsed;

                if (AddRemove.Visibility != Visibility.Collapsed)
                {
                    if (!_PlayList.IsFollowing && _PlayList.OwnerId != AccountsDB.activeAccount.id)
                    {
                        AddRemove.Text = "Добавить к себе";
                        AddRemove.Icon = new SymbolIcon(Symbol.Add);
                    }
                    else
                    {
                        AddRemove.Text = "Удалить";
                        AddRemove.Icon = new SymbolIcon(Symbol.Delete);
                    }
                }
            });
        }

        bool entered;
        private void UserControl_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            entered = true;
            this.Scale = new System.Numerics.Vector3(1.02f, 1.02f, 1.0f);
            this.Translation = new System.Numerics.Vector3(0, -2, 10);
            HoverBorder.Opacity = 0.9;
            FadeOutAnimationGridPlayIcon.Pause();
            FadeInAnimationGridPlayIcon.Begin();
        }

        private void UserControl_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            entered = false;
            this.Scale = new System.Numerics.Vector3(1.0f, 1.0f, 1.0f);
            this.Translation = new System.Numerics.Vector3(0, 0, 8);
            HoverBorder.Opacity = 0.4;
            if (!isThisPlayList_Now_Play)
            {
                FadeInAnimationGridPlayIcon.Pause();
                FadeOutAnimationGridPlayIcon.Begin();
            }
        }

        private void UserControl_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (e.GetCurrentPoint(sender as UIElement).Properties.IsLeftButtonPressed)
            {
                this.Scale = new System.Numerics.Vector3(0.98f, 0.98f, 1.0f);
                if (iVKGetAudio == null)
                    MainView.OpenPlayList(_PlayList);
                else
                {
                    MainView.OpenPlayList(iVKGetAudio);
                }
            }
        }

        private void UserControl_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            this.Scale = entered ? new System.Numerics.Vector3(1.02f, 1.02f, 1.0f) : new System.Numerics.Vector3(1.0f, 1.0f, 1.0f);
        }

        private void UserControl_PointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            this.Scale = new System.Numerics.Vector3(1.0f, 1.0f, 1.0f);
        }

        IVKGetAudio iVKGetAudio = null;

        private void Button_Click(object sender, RoutedEventArgs e)
        {

            if (isThisPlayList_Now_Play)
            {
                if (VK_UI3.Services.MediaPlayerService.MediaPlayer.PlaybackSession.PlaybackState != Windows.Media.Playback.MediaPlaybackState.Paused)
                {
                    VK_UI3.Services.MediaPlayerService.MediaPlayer.Pause();
                    updatePlayState(true, true);
                }
                else
                {
                    VK_UI3.Services.MediaPlayerService.MediaPlayer.Play();
                    updatePlayState(true, false);
                }

            }
            else
            {
                AnimationsChangeFontIcon.ChangeFontIconWithAnimation("\uE916");
                Task.Run(async () =>
                {
                    iVKGetAudio = new PlayListVK(_PlayList, this.DispatcherQueue);
                    iVKGetAudio.PlayThis();
                });
            }
        }

        private void editAlbum_Click(object sender, RoutedEventArgs e)
        {
            ContentDialog dialog = new CustomDialog();

            dialog.XamlRoot = this.XamlRoot;
            dialog.Transitions = new TransitionCollection
                {
                    new PopupThemeTransition()
                };

            var a = new CreatePlayList(_PlayList);

            dialog.Content = a;
            dialog.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
            a.cancelPressed += ((s, e) =>
            {
                if (s != null && s is AudioPlaylist)
                {
                    _PlayList = s as AudioPlaylist;


                    FadeOutAnimationGrid.Begin();
                }

                try
                {
                    if (dialog != null)
                        dialog.Hide();
                    dialog = null;
                }
                catch
                {

                }
            });

            dialog.ShowAsync();
        }

        private async void DeleteAlbum_Click(object sender, RoutedEventArgs e)
        {
            this.DispatcherQueue.TryEnqueue(async () =>
            {
                TempPlayLists.TempPlayLists.updateNextRequest = true;
                await VK.api.Audio.DeletePlaylistAsync(_PlayList.OwnerId, _PlayList.Id);
                var index = _PlaylistItems.IndexOf(_PlayList);
                if (index != -1)
                    _PlaylistItems.RemoveAt(index);
            });
        }
    }
}
