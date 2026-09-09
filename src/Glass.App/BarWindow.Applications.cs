using Glass.App.Configuration;
using Glass.App.Runtime;
using Glass.Core.Applications;
using Glass.Core.Appearance;
using Glass.Core.Editing;
using Glass.Core.Shell;
using Glass.Platform.Windows.Applications;
using Glass.Platform.Windows.Displays;
using Glass.Platform.Windows.Windowing;
using Glass.Shell.Surfaces;
using Glass.Widgets.Abstractions;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Runtime.InteropServices;
using Windows.UI;

namespace Glass.App;

public sealed partial class BarWindow
{
    private async void AddApplicationItem(
        StackPanel panel,
        ApplicationIdentity identity,
        RunningApplicationGroup? group,
        bool isPinned)
    {
        var productSettings = _services.Settings().Normalize();
        var settings = productSettings.Appearance;
        var taskbar = productSettings.Taskbar;
        var displayName = _services.Applications.Current
            .FirstOrDefault(application => application.Identity == identity)?.DisplayName ??
            group?.DisplayName ?? Path.GetFileNameWithoutExtension(identity.Value);
        var iconSize = settings.ApplicationIconSize;
        var image = new Image { Width = iconSize, Height = iconSize, Stretch = Stretch.Uniform };
        var fallback = new FontIcon { Glyph = "\uE8FC", FontSize = iconSize * 0.72 };
        var iconHost = new Grid { Width = iconSize, Height = iconSize };
        iconHost.Children.Add(fallback);
        iconHost.Children.Add(image);
        var indicator = new Border
        {
            Height = group?.IsActive == true ? 3 : 2,
            Width = group is null ? 0 : group.Windows.Count > 1 ? 14 : 6,
            CornerRadius = new CornerRadius(2),
            Background = group?.IsActive == true
                ? ResolveAccentBrush(settings)
                : new SolidColorBrush(Color.FromArgb(180, 150, 160, 172)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Visibility = taskbar.ShowRunningIndicators ? Visibility.Visible : Visibility.Collapsed,
        };
        var item = new StackPanel { Spacing = 2 };
        item.Children.Add(iconHost);
        item.Children.Add(indicator);
        var button = new Button
        {
            Content = item,
            Padding = new Thickness(6, 4, 6, 3),
            MinWidth = iconSize + 12,
            MinHeight = iconSize + 12,
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(Colors.Transparent),
        };
        AutomationProperties.SetName(button, BuildAccessibleName(displayName, group, isPinned));
        if (taskbar.ShowTooltips) ToolTipService.SetToolTip(button, displayName);
        button.Click += (_, _) => Activate(identity, group, button);
        button.ContextFlyout = ApplicationMenu(identity, group, isPinned);
        button.PointerEntered += (_, _) =>
        {
            var index = _applicationElements.IndexOf(button);
            _services.Motion.ApplyMagnification(_applicationElements, index,
                settings.Magnification, settings.MagnificationMaximumScale);
        };
        button.PointerPressed += (_, _) =>
            _services.Motion.AnimateScale(button, MotionIntent.Press);
        button.PointerReleased += (_, _) =>
            _services.Motion.AnimateScale(button, MotionIntent.Hover, 1);
        _applicationElements.Add(button);
        panel.Children.Add(button);

        try
        {
            using var bitmap = _services.Icons.GetBitmap(identity, (int)iconSize,
                WindowPositioner.GetDpi(_nativeWindow.Hwnd));
            if (bitmap is not null)
            {
                var source = new SoftwareBitmapSource();
                await source.SetBitmapAsync(bitmap);
                image.Source = source;
                fallback.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception exception) when (exception is COMException or InvalidOperationException)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Application icon could not be rendered for {identity.Value}: {exception.Message}");
        }
    }

    private MenuFlyout ApplicationMenu(
        ApplicationIdentity identity,
        RunningApplicationGroup? group,
        bool isPinned)
    {
        var flyout = new MenuFlyout();
        var open = new MenuFlyoutItem { Text = group is null ? "Open" : "Activate" };
        open.Click += (_, _) => Activate(identity, group, null);
        flyout.Items.Add(open);
        var newInstance = new MenuFlyoutItem { Text = "New instance" };
        newInstance.Click += (_, _) => _launcher.Launch(identity);
        flyout.Items.Add(newInstance);
        flyout.Items.Add(new MenuFlyoutSeparator());
        var pin = new MenuFlyoutItem { Text = isPinned ? "Unpin" : "Pin" };
        pin.Click += async (_, _) =>
        {
            if (isPinned) await _services.Shell().UnpinApplicationAsync(Id, identity);
            else await _services.Shell().PinApplicationAsync(Id, identity, BarZone.Center);
        };
        flyout.Items.Add(pin);
        if (isPinned)
        {
            var moveEarlier = new MenuFlyoutItem { Text = "Move earlier" };
            var moveLater = new MenuFlyoutItem { Text = "Move later" };
            moveEarlier.Click += async (_, _) => await MoveApplicationAsync(identity, -1);
            moveLater.Click += async (_, _) => await MoveApplicationAsync(identity, 1);
            flyout.Items.Add(moveEarlier);
            flyout.Items.Add(moveLater);
        }
        if (group is not null)
        {
            flyout.Items.Add(new MenuFlyoutSeparator());
            var close = new MenuFlyoutItem { Text = "Close window" };
            close.Click += (_, _) => _launcher.RequestClose(group.Windows[0]);
            flyout.Items.Add(close);
            if (group.Windows.Count > 1)
            {
                var closeAll = new MenuFlyoutItem { Text = "Close all windows" };
                closeAll.Click += (_, _) => _launcher.RequestCloseAll(group.Windows);
                flyout.Items.Add(closeAll);
            }
        }
        return flyout;
    }

    private async ValueTask MoveApplicationAsync(ApplicationIdentity identity, int direction)
    {
        var content = Definition.Content;
        var index = content.ToList().FindIndex(item =>
            item is PinnedApplicationBarItem pin && pin.Application == identity);
        if (index < 0) return;
        var target = Math.Clamp(index + direction, 0, content.Count - 1);
        await _services.Shell().MoveBarContentAsync(Id, index, target);
    }

    private void Activate(
        ApplicationIdentity identity,
        RunningApplicationGroup? group,
        Button? anchor)
    {
        var preferences = _services.Settings().Taskbar;
        if (group is null) _launcher.Launch(identity);
        else if (group.Windows.Count == 1 && preferences.ActivateSingleWindowDirectly)
            _launcher.ActivateOrToggle(group.Windows[0], preferences.ToggleForegroundWindowMinimize);
        else if (anchor is not null) ShowWindowChooser(anchor, group);
        else _launcher.ActivateOrToggle(group.Windows[0], preferences.ToggleForegroundWindowMinimize);
    }

    private void ShowWindowChooser(Button button, RunningApplicationGroup group)
    {
        var flyout = new MenuFlyout();
        foreach (var window in group.Windows)
        {
            var item = new MenuFlyoutItem
            {
                Text = string.IsNullOrWhiteSpace(window.Title) ? group.DisplayName : window.Title,
                Icon = window.IsForeground ? new FontIcon { Glyph = "\uE73E" } : null,
            };
            item.Click += (_, _) => _launcher.ActivateOrToggle(window,
                _services.Settings().Taskbar.ToggleForegroundWindowMinimize);
            flyout.Items.Add(item);
        }
        flyout.ShowAt(button);
    }

}
