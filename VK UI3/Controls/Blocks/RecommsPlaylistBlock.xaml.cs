using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MusicX.Core.Models;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using VK_UI3.VKs;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VK_UI3.Controls.Blocks
{
    public sealed partial class RecommsPlaylistBlock : UserControl, IBlockAdder
    {
        ObservableCollection<Playlist> playlists = new();
        public RecommsPlaylistBlock()
        {
            this.InitializeComponent();

            this.Loading += RecommsPlaylistBlock_Loading;
            this.Unloaded += RecommsPlaylistBlock_Unloaded;
            this.Loaded += RecommsPlaylistBlock_Loaded;
            this.DataContextChanged += RecommsPlaylistBlock_DataContextChanged;
        }

        private void RecommsPlaylistBlock_Loaded(object sender, RoutedEventArgs e)
        {
            myControl.loadMore = load;
            PopulateBlock();
        }

        private void RecommsPlaylistBlock_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            PopulateBlock();
        }

        Block localBlock;
        private void RecommsPlaylistBlock_Unloaded(object sender, RoutedEventArgs e)
        {
            myControl.loadMore = null;
        }

        private SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
        private async void load()
        {
            await semaphore.WaitAsync();
            try
            {
                if (localBlock == null || localBlock.NextFrom == null) return;
                var a = await VK.vkService.GetSectionAsync(localBlock.Id, localBlock.NextFrom);
                localBlock.NextFrom = a.Section.NextFrom;
                if (a.Playlists == null) return;
                this.DispatcherQueue.TryEnqueue(() => {
                    foreach (var item in a.Playlists)
                    {
                        playlists.Add(item);
                    }
                    if (myControl.CheckIfAllContentIsVisible())
                        load();
                });
            }
            finally
            {
                semaphore.Release();
            }
        }

        private void RecommsPlaylistBlock_Loading(FrameworkElement sender, object args)
        {
            PopulateBlock();
        }

        private void PopulateBlock()
        {
            try
            {
                if (DataContext is not Block block)
                    return;

                localBlock = block;
                playlists.Clear();

                switch (localBlock.Layout?.Name)
                {
                    case "list":
                        myControl.disableLoadMode = true;
                        break;
                    default:
                        myControl.loadMore = load;
                        myControl.ItemsPanelTemplate = (ItemsPanelTemplate)Microsoft.UI.Xaml.Application.Current.Resources["GlobalItemsPanelTemplate"];
                        break;
                }

                if (block.Playlists != null)
                {
                    foreach (var item in block.Playlists)
                    {
                        playlists.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
            }
        }

        public void AddBlock(Block block)
        {
            this.DispatcherQueue.TryEnqueue(async () => {
                foreach (var item in block.Playlists)
                {
                    playlists.Add(item);
                }
            });
        }

        public bool itsAll
        {
            get
            {
                if (localBlock == null) return true;
                if (localBlock.NextFrom == null) return true; else return false;
            }
        }

    }
}
