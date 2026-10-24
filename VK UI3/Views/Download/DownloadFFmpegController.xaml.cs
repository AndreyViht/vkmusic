using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.ComponentModel;
using VK_UI3.DownloadTrack;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VK_UI3.Views.Download
{
    public sealed partial class DownloadFFmpegController : UserControl
    {
        private DownloadFileWithProgress source;
        public DownloadFFmpegController()
        {
            this.InitializeComponent();

            this.Loaded += DownloadFFmpegController_Loaded;
            this.Unloaded += DownloadFFmpegController_Unloaded;
        }

        private void DownloadFFmpegController_Unloaded(object sender, RoutedEventArgs e)
        {
            if (source == null) return;
            source.ProgressChanged -= DownloadFileWithProgress_ProgressChanged;
            source.DownloadCompleted -= DownloadFileWithProgress_DownloadCompleted;
        }

        private void DownloadFileWithProgress_DownloadCompleted(object sender, EventArgs e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                DownloadTitle.Text = source?.LastError ?? "FFmpeg установлен";
                DownloadProgressBar.ShowError = source?.LastError != null;
                RetryButton.IsEnabled = source?.LastError != null;
                CancelButton.IsEnabled = false;
            });
        }

        private void DownloadFFmpegController_Loaded(object sender, RoutedEventArgs e)
        {
            source = MainWindow.downloadFileWithProgress;
            if (source == null) return;
            source.ProgressChanged += DownloadFileWithProgress_ProgressChanged;
            source.DownloadCompleted += DownloadFileWithProgress_DownloadCompleted;
            if (source.LastError != null) DownloadFileWithProgress_DownloadCompleted(source, EventArgs.Empty);
        }
        private void RetryDownload(object sender, RoutedEventArgs e)
        {
            if (source == null) return;
            DownloadTitle.Text = "Загрузка FFmpeg…"; DownloadProgressBar.ShowError = false;
            RetryButton.IsEnabled = false; CancelButton.IsEnabled = true; source.DownloadFile();
        }
        private void CancelDownload(object sender, RoutedEventArgs e) => source?.CancelDownload();

        private void DownloadFileWithProgress_ProgressChanged(object sender, System.Net.DownloadProgressChangedEventArgs e)
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                int progressPercentage = e.ProgressPercentage; // Прогресс в процентах
                DownloadProgressBar.Value = progressPercentage;
                pathText.Text = $"Загружено: {Math.Round((double)e.BytesReceived / (1024 * 1024))} Мб  из  {((sender as DownloadFileWithProgress).mb is double total ? Math.Round(total).ToString() : "?")} Мб ";
            });
        }

    }
}
