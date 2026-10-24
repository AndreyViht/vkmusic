using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using System;
using VK_UI3.DB;
using Windows.UI;

namespace VK_UI3.Services;

public enum SurfaceStyle { WindowsDesktop, LiquidGlass, Midnight, Aurora }

public static class SurfaceTheme
{
    public static SurfaceStyle Current { get; private set; }
    public static void Initialize()
    {
        Enum.TryParse(SettingsTable.GetSetting("appearance.surface", "WindowsDesktop").settingValue, out SurfaceStyle style);
        Current = style;
    }
    public static void Set(SurfaceStyle style)
    {
        Current = style; SettingsTable.SetSetting("appearance.surface", style.ToString());
        AppThemeService.ApplyTheme(AppThemeService.CurrentThemeMode, AppThemeService.CurrentAccentColor);
    }
    public static Color Color(string value)
    {
        var hex = value.TrimStart('#');
        return Windows.UI.Color.FromArgb(255, Convert.ToByte(hex.Substring(0, 2), 16), Convert.ToByte(hex.Substring(2, 2), 16), Convert.ToByte(hex.Substring(4, 2), 16));
    }
    private static void Brush(string key, string hex)
    {
        var resources = Application.Current.Resources;
        var color = Color(hex);
        if (resources.TryGetValue(key, out var existing) && existing is SolidColorBrush brush) brush.Color = color;
        else resources[key] = new SolidColorBrush(color);
    }
    public static void Apply(bool light, bool oled)
    {
        string background, card, control, stroke, secondary;
        double radius;
        switch (Current)
        {
            case SurfaceStyle.LiquidGlass:
                background = light ? "DCE7F4" : "141C2D"; card = light ? "F4F8FD" : "26344B";
                control = light ? "E5EDF8" : "354762"; stroke = light ? "BDCFE4" : "536A89"; secondary = light ? "455A72" : "C2D1E4"; radius = 22; break;
            case SurfaceStyle.Midnight:
                background = light ? "F1EFF8" : "100F1B"; card = light ? "FEFCFF" : "211F32";
                control = light ? "E8E2F3" : "332D4A"; stroke = light ? "D3C8E3" : "514764"; secondary = light ? "625772" : "C7BED8"; radius = 16; break;
            case SurfaceStyle.Aurora:
                background = light ? "E3F0ED" : "0E1D21"; card = light ? "F5FCF9" : "1C3439";
                control = light ? "D9ECE5" : "2B4A4E"; stroke = light ? "B6D5CB" : "45676D"; secondary = light ? "3E615A" : "B5D5CF"; radius = 18; break;
            default:
                background = light ? "EDF2F8" : "141820"; card = light ? "FFFFFF" : "222A36";
                control = light ? "E2E9F2" : "303C4E"; stroke = light ? "C3CFDE" : "4C5C72"; secondary = light ? "4D5D73" : "C0CDDE"; radius = 10; break;
        }
        if (oled) { background = "000000"; card = "0D1016"; control = "1A202A"; }
        Brush("VihtBackgroundBrush", background); Brush("VihtSurfaceBrush", card); Brush("VihtControlBrush", control);
        Brush("VihtStrokeBrush", stroke); Brush("VihtTextBrush", light ? "142238" : "F5F8FF"); Brush("VihtSecondaryTextBrush", secondary);
        Brush("VihtPlayerBrush", light ? "F5F8FC" : "202938");
        if (oled) Brush("VihtPlayerBrush", "10131A");
        Brush("VihtPlayerTextBrush", light ? "142238" : "F5F8FF");
        Brush("VihtAccentBrush", $"{AppThemeService.CurrentAccentColor.R:X2}{AppThemeService.CurrentAccentColor.G:X2}{AppThemeService.CurrentAccentColor.B:X2}");
        var accent = AppThemeService.CurrentAccentColor;
        Brush("VihtAccentTextBrush", (.2126 * accent.R + .7152 * accent.G + .0722 * accent.B) > 155 ? "101828" : "FFFFFF");
        var resources = Application.Current.Resources;
        resources["VihtCardCornerRadius"] = new CornerRadius(radius);
        resources["VihtControlCornerRadius"] = new CornerRadius(Current == SurfaceStyle.LiquidGlass ? 16 : Current == SurfaceStyle.WindowsDesktop ? 6 : 10);
        if (resources["VihtCardBrush"] is AcrylicBrush glass)
        {
            glass.TintColor = Color(card); glass.FallbackColor = Color(card);
            glass.TintOpacity = Current == SurfaceStyle.LiquidGlass ? .88 : 1;
            glass.TintLuminosityOpacity = Current == SurfaceStyle.LiquidGlass ? .90 : 1;
        }
        if (MainWindow.mainWindow?.Content is FrameworkElement root) ApplyGeometry(root);
    }
    private static void ApplyGeometry(DependencyObject node)
    {
        // Existing controls need geometry refreshed too; brush objects themselves stay alive.
        var resources = Application.Current.Resources;
        if (node is Button button && button.Width != button.Height)
            button.CornerRadius = (CornerRadius)resources["VihtControlCornerRadius"];
        if (node is Border border && ReferenceEquals(border.Background, resources["VihtCardBrush"]))
            border.CornerRadius = (CornerRadius)resources["VihtCardCornerRadius"];
        if (node is Panel panel && ReferenceEquals(panel.Background, resources["VihtCardBrush"]))
        {
            if (panel is Grid grid) grid.CornerRadius = (CornerRadius)resources["VihtCardCornerRadius"];
            if (panel is StackPanel stack) stack.CornerRadius = (CornerRadius)resources["VihtCardCornerRadius"];
        }
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(node); index++) ApplyGeometry(VisualTreeHelper.GetChild(node, index));
    }
}
