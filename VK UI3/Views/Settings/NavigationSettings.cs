using Microsoft.UI.Xaml.Controls;
using VK_UI3.Services;
namespace VK_UI3.Views.Settings;
public sealed class NavigationSettings : StackPanel
{
    public NavigationSettings()
    {
        Spacing = 10;
        var position = new ComboBox { Header = "Расположение меню", HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch };
        foreach (var name in new[] { "Сверху", "Снизу", "Слева", "Справа" }) position.Items.Add(name);
        position.SelectedIndex = (int)NavigationAppearance.Dock;
        var hide = new ToggleSwitch { Header = "Скрывать меню до наведения", IsOn = NavigationAppearance.AutoHide, OnContent = "Включено", OffContent = "Выключено" };
        position.SelectionChanged += (_, _) => { if (position.SelectedIndex >= 0) NavigationAppearance.Set((MenuDock)position.SelectedIndex, hide.IsOn); };
        hide.Toggled += (_, _) => NavigationAppearance.Set((MenuDock)position.SelectedIndex, hide.IsOn);
        Children.Add(position); Children.Add(hide);
        Children.Add(new TextBlock { Text = "Наведите на тонкую полосу у края, чтобы раскрыть меню. F10 — открыть с клавиатуры.", TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap });
    }
}
