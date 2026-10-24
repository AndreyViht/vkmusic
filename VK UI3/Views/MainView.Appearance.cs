using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using VK_UI3.Services;

namespace VK_UI3.Views;
public sealed partial class MainView
{
    private bool menuHovered;
    private bool menuOpen;
    private readonly DispatcherTimer menuHideTimer = new() { Interval = TimeSpan.FromMilliseconds(650) };
    private Storyboard menuAnimation;
    private void AttachMenuAppearance()
    {
        NavigationAppearance.Changed += MenuAppearanceChanged;
        MenuHost.PointerEntered += MenuEntered; MenuHost.PointerExited += MenuExited;
        MenuHost.GotFocus += MenuFocused; MenuHost.LostFocus += MenuBlurred;
        menuHideTimer.Tick += HideMenuTick;
        ApplyMenuAppearance();
    }
    private void DetachMenuAppearance()
    {
        NavigationAppearance.Changed -= MenuAppearanceChanged;
        MenuHost.PointerEntered -= MenuEntered; MenuHost.PointerExited -= MenuExited;
        MenuHost.GotFocus -= MenuFocused; MenuHost.LostFocus -= MenuBlurred;
        menuHideTimer.Tick -= HideMenuTick; menuHideTimer.Stop(); menuAnimation?.Stop();
    }
    private void MenuAppearanceChanged(object sender, EventArgs e) => ApplyMenuAppearance();
    private void MenuEntered(object sender, PointerRoutedEventArgs e) { menuHovered = true; RevealMenu(); }
    private void MenuExited(object sender, PointerRoutedEventArgs e) { menuHovered = false; menuHideTimer.Start(); }
    private void MenuFocused(object sender, RoutedEventArgs e) => RevealMenu();
    private void MenuBlurred(object sender, RoutedEventArgs e) => menuHideTimer.Start();
    private bool MenuHasFocus()
    {
        var focused = FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
        if (focused is Control control && control.FocusState != FocusState.Keyboard) return false;
        while (focused != null) { if (focused == MenuHost) return true; focused = VisualTreeHelper.GetParent(focused); }
        return false;
    }
    private void HideMenuTick(object sender, object e)
    {
        menuHideTimer.Stop();
        if (NavigationAppearance.AutoHide && !menuHovered && !MenuHasFocus()) SetMenuOpen(false);
    }
    private void RevealMenu() { menuHideTimer.Stop(); SetMenuOpen(true); }
    private void ApplyMenuAppearance()
    {
        var dock = NavigationAppearance.Dock;
        bool vertical = dock == MenuDock.Left || dock == MenuDock.Right;
        CustomTopNav.Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal;
        CustomTopNav.HorizontalAlignment = vertical ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;
        CustomTopNav.Margin = new Thickness(6);
        MenuScroll.HorizontalScrollMode = vertical ? ScrollMode.Disabled : ScrollMode.Enabled;
        MenuScroll.VerticalScrollMode = vertical ? ScrollMode.Enabled : ScrollMode.Disabled;
        MenuScroll.HorizontalScrollBarVisibility = vertical ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
        MenuScroll.VerticalScrollBarVisibility = vertical ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
        Grid.SetRow(MenuHost, dock == MenuDock.Bottom ? 3 : vertical ? 1 : 0);
        Grid.SetColumn(MenuHost, dock == MenuDock.Right ? 2 : 0);
        Grid.SetRowSpan(MenuHost, vertical ? 2 : 1); Grid.SetColumnSpan(MenuHost, vertical ? 1 : 3);
        MenuHost.Width = vertical ? 230 : double.NaN;
        MenuHost.HorizontalAlignment = HorizontalAlignment.Stretch;
        MenuHost.VerticalAlignment = VerticalAlignment.Stretch;
        foreach (var child in CustomTopNav.Children)
            if (child is Button button) button.HorizontalAlignment = vertical ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;
        SetMenuOpen(!NavigationAppearance.AutoHide || menuHovered || MenuHasFocus());
    }
    private void SetMenuOpen(bool visible)
    {
        menuOpen = visible;
        var vertical = NavigationAppearance.Dock is MenuDock.Left or MenuDock.Right;
        MenuHost.MaxWidth = vertical && !visible ? 9 : double.PositiveInfinity;
        MenuHost.MaxHeight = !vertical && !visible ? 9 : double.PositiveInfinity;
        MenuScroll.IsHitTestVisible = visible;
        menuAnimation?.Stop();
        if (!visible) { MenuScroll.Opacity = 0; return; }
        var animation = new DoubleAnimation { To = 1, Duration = TimeSpan.FromMilliseconds(180) };
        Storyboard.SetTarget(animation, MenuScroll); Storyboard.SetTargetProperty(animation, "Opacity");
        menuAnimation = new Storyboard(); menuAnimation.Children.Add(animation); menuAnimation.Begin();
    }
    private void FocusMenu()
    {
        RevealMenu();
        if (CustomTopNav.Children.Count > 0 && CustomTopNav.Children[0] is Button button) button.Focus(FocusState.Keyboard);
    }
}
