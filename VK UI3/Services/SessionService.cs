using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using VK_UI3.DB;
using VkNet.Abstractions;
using VkNet.Exception;

namespace VK_UI3.Services;

public static class SessionService
{
    private static long? redirectedAccount;
    public static bool IsAuthorizationError(Exception error) =>
        error is VkApiException vk && vk.ErrorCode == 5 ||
        error is AggregateException aggregate && aggregate.InnerExceptions.Any(IsAuthorizationError) ||
        error.InnerException != null && IsAuthorizationError(error.InnerException);

    public static bool Handle(Exception error, long? expectedAccount = null)
    {
        if (!IsAuthorizationError(error)) return false;
        var account = AccountsDB.activeAccount;
        if (expectedAccount.HasValue && expectedAccount != account.id) return true;
        if (redirectedAccount == account.id) return true;
        redirectedAccount = account.id;
        account.Token = null;
        account.Expiration = DateTimeOffset.MinValue;
        account.Update();
        MainWindow.mainWindow?.DispatcherQueue.TryEnqueue(() =>
        {
            MediaPlayerService.StopPlayback();
            MainWindow.mainWindow.GoLogin();
            new Views.Notification.Notification("Сессия ВК завершена", "Войдите в аккаунт снова, чтобы продолжить слушать музыку.");
        });
        return true;
    }

    public static async Task ValidateAsync()
    {
        var account = AccountsDB.activeAccount;
        if (string.IsNullOrWhiteSpace(account.Token))
        {
            MainWindow.mainWindow.GoLogin();
            return;
        }
        try
        {
            await App._host.Services.GetRequiredService<IUsersCategory>().GetAsync(Enumerable.Empty<long>())
                .WaitAsync(TimeSpan.FromSeconds(20));
            redirectedAccount = null;
            if (MediaPlayerService.iVKGetAudio == null && SettingsTable.GetSetting("player.remember", "1").settingValue == "1")
                await PlaybackStateService.RestoreAsync();
        }
        catch (Exception error)
        {
            if (!Handle(error)) DiagnosticsService.Record("session", error);
        }
    }
}
