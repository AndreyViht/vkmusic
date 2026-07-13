using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using VK_UI3.DB;
using System.Linq;

namespace VK_UI3.Views.Settings
{
    public sealed class OverlaySetting : CheckBox
    {
        private static OverlayWindow _overlayWindow;

        public OverlaySetting()
        {
            try
            {
                this.Content = "Показывать мини-плеер поверх всех окон";

                this.Checked += OverlaySetting_Checked;
                this.Unchecked += OverlaySetting_Unchecked;
                this.Loaded += OverlaySetting_Loaded;

                // Получение стиля из ресурсов
                Style style = Application.Current.Resources["DefaultCheckBoxStyle"] as Style;

                // Установка стиля
                if (style != null) this.Style = style;

                AutomationProperties.SetName(this, "Показывать мини-плеер");
                AutomationProperties.SetHelpText(this, "Показывать специальную кнопку-оверлей поверх всех окон, которая превращается в мини-плеер.");
            }
            catch { }
        }

        private void OverlaySetting_Loaded(object sender, RoutedEventArgs e)
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                var setting = SettingsTable.GetSetting("overlayPlayer");
                if (setting != null)
                {
                    this.IsChecked = setting.settingValue == "1";
                }
                
                // Инициализация при старте (можно вынести в MainWindow, но для простоты сделаем тут)
                UpdateOverlayState(this.IsChecked == true);
            });
        }

        private void OverlaySetting_Unchecked(object sender, RoutedEventArgs e)
        {
            SettingsTable.SetSetting("overlayPlayer", "0");
            UpdateOverlayState(false);
        }

        private void OverlaySetting_Checked(object sender, RoutedEventArgs e)
        {
            SettingsTable.SetSetting("overlayPlayer", "1");
            UpdateOverlayState(true);
        }

        public static void UpdateOverlayState(bool show)
        {
            if (show)
            {
                if (_overlayWindow == null)
                {
                    _overlayWindow = new OverlayWindow();
                    _overlayWindow.Activate();
                }
            }
            else
            {
                if (_overlayWindow != null)
                {
                    _overlayWindow.Close();
                    _overlayWindow = null;
                }
            }
        }
    }
}
