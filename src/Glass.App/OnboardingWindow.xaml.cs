using Glass.App.Configuration;
using Glass.App.Runtime;
using Glass.Core.Appearance;
using Glass.Core.Placement;
using Glass.Core.Runtime;
using Glass.Rendering.Materials;
using Glass.Shell.Runtime;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace Glass.App;

public sealed partial class OnboardingWindow : Window
{
    private readonly AppearanceSettingsRuntime _settings;
    private readonly ShellRuntime _shell;
    private readonly GlassMaterialController _materials;
    private readonly Glass.Platform.Windows.Runtime.PackagedStartupTaskService _startup;
    private readonly Func<RenderingPolicy> _renderingPolicy;
    private readonly Func<ValueTask> _completed;
    private int _step;

    internal OnboardingWindow(
        AppearanceSettingsRuntime settings,
        ShellRuntime shell,
        GlassMaterialController materials,
        Glass.Platform.Windows.Runtime.PackagedStartupTaskService startup,
        Func<RenderingPolicy> renderingPolicy,
        Func<ValueTask> completed)
    {
        InitializeComponent();
        _settings = settings;
        _shell = shell;
        _materials = materials;
        _startup = startup;
        _renderingPolicy = renderingPolicy;
        _completed = completed;
        Title = ProductBranding.OnboardingWindowTitle;
        ExtendsContentIntoTitleBar = true;
        if (Glass.Platform.Windows.Windowing.WinUiWindowHandle.FromWindow(this).AppWindow.Presenter
            is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
        }
        _materials.Apply(this, SurfaceChrome, settings.Current.Appearance.Material,
            settings.Current.Appearance.ThemeMode, GlassSurfaceRole.ControlCenter,
            _renderingPolicy());
        Activated += OnActivated;
    }

    private async void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= OnActivated;
        var state = await _startup.GetAsync();
        StartWithWindows.IsEnabled =
            state.Availability == Glass.Platform.Windows.Runtime.StartupTaskAvailability.Available;
        StartWithWindows.IsOn = state.IsEnabled;
        StartupAvailability.Text = StartWithWindows.IsEnabled
            ? "Windows will start Glass quietly after sign-in."
            : "Available in the packaged release. This development build will not modify startup.";
    }

    private void Back_Click(object sender, RoutedEventArgs args)
    {
        _step = Math.Max(0, _step - 1);
        ShowStep();
    }

    private async void Next_Click(object sender, RoutedEventArgs args)
    {
        if (_step < 4)
        {
            _step++;
            ShowStep();
            return;
        }
        await FinishAsync(useDefaults: false);
    }

    private async void Skip_Click(object sender, RoutedEventArgs args) =>
        await FinishAsync(useDefaults: true);

    private void ShowStep()
    {
        var steps = new[] { WelcomeStep, AppearanceStep, BarStep, StartupStep, DoneStep };
        for (var index = 0; index < steps.Length; index++)
            steps[index].Visibility = index == _step ? Visibility.Visible : Visibility.Collapsed;
        BackButton.IsEnabled = _step > 0;
        SkipButton.Visibility = _step == 4 ? Visibility.Collapsed : Visibility.Visible;
        NextButton.Content = _step == 4 ? "Open Control Center" : "Continue";
    }

    private async ValueTask FinishAsync(bool useDefaults)
    {
        if (!useDefaults)
        {
            var preset = AppearancePresets.BuiltIn[Math.Clamp(Preset.SelectedIndex, 0,
                AppearancePresets.BuiltIn.Count - 1)];
            await _settings.UpdateAsync(settings => settings with
            {
                Appearance = settings.Appearance with { Material = preset.Material },
                Behavior = settings.Behavior with
                {
                    OnboardingCompleted = true,
                    StartWithWindows = StartWithWindows.IsOn,
                },
            });
            var bar = _shell.Layout.Bars[0];
            var edge = (ScreenEdge)Math.Clamp(BarEdge.SelectedIndex, 0, 3);
            await _shell.UpdateBarAsync(bar with
            {
                Placement = new AnchoredPlacement(bar.Placement.Target, edge, 0,
                    new Glass.Core.Geometry.LogicalSize(bar.Length, bar.Thickness)),
                VisualMode = (BarVisualMode)Math.Clamp(BarStyle.SelectedIndex, 0, 2),
            });
            if (StartWithWindows.IsEnabled)
                await _startup.SetEnabledAsync(StartWithWindows.IsOn);
        }
        else
        {
            await _settings.UpdateAsync(settings => settings with
            {
                Behavior = settings.Behavior with { OnboardingCompleted = true },
            });
        }
        await _completed();
        Close();
    }
}
