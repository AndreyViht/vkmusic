using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using SetupLib.Interfaces;

namespace SetupLib.Services;

public class InstallationService : IInstallationService
{
    private readonly IFileDownloadService download;
    private readonly ISystemService system;
    public event EventHandler<InstallStatusChangedEventArgs>? InstallStatusChanged;
    public InstallationService(IFileDownloadService fileDownloadService, ISystemService systemService) { download = fileDownloadService; system = systemService; }
    private void Status(string text) => InstallStatusChanged?.Invoke(this, new InstallStatusChangedEventArgs { Status = text });

    public async Task InstallUpdateAsync(AppUpdater updater, bool skipCertCheck, bool forceInstall, string? PathInstallZIP = null)
    {
        var asset = updater._currentReleaseInfo?.Assets.GetValueOrDefault(updater.SelectedPackageType)
            ?? throw new InvalidOperationException("Нет подходящего пакета обновления.");
        var directory = Path.Combine(Path.GetTempPath(), "VihtUpdate-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, updater.SelectedPackageType == PackageType.ZIP ? "update.zip" : "update.msix");
        Status("Скачивание обновления…");
        await download.DownloadFileAsync(asset.Url, file);
        await VerifyAsync(file, asset);
        if (updater.SelectedPackageType == PackageType.ZIP)
        {
            // Validate the archive before asking the running program to exit.
            using (var archive = ZipFile.OpenRead(file))
                if (archive.Entries.Count(e => e.Name.Equals("Viht Music VK.exe", StringComparison.OrdinalIgnoreCase)) != 1)
                    throw new InvalidDataException("Архив не содержит единственную копию приложения.");
            var target = Path.GetFullPath(PathInstallZIP ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Viht Music VK"));
            ValidateTarget(target);
            var parent = Process.GetCurrentProcess().ProcessName == "Viht Music VK" ? Environment.ProcessId : 0;
            var script = BuildZipScript(file, target, parent);
            var scriptPath = Path.Combine(directory, "install.ps1");
            await File.WriteAllTextAsync(scriptPath, script, new UTF8Encoding(true));
            var process = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = directory };
            foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", scriptPath }) process.ArgumentList.Add(argument);
            Process.Start(process)?.Dispose();
            Status("Установщик готов. После закрытия приложения файлы будут заменены; предыдущая версия сохранится в резервной папке.");
            return;
        }
        if (!skipCertCheck && !string.IsNullOrEmpty(updater._currentReleaseInfo.CertificateUrl) && system.IsRunningAsAdministrator())
            await InstallCertificateAsync(updater._currentReleaseInfo.CertificateUrl);
        await PowerShellAsync($"Add-AppxPackage -Path {Quote(file)} -ErrorAction Stop" + (forceInstall ? " -ForceUpdateFromAnyVersion" : ""));
        Status("Обновление установлено.");
    }

    public static async Task VerifyAsync(string file, PackageAsset asset)
    {
        if (asset.Size > 0 && new FileInfo(file).Length != asset.Size) throw new InvalidDataException("Размер обновления не совпадает с опубликованным.");
        if (string.IsNullOrWhiteSpace(asset.Sha256)) throw new InvalidDataException("Сервер не опубликовал SHA-256. Автоматическая установка отменена.");
        await using var stream = File.OpenRead(file);
        var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream));
        if (!actual.Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Контрольная сумма обновления не совпадает.");
    }
    private static string Quote(string value) => "'" + value.Replace("'", "''") + "'";
    private static void ValidateTarget(string target)
    {
        var path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(target));
        if (path.Equals(Path.TrimEndingDirectorySeparator(Path.GetPathRoot(path)!), StringComparison.OrdinalIgnoreCase) ||
            new[] { Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.Windows) }
                .Any(p => path.Equals(Path.TrimEndingDirectorySeparator(p), StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Выберите отдельную папку приложения.");
    }

    public static string BuildZipScript(string zip, string target, int processId)
    {
        ValidateTarget(target);
        return "$ErrorActionPreference = 'Stop'\n" +
            "$target = " + Quote(Path.TrimEndingDirectorySeparator(Path.GetFullPath(target))) + "\n" +
            "$zip = " + Quote(Path.GetFullPath(zip)) + "\n" +
            "$ownerId = " + processId + "\n" + """
            $parent = Split-Path -LiteralPath $target
            $suffix = [Guid]::NewGuid().ToString('N')
            $stage = Join-Path $parent ('Viht-stage-' + $suffix)
            $backup = $target + '.previous-' + $suffix
            $unpack = Join-Path $PSScriptRoot 'unpack'
            $log = Join-Path $PSScriptRoot 'install.log'
            $moved = $false
            try {
                if ($ownerId -gt 0) {
                    $running = Get-Process -Id $ownerId -ErrorAction SilentlyContinue
                    if ($running -and -not $running.WaitForExit(30000)) { throw 'Приложение не было закрыто за 30 секунд.' }
                }
                New-Item -ItemType Directory -Path $parent -Force | Out-Null
                Expand-Archive -LiteralPath $zip -DestinationPath $unpack -Force
                $executables = @(Get-ChildItem -LiteralPath $unpack -Recurse -File -Filter 'Viht Music VK.exe')
                if ($executables.Count -ne 1) { throw 'Не найдено приложение в архиве.' }
                $source = $executables[0].Directory.FullName
                New-Item -ItemType Directory -Path $stage | Out-Null
                if (Test-Path -LiteralPath $target) {
                    Get-ChildItem -LiteralPath $target -Force | Copy-Item -Destination $stage -Recurse -Force
                }
                Get-ChildItem -LiteralPath $source -Force | Copy-Item -Destination $stage -Recurse -Force
                if (-not (Test-Path -LiteralPath (Join-Path $stage 'Viht Music VK.exe'))) { throw 'Подготовка обновления не завершена.' }
                if (Test-Path -LiteralPath $target) { Move-Item -LiteralPath $target -Destination $backup; $moved = $true }
                try { Move-Item -LiteralPath $stage -Destination $target }
                catch { if ($moved) { Move-Item -LiteralPath $backup -Destination $target }; throw }
                Start-Process -FilePath (Join-Path $target 'Viht Music VK.exe') -WorkingDirectory $target
                'Установка завершена. Резервная копия: ' + $backup | Set-Content -LiteralPath $log -Encoding UTF8
            } catch {
                $_.Exception.ToString() | Set-Content -LiteralPath $log -Encoding UTF8
                Add-Type -AssemblyName PresentationFramework
                [System.Windows.MessageBox]::Show('Не удалось установить обновление. Журнал: ' + $log, 'Viht Music VK') | Out-Null
                exit 1
            }
            """;
    }
    private static async Task PowerShellAsync(string command)
    {
        var start = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes("$ErrorActionPreference='Stop'; " + command)) }) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new IOException("Не удалось запустить установщик.");
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(10)); await output;
        if (process.ExitCode != 0) throw new IOException("Установка не выполнена: " + await error);
        await error;
    }
    public async Task InstallAppInstallerAsync()
    {
        var file = Path.Combine(Path.GetTempPath(), "Viht-AppInstaller.msixbundle");
        await download.DownloadFileAsync("https://github.com/microsoft/winget-cli/releases/download/v1.7.10661/Microsoft.DesktopAppInstaller_8wekyb3d8bbwe.msixbundle", file);
        await PowerShellAsync($"Add-AppxPackage -Path {Quote(file)} -ErrorAction Stop");
    }
    public async Task InstallCertificateAsync(string url)
    {
        var file = Path.Combine(Path.GetTempPath(), "Viht-" + Guid.NewGuid().ToString("N") + ".cer");
        try
        {
            await download.DownloadFileAsync(url, file);
            using var certificate = new X509Certificate2(file);
            if (DateTime.Now > certificate.NotAfter || DateTime.Now < certificate.NotBefore) throw new InvalidDataException("Сертификат недействителен.");
            using var store = new X509Store(StoreName.TrustedPeople, StoreLocation.LocalMachine); store.Open(OpenFlags.ReadWrite);
            if (store.Certificates.Find(X509FindType.FindByThumbprint, certificate.Thumbprint, false).Count == 0) store.Add(certificate);
        }
        finally { if (File.Exists(file)) File.Delete(file); }
    }
    public async Task InstallAppRuntimeAsync() => await PowerShellAsync("winget install --id Microsoft.WindowsAppRuntime.1.6 --exact --silent --accept-source-agreements --accept-package-agreements; if ($LASTEXITCODE -ne 0) { throw 'Windows App Runtime не установлен.' }");
    public void InstallLatestDotNetRuntime() => PowerShellAsync("winget install --id Microsoft.DotNet.DesktopRuntime.8 --exact --silent --accept-source-agreements --accept-package-agreements; if ($LASTEXITCODE -ne 0) { throw '.NET Runtime не установлен.' }").GetAwaiter().GetResult();
}
