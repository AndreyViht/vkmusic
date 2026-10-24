using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VK_UI3.Services;

namespace VK_UI3.Views.Settings;
public sealed class OverlaySetting : CheckBox
{
    private bool initializing;
    public OverlaySetting()
    {
        Content = "Музыкальный островок, когда Viht свёрнут";
        Loaded += (_, _) => { initializing = true; IsChecked = MusicIslandService.Enabled; initializing = false; };
        Checked += Changed; Unchecked += Changed;
    }
    private void Changed(object sender, RoutedEventArgs e) { if (!initializing) MusicIslandService.SetEnabled(IsChecked == true); }
    public static void UpdateOverlayState(bool show) => MusicIslandService.SetEnabled(show);
}
