using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using MusicX.Core.Models;
using System;
using System.Threading;
using System.Threading.Tasks;
using VK_UI3.DB;
using VK_UI3.Services;
using VK_UI3.Views.Share;
using VK_UI3.VKs;
using VK_UI3.VKs.IVK;
using VkNet.Model;
using VkNet.Model.Attachments;
using VkNet.Model.RequestParams;
using VkNet.Utils;
using static VK_UI3.Views.SectionView;

namespace VK_UI3.Views;

public class WaitParameters
{
    public SectionType sectionType;
    public string SectionID;
    public Section section;
    public IVKGetAudio iVKGetAudio;
    public OpenedPlayList openedPlayList;
    public AudioPlaylist Playlist;
    public object moreParams;
    public MainView mainView;
    public string searchText;
    public string searchFilter;
}

public sealed partial class WaitView : Microsoft.UI.Xaml.Controls.Page, IDisposable
{
    private WaitParameters parameters;
    private CancellationTokenSource loading;
    public WaitView()
    {
        InitializeComponent();
        Loaded += async (_, _) => { MainWindow.onRefreshClicked += Refresh; await LoadAsync(); };
        Unloaded += (_, _) => Dispose();
    }
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        parameters = e.Parameter as WaitParameters;
    }
    private async void Refresh(object sender, EventArgs args)
    {
        if (MainView.mainView?.ContentFramePublic?.frameCurrent?.Frame?.Content == this) await LoadAsync();
    }
    private void Navigate(Type page, object value) => frameSection.Navigate(page, value, new DrillInNavigationTransitionInfo());

    public async Task LoadAsync()
    {
        loading?.Cancel();
        loading?.Dispose();
        loading = new CancellationTokenSource(TimeSpan.FromSeconds(35));
        var token = loading.Token;
        var accountId = AccountsDB.activeAccount.id;
        Navigate(typeof(LoginWindow.waitPage), null);
        try
        {
            if (parameters == null) throw new InvalidOperationException("Не указан раздел.");
            var result = await ResolveAsync(token).WaitAsync(token);
            token.ThrowIfCancellationRequested();
            if (accountId != AccountsDB.activeAccount.id) return;
            Navigate(result.Page, result.Data);
            if (parameters.sectionType == SectionType.Search) MainView.mainView.showSearch();
            else MainView.mainView.hideSearch();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            if (IsLoaded && loading?.Token == token)
                Navigate(typeof(ErrorPage), "Загрузка заняла слишком много времени. Проверьте сеть и нажмите «Повторить».");
        }
        catch (Exception error)
        {
            DiagnosticsService.Record("section", error);
            if (!token.IsCancellationRequested && accountId == AccountsDB.activeAccount.id && !SessionService.Handle(error))
                Navigate(typeof(ErrorPage), "Не удалось загрузить раздел. Проверьте подключение и повторите попытку.");
        }
    }

    private async Task<(Type Page, object Data)> ResolveAsync(CancellationToken token)
    {
        switch (parameters.sectionType)
        {
            case SectionType.MyListAudio:
                parameters.iVKGetAudio = new UserAudio(AccountsDB.activeAccount.id, DispatcherQueue);
                await parameters.iVKGetAudio.WaitForTracksAsync(token);
                return (typeof(PlayListPage), parameters.iVKGetAudio);
            case SectionType.PlayList:
                parameters.iVKGetAudio ??= new PlayListVK(parameters.Playlist, DispatcherQueue);
                await parameters.iVKGetAudio.WaitForTracksAsync(token);
                return (typeof(PlayListPage), parameters.iVKGetAudio);
            case SectionType.CustomIVKGetAudio:
                await parameters.iVKGetAudio.WaitForTracksAsync(token);
                return (typeof(PlayListPage), parameters.iVKGetAudio);
            case SectionType.UserPlayListList:
                var id = long.Parse(parameters.SectionID);
                var playlists = await VK.api.Audio.GetPlaylistsAsync(id, 100).WaitAsync(token);
                return (typeof(UserPlayList), new UserPlayListParameters
                { VKaudioPlaylists = playlists, UserId = id, offset = 100, LoadedAll = playlists.Count != 100, openedPlayList = parameters.openedPlayList });
            case SectionType.ConversDialogs:
                var dialogs = await VK.api.Messages.GetConversationsAsync(new GetConversationsParams
                { Count = 50, Offset = 0, Extended = true, Fields = new[] { "photo_max_orig", "online", "can_write_private_message" } }).WaitAsync(token);
                return (typeof(ConversationsList), new ConversationsListParams { result = dialogs, itsAll = dialogs.Count < 50 });
            case SectionType.LoadFriends:
                var request = new VkParameters
                {
                    { "user_id", AccountsDB.activeAccount.id }, { "count", 25 }, { "offset", 0 }, { "order", "name" },
                    { "fields", "can_see_audio,photo_100,photo_200_orig,status,nickname,can_write_private_message" }
                };
                var friends = (await VK.api.CallAsync("friends.get", request).WaitAsync(token)).ToVkCollectionOf<User>(x => x);
                return (typeof(FriendsList), new FriendsListParametrs { friends = friends, itsAll = friends.Count < 25 });
            case SectionType.Artist:
                var artist = await VK.vkService.GetAudioArtistAsync(parameters.SectionID).WaitAsync(token);
                return await SectionAsync(artist.Catalog.DefaultSection, token);
            case SectionType.UserSection:
                var user = await VK.vkService.GetAudioUser(parameters.SectionID).WaitAsync(token);
                return await SectionAsync(user.Catalog.DefaultSection, token);
            case SectionType.Search when !string.IsNullOrWhiteSpace(parameters.searchText):
                var search = await VK.vkService.GetAudioSearchAsync(parameters.searchText).WaitAsync(token);
                var section = search?.Catalog?.Sections?.Find(s => s.Id == search.Catalog.DefaultSection)
                    ?? search?.Catalog?.Sections?.Find(s => true);
                if (section == null) throw new InvalidOperationException("Поиск не вернул раздел.");
                if (section.Blocks.Count == 0)
                {
                    var full = await VK.vkService.GetSectionAsync(section.Id).AsTask().WaitAsync(token);
                    section = full.Section;
                }
                if (!string.IsNullOrEmpty(parameters.searchFilter) && parameters.searchFilter != "all")
                    section.Blocks = section.Blocks.FindAll(b => b.DataType == parameters.searchFilter);
                return (typeof(SectionView), section);
            default:
                return await SectionAsync(parameters.SectionID, token);
        }
    }
    private async Task<(Type Page, object Data)> SectionAsync(string id, CancellationToken token)
    {
        var response = await VK.vkService.GetSectionAsync(id).AsTask().WaitAsync(token);
        return response?.Section == null ? throw new InvalidOperationException("Раздел недоступен.") : (typeof(SectionView), response.Section);
    }
    public void Dispose() { MainWindow.onRefreshClicked -= Refresh; loading?.Cancel(); }
}
