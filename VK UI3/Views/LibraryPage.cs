using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VK_UI3.DB;
using VK_UI3.Helpers;
using VK_UI3.Services;
using VK_UI3.VKs.IVK;
using Windows.Storage.Pickers;

namespace VK_UI3.Views;

public sealed class LibraryPage : Page
{
    private readonly ListView tracks = new() { SelectionMode = ListViewSelectionMode.Single };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel tools = new() { Orientation = Orientation.Horizontal, Spacing = 8 };
    private readonly ObservableCollection<ExtendedAudio> local = new();
    private bool offline;
    public LibraryPage()
    {
        var layout = new Grid { Padding = new Thickness(16), RowSpacing = 12 };
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.Children.Add(tools); Grid.SetRow(status, 1); layout.Children.Add(status);
        Grid.SetRow(tracks, 2); layout.Children.Add(tracks); Content = layout;
        tracks.DoubleTapped += (_, _) => PlaySelected();
        tracks.DragItemsCompleted += (_, _) => PlaybackStateService.Save();
    }
    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        offline = e.Parameter as string == "local";
        tools.Children.Clear();
        Button Add(string text, Action action)
        {
            var button = new Button { Content = text }; button.Click += (_, _) => action(); tools.Children.Add(button); return button;
        }
        if (Frame.CanGoBack) Add("Назад", () => Frame.GoBack());
        Add("Воспроизвести", PlaySelected);
        Add("Пауза / продолжить", async () => await MediaPlayerService.TogglePlayPause());
        Add("Следующий", MediaPlayerService.PlayNextTrack);
        if (offline)
        {
            tracks.ItemsSource = local;
            Add("Добавить папку", async () => await ChooseFolderAsync());
            Add("Обновить", async () => await ScanAsync());
            Add("Следующим", () => { if (tracks.SelectedItem is ExtendedAudio item) PlaybackStateService.Enqueue(item, true); });
            await ScanAsync();
        }
        else
        {
            tracks.ItemsSource = PlaybackStateService.Pending;
            tracks.CanDragItems = true; tracks.CanReorderItems = true; tracks.AllowDrop = true;
            tracks.ReorderMode = ListViewReorderMode.Enabled;
            Add("Удалить из очереди", () => { if (tracks.SelectedItem is ExtendedAudio item) PlaybackStateService.Pending.Remove(item); PlaybackStateService.Save(); });
            Add("Выше", () => Move(-1)); Add("Ниже", () => Move(1));
            Add("Очистить", () => { PlaybackStateService.Pending.Clear(); PlaybackStateService.Save(); });
            Add("Текущий список", () => { if (MediaPlayerService.iVKGetAudio != null) MainView.OpenIVkAudio(MediaPlayerService.iVKGetAudio); });
            status.Text = "Далее в очереди. Добавляйте треки через их меню; перетаскивайте для изменения порядка.";
        }
    }
    private void Move(int delta)
    {
        int index = tracks.SelectedIndex, next = index + delta;
        if (index < 0 || next < 0 || next >= PlaybackStateService.Pending.Count) return;
        PlaybackStateService.Pending.Move(index, next); tracks.SelectedIndex = next; PlaybackStateService.Save();
    }
    private void PlaySelected()
    {
        if (tracks.SelectedItem is not ExtendedAudio selected) return;
        if (offline) selected.iVKGetAudio.playTrack(selected.iVKGetAudio.listAudio.IndexOf(selected));
        else { var index = PlaybackStateService.Pending.IndexOf(selected); if (index > 0) PlaybackStateService.Pending.Move(index, 0); MediaPlayerService.PlayNextTrack(); }
    }
    private async Task ChooseFolderAsync()
    {
        try
        {
            var picker = new FolderPicker(); picker.FileTypeFilter.Add("*");
            WinRT.Interop.InitializeWithWindow.Initialize(picker, MainWindow.hvn);
            var folder = await picker.PickSingleFolderAsync();
            if (folder == null) return;
            var folders = Folders();
            if (!folders.Contains(folder.Path, StringComparer.OrdinalIgnoreCase)) folders.Add(folder.Path);
            SettingsTable.SetSetting("local.folders", System.Text.Json.JsonSerializer.Serialize(folders));
            await ScanAsync();
        }
        catch (Exception error) { DiagnosticsService.Record("local-folder", error); status.Text = "Не удалось открыть папку."; }
    }
    private static List<string> Folders()
    {
        try { return System.Text.Json.JsonSerializer.Deserialize<List<string>>(SettingsTable.GetSetting("local.folders")?.settingValue ?? "[]") ?? new(); }
        catch { return new(); }
    }
    private async Task ScanAsync()
    {
        tools.IsHitTestVisible = false; status.Text = "Поиск локальных треков…";
        try
        {
            var folders = Folders().Concat(PathTable.GetAllPaths().Select(p => p.path)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var files = await Task.Run(() => folders.Where(Directory.Exists).SelectMany(folder => Directory.EnumerateFiles(folder, "*",
                new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint }))
                .Where(f => new[] { ".mp3", ".flac", ".wav", ".m4a", ".ogg", ".aac" }.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .Distinct(StringComparer.OrdinalIgnoreCase).Take(10000).ToArray());
            var source = new SimpleAudio(DispatcherQueue) { name = "Локальная музыка", itsAll = true, countTracks = files.Length };
            local.Clear();
            foreach (var path in files)
            {
                var item = new ExtendedAudio(new VkNet.Model.Attachments.Audio { Title = Path.GetFileNameWithoutExtension(path), Artist = "Локальная музыка", Url = new Uri(path) }, source);
                source.listAudio.Add(item); local.Add(item);
            }
            status.Text = files.Length == 0 ? "Добавьте папку с музыкой. Скачанные треки из сохранённых папок появятся здесь автоматически." : $"Локальная музыка: {files.Length} треков. Доступна без входа в ВК.";
        }
        catch (Exception error) { DiagnosticsService.Record("local-scan", error); status.Text = "Не удалось прочитать музыкальную библиотеку."; }
        finally { tools.IsHitTestVisible = true; }
    }
}
