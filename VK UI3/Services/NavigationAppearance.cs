using System;
using VK_UI3.DB;
namespace VK_UI3.Services;
public enum MenuDock { Top, Bottom, Left, Right }
public static class NavigationAppearance
{
    public static event EventHandler Changed;
    public static MenuDock Dock => Enum.TryParse<MenuDock>(SettingsTable.GetSetting("menu.dock", "Top").settingValue, out var dock) ? dock : MenuDock.Top;
    public static bool AutoHide => SettingsTable.GetSetting("menu.autohide", "0").settingValue == "1";
    public static void Set(MenuDock dock, bool autoHide)
    {
        SettingsTable.SetSetting("menu.dock", dock.ToString()); SettingsTable.SetSetting("menu.autohide", autoHide ? "1" : "0");
        Changed?.Invoke(null, EventArgs.Empty);
    }
}
