using Glass.App.Configuration;
using Glass.App.Runtime;
using Glass.Core.Applications;
using Glass.Core.Appearance;
using Glass.Core.Editing;
using Glass.Core.Geometry;
using Glass.Core.Placement;
using Glass.Core.Product;
using Glass.Core.Shell;
using Glass.Platform.Windows.Applications;
using Glass.Platform.Windows.Displays;
using Glass.Platform.Windows.Runtime;
using Glass.Platform.Windows.Windowing;
using Glass.Shell.Runtime;
using Glass.Widgets.Abstractions;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System.Globalization;
using Windows.ApplicationModel.DataTransfer;

namespace Glass.App;

public sealed partial class ControlCenterWindow
{
    private void LoadBehavior()
    {
        _loading = true;
        var settings = _settings.Current;
        Select(MotionMode, settings.Appearance.MotionPreference.ToString());
        DirectActivation.IsOn = settings.Taskbar.ActivateSingleWindowDirectly;
        ToggleMinimize.IsOn = settings.Taskbar.ToggleForegroundWindowMinimize;
        RunningIndicators.IsOn = settings.Taskbar.ShowRunningIndicators;
        ShowTooltips.IsOn = settings.Taskbar.ShowTooltips;
        RespectFullscreen.IsOn = settings.Taskbar.RespectFullscreenApplications;
        _loading = false;
    }

    private async void Behavior_Changed(object sender, object args)
    {
        if (_loading) return;
        await _settings.UpdateAsync(current => current with
        {
            Appearance = current.Appearance with
            {
                MotionPreference = Parse(MotionMode, MotionPreference.System),
            },
            Taskbar = current.Taskbar with
            {
                ActivateSingleWindowDirectly = DirectActivation.IsOn,
                ToggleForegroundWindowMinimize = ToggleMinimize.IsOn,
                ShowRunningIndicators = RunningIndicators.IsOn,
                ShowTooltips = ShowTooltips.IsOn,
                RespectFullscreenApplications = RespectFullscreen.IsOn,
            },
        });
    }

}
