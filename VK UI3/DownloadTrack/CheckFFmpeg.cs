using System;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Linq;
using VK_UI3.DB;

namespace VK_UI3.DownloadTrack
{
    public class CheckFFmpeg
    {
        public async Task<double?> getFileSizeInMBAsync()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    var response = await client.GetAsync(getLinkFFMPEG(), HttpCompletionOption.ResponseHeadersRead);
                    if (response.IsSuccessStatusCode)
                    {
                        var contentLength = response.Content.Headers.ContentLength;
                        return contentLength.HasValue ? (double)contentLength.Value / (1024 * 1024) : (double?)null;
                    }
                }
            }
            catch
            {
                // Log error if needed
            }
            return null;
        }

        public string GetFFmpegDirectory()
        {
            try
            {
                var path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                path = Path.Combine(path, "VKMMKZ", "FFmpeg");

                // Создаем все поддиректории, если они не существуют
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);

                    // Добавляем скрытый атрибут для родительской папки VKMMKZ (опционально)
                    var parentDir = Path.GetDirectoryName(path);
                    if (Directory.Exists(parentDir))
                    {
                        try
                        {
                            File.SetAttributes(parentDir, File.GetAttributes(parentDir) | FileAttributes.Hidden);
                        }
                        catch
                        {
                            // Если не получилось установить атрибут - игнорируем
                        }
                    }
                }

                return path;
            }
            catch (Exception ex)
            {
                // Логируем ошибку, если нужно
                Console.WriteLine($"Ошибка при создании директории FFmpeg: {ex.Message}");
                throw; // или возвращаем путь к временной директории
            }
        }

        public string GetFFmpegPath()
        {
            return Path.Combine(GetFFmpegDirectory(), RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffmpeg.exe" : "ffmpeg");
        }

        public bool IsExist()
        {
            string directory = GetFFmpegDirectory();

            if (!Directory.Exists(directory))
            {
                return false;
            }

            string[] requiredFiles = new string[]
            {
                "avcodec-61.dll",
                "avdevice-61.dll",
                "avfilter-10.dll",
                "avformat-61.dll",
                "avutil-59.dll",
                "swresample-5.dll",
                "swscale-8.dll"
            };

            foreach (string file in requiredFiles)
            {
                if (!File.Exists(Path.Combine(directory, file)))
                {
                    return false;
                }
            }

            return true;
        }

        public string? getLinkFFMPEG()
        {
            // FFmpeg.AutoGen 7.1 requires ABI 61/59. Pin a compatible shared build.
            return RuntimeInformation.ProcessArchitecture == Architecture.X64
                ? "https://github.com/BtbN/FFmpeg-Builds/releases/download/autobuild-2026-07-31-14-10/ffmpeg-n7.1.5-12-g1fdbca85aa-win64-lgpl-shared-7.1.zip"
                : null;
        }
    }

    public class DownloadFileWithProgress
    {
        public bool isNowDownload = false;
        public delegate void DownloadCompletedHandler(object sender, EventArgs e);
        public event DownloadCompletedHandler DownloadCompleted;

        public delegate void ProgressChangedHandler(object sender, DownloadProgressChangedEventArgs e);
        public event ProgressChangedHandler ProgressChanged;

        public string LastError { get; private set; }
        public bool IsDownloading => isDownloading;
        private readonly string tempZipPath = Path.Combine(Path.GetTempPath(), "viht-ffmpeg-" + Guid.NewGuid().ToString("N") + ".zip");
        private WebClient webClient;
        private CheckFFmpeg checkFFmpeg = new CheckFFmpeg();
        private static bool isDownloading = false;
        private static object downloadLock = new object();

        public DownloadFileWithProgress()
        {
            webClient = new WebClient();
            webClient.DownloadFileCompleted += Completed;
            webClient.DownloadProgressChanged += ProgressChangedEvent;
            if (MainWindow.downloadFileWithProgress == null)
            {
                MainWindow.downloadFileWithProgress = this;
                _ = MainWindow.mainWindow.requstDownloadFFMpegAsync();
            }
            else MainWindow.mainWindow.MainWindow_showDownload();
        }

        public double? mb;

        public async void DownloadFile()
        {
            lock (downloadLock) { if (isDownloading) return; isDownloading = true; }
            LastError = null;
            try
            {
                var url = checkFFmpeg.getLinkFFMPEG() ?? throw new NotSupportedException("Автозагрузка FFmpeg недоступна для этой архитектуры.");
                mb = await checkFFmpeg.getFileSizeInMBAsync();
                webClient.DownloadFileAsync(new Uri(url), tempZipPath);
            }
            catch (Exception error) { Completed(this, new AsyncCompletedEventArgs(error, false, null)); }
        }

        public void CancelDownload() { webClient.CancelAsync(); }

        private void ExtractFFmpegFromZip(string zipPath)
        {
            var staging = Path.Combine(Path.GetTempPath(), "viht-ffmpeg-extract-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            try
            {
                ZipFile.ExtractToDirectory(zipPath, staging);
                var codec = Directory.GetFiles(staging, "avcodec-61.dll", SearchOption.AllDirectories).SingleOrDefault()
                    ?? throw new InvalidDataException("В архиве нет совместимых библиотек FFmpeg 7.1.");
                var binary = Path.GetDirectoryName(codec);
                string[] required = { "avcodec-61.dll", "avdevice-61.dll", "avfilter-10.dll", "avformat-61.dll", "avutil-59.dll", "swresample-5.dll", "swscale-8.dll" };
                if (required.Any(name => !File.Exists(Path.Combine(binary, name)))) throw new InvalidDataException("Неполный архив FFmpeg.");
                var target = checkFFmpeg.GetFFmpegDirectory();
                foreach (var file in Directory.GetFiles(binary)) File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
                foreach (var license in Directory.GetFiles(staging, "*COPYING*", SearchOption.AllDirectories).Concat(Directory.GetFiles(staging, "LICENSE*", SearchOption.AllDirectories)))
                    File.Copy(license, Path.Combine(target, Path.GetFileName(license)), true);
                if (!checkFFmpeg.IsExist()) throw new InvalidDataException("Проверка FFmpeg не пройдена.");
            }
            finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
        }

        private void Completed(object sender, AsyncCompletedEventArgs e)
        {
            try
            {
                if (e.Cancelled) throw new OperationCanceledException();
                if (e.Error != null) throw e.Error;
                ExtractFFmpegFromZip(tempZipPath);
                LastError = null;
                MainWindow.mainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    if (SettingsTable.GetSetting("downloadALL") == null) PlayListDownload.ResumeOnlyFirst(); else PlayListDownload.ResumeAll();
                });
            }
            catch (Exception error)
            {
                VK_UI3.Services.DiagnosticsService.Record("ffmpeg-download", error);
                LastError = error is OperationCanceledException ? "Загрузка отменена." : "Не удалось установить FFmpeg. Проверьте сеть и повторите загрузку.";
            }
            finally
            {
                lock (downloadLock) isDownloading = false;
                if (File.Exists(tempZipPath)) File.Delete(tempZipPath);
                DownloadCompleted?.Invoke(this, EventArgs.Empty);
                if (LastError == null) MainWindow.downloadFileWithProgress = null;
            }
        }

        private void ProgressChangedEvent(object sender, DownloadProgressChangedEventArgs e)
        {
            ProgressChanged?.Invoke(this, e);
        }
    }
}