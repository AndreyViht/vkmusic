using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using VK_UI3.DB;
using VK_UI3.Services;
using Windows.ApplicationModel.DataTransfer;

namespace VK_UI3.Views.Settings;
public sealed class PlaybackTools : StackPanel
{
    public PlaybackTools()
    {
        Spacing = 10;
        void Button(string title, Action action)
        {
            var button = new Button { Content = title }; button.Click += (_, _) => action(); Children.Add(button);
        }
        Button("Открыть очередь", () => MainView.OpenLibrary("queue"));
        Button("Локальная музыка", () => MainView.OpenLibrary("local"));
        var restore = new CheckBox { Content = "Сохранять очередь и позицию воспроизведения", IsChecked = SettingsTable.GetSetting("player.remember", "1").settingValue == "1" };
        restore.Checked += (_, _) => SettingsTable.SetSetting("player.remember", "1");
        restore.Unchecked += (_, _) => { SettingsTable.SetSetting("player.remember", "0"); PlaybackStateService.Forget(); };
        Children.Add(restore);
        Button("Восстановить последнее прослушивание", async () => await PlaybackStateService.RestoreAsync());
        var normalize = new CheckBox { Content = "Выравнивание громкости (FFmpeg, со следующего трека)", IsChecked = SettingsTable.GetSetting("player.normalize", "0").settingValue == "1" };
        normalize.Checked += (_, _) => SettingsTable.SetSetting("player.normalize", "1");
        normalize.Unchecked += (_, _) => SettingsTable.SetSetting("player.normalize", "0"); Children.Add(normalize);
        var minutes = new NumberBox { Header = "Таймер сна, минут", Minimum = 0, Maximum = 240, Value = 30, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        Children.Add(minutes);
        var timerStatus = new TextBlock { TextWrapping = TextWrapping.Wrap };
        Button("Включить таймер сна", () => { PlaybackStateService.SetSleepTimer(double.IsFinite(minutes.Value) ? (int)minutes.Value : 0); timerStatus.Text = PlaybackStateService.SleepUntil is { } end ? $"Воспроизведение остановится в {end:HH:mm}" : "Таймер выключен"; });
        Button("Выключить таймер", () => { PlaybackStateService.SetSleepTimer(0); timerStatus.Text = "Таймер выключен"; }); Children.Add(timerStatus);
        var report = new TextBox { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, MinHeight = 120 };
        Button("Диагностика", () => report.Text = DiagnosticsService.Report());
        Button("Копировать диагностику без личных данных", () => { var data = new DataPackage(); data.SetText(DiagnosticsService.Report()); Clipboard.SetContent(data); });
        Children.Add(report);
    }
}
