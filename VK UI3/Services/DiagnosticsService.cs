using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using VK_UI3.DB;
using VK_UI3.DownloadTrack;

namespace VK_UI3.Services;

public static class DiagnosticsService
{
    private static readonly Queue<string> errors = new();
    public static void Record(string area, Exception error)
    {
        // Never retain messages, URLs, identifiers or stack traces: they can contain tokens.
        lock (errors)
        {
            errors.Enqueue($"{DateTime.Now:HH:mm:ss} {area}: {error.GetType().Name}");
            while (errors.Count > 20) errors.Dequeue();
        }
    }
    public static string Report()
    {
        string recent;
        lock (errors) recent = string.Join(Environment.NewLine, errors.ToArray());
        return $"Viht Music VK {Assembly.GetExecutingAssembly().GetName().Version}\n" +
            $"Сеть: {(NetworkInterface.GetIsNetworkAvailable() ? "подключена" : "нет подключения")}\n" +
            $"Аккаунт ВК: {(string.IsNullOrEmpty(AccountsDB.activeAccount.Token) ? "требуется вход" : "токен сохранён")}\n" +
            $"FFmpeg: {(new CheckFFmpeg().IsExist() ? "установлен" : "не установлен")}\n" +
            $"Очередь: {MediaPlayerService.iVKGetAudio?.listAudio.Count ?? 0}\n" +
            $"Последние ошибки (без личных данных):\n{recent}";
    }
}
