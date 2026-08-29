using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using VK_UI3.DB;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace VK_UI3.Services
{
    public enum AppThemeMode
    {
        Dark = 0,
        Light = 1,
        OledBlack = 2,
        System = 3
    }

    public class AccentColorItem
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public Color Color { get; set; }
        public string Hex { get; set; }
    }

    public static class AppThemeService
    {
        public static event EventHandler ThemeChanged;

        private const string SettingThemeMode = "app_theme_mode";
        private const string SettingAccentColor = "app_accent_color";

        public static readonly List<AccentColorItem> PredefinedAccents = new()
        {
            new AccentColorItem { Id = "vk_blue", Title = "ВКонтакте", Color = Color.FromArgb(255, 0, 119, 255), Hex = "#0077FF" },
            new AccentColorItem { Id = "pink_purple", Title = "Розово-пурпурный", Color = Color.FromArgb(255, 217, 70, 239), Hex = "#D946EF" },
            new AccentColorItem { Id = "gold_black", Title = "Золото", Color = Color.FromArgb(255, 255, 215, 0), Hex = "#FFD700" },
            new AccentColorItem { Id = "oled_mono", Title = "Монохром", Color = Color.FromArgb(255, 156, 163, 175), Hex = "#9CA3AF" },
            new AccentColorItem { Id = "emerald", Title = "Изумруд", Color = Color.FromArgb(255, 16, 185, 129), Hex = "#10B981" },
            new AccentColorItem { Id = "crimson", Title = "Киберпанк", Color = Color.FromArgb(255, 255, 51, 102), Hex = "#FF3366" },
            new AccentColorItem { Id = "sunset", Title = "Закат", Color = Color.FromArgb(255, 255, 107, 0), Hex = "#FF6B00" },
            new AccentColorItem { Id = "cyan", Title = "Бирюзовый", Color = Color.FromArgb(255, 6, 182, 212), Hex = "#06B6D4" },
        };

        public static AppThemeMode CurrentThemeMode { get; private set; } = AppThemeMode.Dark;
        public static string CurrentAccentId { get; private set; } = "vk_blue";
        public static Color CurrentAccentColor { get; private set; } = Color.FromArgb(255, 0, 119, 255);

        public static void Initialize()
        {
            var savedMode = SettingsTable.GetSetting(SettingThemeMode)?.settingValue;
            if (!string.IsNullOrEmpty(savedMode) && Enum.TryParse<AppThemeMode>(savedMode, out var mode))
            {
                CurrentThemeMode = mode;
            }
            else
            {
                CurrentThemeMode = AppThemeMode.Dark;
            }

            var savedAccent = SettingsTable.GetSetting(SettingAccentColor)?.settingValue;
            if (!string.IsNullOrEmpty(savedAccent))
            {
                var match = PredefinedAccents.Find(a => a.Id == savedAccent);
                if (match != null)
                {
                    CurrentAccentId = match.Id;
                    CurrentAccentColor = match.Color;
                }
                else if (savedAccent.StartsWith("#") && TryParseHex(savedAccent, out var parsed))
                {
                    CurrentAccentId = "custom";
                    CurrentAccentColor = parsed;
                }
            }

            ApplyTheme(CurrentThemeMode, CurrentAccentColor);
        }

        public static void SetThemeMode(AppThemeMode mode)
        {
            CurrentThemeMode = mode;
            SettingsTable.SetSetting(SettingThemeMode, mode.ToString());
            ApplyTheme(CurrentThemeMode, CurrentAccentColor);
        }

        public static void SetAccent(string accentId, Color? customColor = null)
        {
            CurrentAccentId = accentId;
            if (accentId == "custom" && customColor.HasValue)
            {
                CurrentAccentColor = customColor.Value;
                SettingsTable.SetSetting(SettingAccentColor, $"#{CurrentAccentColor.R:X2}{CurrentAccentColor.G:X2}{CurrentAccentColor.B:X2}");
            }
            else
            {
                var match = PredefinedAccents.Find(a => a.Id == accentId);
                if (match != null)
                {
                    CurrentAccentColor = match.Color;
                    SettingsTable.SetSetting(SettingAccentColor, match.Id);
                }
            }

            ApplyTheme(CurrentThemeMode, CurrentAccentColor);
        }

        public static void ApplyTheme(AppThemeMode mode, Color accentColor)
        {
            if (Application.Current == null) return;

            ElementTheme targetElementTheme = mode switch
            {
                AppThemeMode.Light => ElementTheme.Light,
                AppThemeMode.Dark => ElementTheme.Dark,
                AppThemeMode.OledBlack => ElementTheme.Dark,
                _ => ElementTheme.Default
            };

            if (MainWindow.mainWindow?.Content is FrameworkElement root)
            {
                root.RequestedTheme = targetElementTheme;
            }

            // Apply Dynamic Resource Overrides for accent color
            ApplyAccentColors(accentColor, mode);

            ThemeChanged?.Invoke(null, EventArgs.Empty);
        }

        private static void ApplyAccentColors(Color accentColor, AppThemeMode mode)
        {
            var res = Application.Current.Resources;

            res["SystemAccentColor"] = accentColor;
            res["SystemAccentColorLight1"] = Lighten(accentColor, 0.15);
            res["SystemAccentColorLight2"] = Lighten(accentColor, 0.30);
            res["SystemAccentColorLight3"] = Lighten(accentColor, 0.45);
            res["SystemAccentColorDark1"] = Darken(accentColor, 0.15);
            res["SystemAccentColorDark2"] = Darken(accentColor, 0.30);
            res["SystemAccentColorDark3"] = Darken(accentColor, 0.45);

            var accentBrush = new SolidColorBrush(accentColor);
            res["AccentFillColorDefaultBrush"] = accentBrush;
            res["AccentFillColorSecondaryBrush"] = new SolidColorBrush(Lighten(accentColor, 0.15));
            res["AccentFillColorTertiaryBrush"] = new SolidColorBrush(Lighten(accentColor, 0.30));
            res["ToggleSwitchFillOn"] = accentBrush;
            res["SliderTrackValueFill"] = accentBrush;

            if (mode == AppThemeMode.OledBlack)
            {
                res["OledBackgroundBrush"] = new SolidColorBrush(Color.FromArgb(255, 0, 0, 0));
                res["OledCardBrush"] = new SolidColorBrush(Color.FromArgb(255, 10, 10, 10));
            }
            else
            {
                res["OledBackgroundBrush"] = new SolidColorBrush(Colors.Transparent);
                res["OledCardBrush"] = new SolidColorBrush(Color.FromArgb(255, 30, 30, 30));
            }
        }

        private static Color Lighten(Color color, double fraction)
        {
            byte r = (byte)Math.Clamp(color.R + (255 - color.R) * fraction, 0, 255);
            byte g = (byte)Math.Clamp(color.G + (255 - color.G) * fraction, 0, 255);
            byte b = (byte)Math.Clamp(color.B + (255 - color.B) * fraction, 0, 255);
            return Color.FromArgb(color.A, r, g, b);
        }

        private static Color Darken(Color color, double fraction)
        {
            byte r = (byte)Math.Clamp(color.R * (1 - fraction), 0, 255);
            byte g = (byte)Math.Clamp(color.G * (1 - fraction), 0, 255);
            byte b = (byte)Math.Clamp(color.B * (1 - fraction), 0, 255);
            return Color.FromArgb(color.A, r, g, b);
        }

        private static bool TryParseHex(string hex, out Color color)
        {
            color = Colors.White;
            hex = hex.Replace("#", "").Trim();
            if (hex.Length == 6 &&
                byte.TryParse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out byte r) &&
                byte.TryParse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out byte g) &&
                byte.TryParse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out byte b))
            {
                color = Color.FromArgb(255, r, g, b);
                return true;
            }
            return false;
        }
    }
}
