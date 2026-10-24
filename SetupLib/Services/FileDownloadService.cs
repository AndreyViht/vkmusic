using SetupLib.Interfaces;
namespace SetupLib.Services;
public class FileDownloadService : IFileDownloadService
{
    private readonly Func<HttpClient> createClient;
    public FileDownloadService(Func<HttpClient>? createClient = null) => this.createClient = createClient ?? (() => new HttpClient());
    public event EventHandler<DownloadProgressChangedEventArgs>? DownloadProgressChanged;
    public async Task DownloadFileAsync(string url, string destinationPath)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https") throw new InvalidOperationException("Для загрузки требуется HTTPS-адрес.");
        var partial = destinationPath + ".partial";
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        try
        {
            using var client = createClient();
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentType?.MediaType?.Contains("html", StringComparison.OrdinalIgnoreCase) == true)
                throw new InvalidDataException("Сервер вернул веб-страницу вместо файла. Проверьте источник обновления.");
            var length = response.Content.Headers.ContentLength;
            await using (var source = await response.Content.ReadAsStreamAsync(timeout.Token))
            await using (var target = File.Create(partial))
            {
                long total = 0;
                var buffer = new byte[81920];
                int count;
                while ((count = await source.ReadAsync(buffer, timeout.Token)) != 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, count), timeout.Token);
                    total += count;
                    DownloadProgressChanged?.Invoke(this, new DownloadProgressChangedEventArgs
                    { BytesDownloaded = total, TotalBytes = length ?? 0, Percentage = length > 0 ? total * 100d / length.Value : 0 });
                }
                if (total == 0 || (length.HasValue && total != length.Value)) throw new IOException("Файл загружен не полностью.");
            }
            File.Move(partial, destinationPath, true);
        }
        finally { if (File.Exists(partial)) File.Delete(partial); }
    }
}
