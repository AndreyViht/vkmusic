using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Linq;
using VK_UI3.Services;

namespace VK_UI3.Views.Settings
{
    public sealed partial class ThemeSettingsExpander : Expander
    {
        private bool _isInitializing = true;

        public ThemeSettingsExpander()
        {
            this.InitializeComponent();
            this.Loaded += ThemeSettingsExpander_Loaded;
        }

        private void ThemeSettingsExpander_Loaded(object sender, RoutedEventArgs e)
        {
            _isInitializing = true;

            AccentGridView.ItemsSource = AppThemeService.PredefinedAccents;

            // Set current mode
            switch (AppThemeService.CurrentThemeMode)
            {
                case AppThemeMode.Light:
                    ThemeModeRadioButtons.SelectedItem = RadioLight;
                    break;
                case AppThemeMode.OledBlack:
                    ThemeModeRadioButtons.SelectedItem = RadioOled;
                    break;
                case AppThemeMode.System:
                    ThemeModeRadioButtons.SelectedItem = RadioSystem;
                    break;
                default:
                    ThemeModeRadioButtons.SelectedItem = RadioDark;
                    break;
            }

            // Set current accent in GridView
            var currentAccent = AppThemeService.PredefinedAccents.FirstOrDefault(a => a.Id == AppThemeService.CurrentAccentId);
            if (currentAccent != null)
            {
                AccentGridView.SelectedItem = currentAccent;
            }

            _isInitializing = false;
        }

        private void ThemeModeRadioButtons_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;

            if (ThemeModeRadioButtons.SelectedItem is RadioButton selectedRadio && selectedRadio.Tag is string tag)
            {
                if (System.Enum.TryParse<AppThemeMode>(tag, out var mode))
                {
                    AppThemeService.SetThemeMode(mode);
                }
            }
        }

        private void AccentGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is AccentColorItem item)
            {
                AccentGridView.SelectedItem = item;
                AppThemeService.SetAccent(item.Id);
            }
        }
    }
}
