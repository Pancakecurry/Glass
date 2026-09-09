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
    private void LoadAppearance()
    {
        _loading = true;
        var settings = _settings.Current.Normalize();
        var material = SelectedMaterial(settings);
        Select(ThemeMode, settings.Appearance.ThemeMode.ToString());
        Select(AccentMode, settings.Appearance.AccentPreference.ToString());
        AccentColor.Text = settings.Appearance.CustomAccentColor;
        TintColor.Text = material.TintColor;
        MaterialIntensity.Value = material.MaterialIntensity;
        TintStrength.Value = material.TintStrength;
        Luminosity.Value = material.Luminosity;
        BorderStrength.Value = material.BorderStrength;
        EdgeStrength.Value = material.EdgeHighlightStrength;
        ShadowStrength.Value = material.ShadowStrength;
        CornerRadius.Value = material.CornerRadius;
        OverallOpacity.Value = material.OverallOpacity;
        BarPadding.Value = settings.Appearance.BarPadding;
        ItemSpacing.Value = settings.Appearance.ItemSpacing;
        IconSize.Value = settings.Appearance.ApplicationIconSize;
        Select(WidgetDensity, settings.Appearance.WidgetDensity.ToString());
        Select(Magnification, settings.Appearance.Magnification.ToString());
        MaximumScale.Value = settings.Appearance.MagnificationMaximumScale;
        PopulatePresets(material.Preset);
        ResetAppearance.Content = _editMode.Selection is null
            ? "Reset global defaults" : "Reset to Global";
        _loading = false;
    }

    private void PopulatePresets(MaterialPreset current)
    {
        PresetSelector.Items.Clear();
        foreach (var preset in AppearancePresets.BuiltIn.Concat(_settings.Current.CustomPresets))
            PresetSelector.Items.Add(new ComboBoxItem { Content = preset.Name, Tag = preset });
        PresetSelector.SelectedItem = PresetSelector.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(item => item.Tag is AppearancePresetDefinition preset &&
                preset.Material.Preset == current);
    }

    private async void PresetSelector_SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (_loading || (PresetSelector.SelectedItem as ComboBoxItem)?.Tag is not AppearancePresetDefinition preset) return;
        await ApplyMaterialAsync(preset.Material);
    }
    private async void Appearance_Changed(object sender, object args)
    {
        if (_loading) return;
        if (ReferenceEquals(sender, ThemeMode) && Enum.TryParse<Glass.Core.Appearance.ThemeMode>(ThemeMode.SelectedItem as string, out var theme))
            await _settings.UpdateAsync(current => current with
            {
                Appearance = current.Appearance with { ThemeMode = theme },
            });
        else if (ReferenceEquals(sender, AccentMode) && Enum.TryParse<AccentPreference>(AccentMode.SelectedItem as string, out var accent))
            await _settings.UpdateAsync(current => current with
            {
                Appearance = current.Appearance with { AccentPreference = accent },
            });
        else if (ReferenceEquals(sender, AccentColor))
            await _settings.UpdateAsync(current => current with
            {
                Appearance = current.Appearance with { CustomAccentColor = AccentColor.Text },
            });
        else if (ReferenceEquals(sender, Magnification) && Enum.TryParse<MagnificationMode>(Magnification.SelectedItem as string, out var magnification))
            await _settings.UpdateAsync(current => current with
            {
                Appearance = current.Appearance with { Magnification = magnification },
            });
        else if (ReferenceEquals(sender, WidgetDensity) && Enum.TryParse<WidgetSurfaceDensity>(WidgetDensity.SelectedItem as string, out var density))
            await _settings.UpdateAsync(current => current with
            {
                Appearance = current.Appearance with { WidgetDensity = density },
            });
        else if (ReferenceEquals(sender, TintColor))
            await ApplyMaterialOverrideAsync(value => value with { TintColor = TintColor.Text },
                value => value with { TintColor = TintColor.Text });
    }
    private async void AppearanceSlider_Changed(object sender, RangeBaseValueChangedEventArgs args)
    {
        if (_loading) return;
        if (ReferenceEquals(sender, BarPadding) || ReferenceEquals(sender, ItemSpacing) ||
            ReferenceEquals(sender, IconSize) || ReferenceEquals(sender, MaximumScale))
        {
            await _settings.UpdateAsync(current => current with
            {
                Appearance = current.Appearance with
                {
                    BarPadding = BarPadding.Value,
                    ItemSpacing = ItemSpacing.Value,
                    ApplicationIconSize = IconSize.Value,
                    MagnificationMaximumScale = MaximumScale.Value,
                },
            });
            return;
        }
        await ApplyMaterialOverrideAsync(material => sender switch
        {
            Slider value when ReferenceEquals(value, MaterialIntensity) => material with { MaterialIntensity = value.Value },
            Slider value when ReferenceEquals(value, TintStrength) => material with { TintStrength = value.Value },
            Slider value when ReferenceEquals(value, Luminosity) => material with { Luminosity = value.Value },
            Slider value when ReferenceEquals(value, BorderStrength) => material with { BorderStrength = value.Value },
            Slider value when ReferenceEquals(value, EdgeStrength) => material with { EdgeHighlightStrength = value.Value },
            Slider value when ReferenceEquals(value, ShadowStrength) => material with { ShadowStrength = value.Value },
            Slider value when ReferenceEquals(value, CornerRadius) => material with { CornerRadius = value.Value },
            Slider value when ReferenceEquals(value, OverallOpacity) => material with { OverallOpacity = value.Value },
            _ => material,
        }, value => sender switch
        {
            Slider slider when ReferenceEquals(slider, MaterialIntensity) => value with { MaterialIntensity = slider.Value },
            Slider slider when ReferenceEquals(slider, TintStrength) => value with { TintStrength = slider.Value },
            Slider slider when ReferenceEquals(slider, Luminosity) => value with { Luminosity = slider.Value },
            Slider slider when ReferenceEquals(slider, BorderStrength) => value with { BorderStrength = slider.Value },
            Slider slider when ReferenceEquals(slider, EdgeStrength) => value with { EdgeHighlightStrength = slider.Value },
            Slider slider when ReferenceEquals(slider, ShadowStrength) => value with { ShadowStrength = slider.Value },
            Slider slider when ReferenceEquals(slider, CornerRadius) => value with { CornerRadius = slider.Value },
            Slider slider when ReferenceEquals(slider, OverallOpacity) => value with { OverallOpacity = slider.Value },
            _ => value,
        });
    }

    private async Task ApplyMaterialAsync(MaterialSettings material)
    {
        if (_editMode.Selection is not { } selection)
        {
            await _settings.UpdateAsync(current => current with
            {
                Appearance = current.Appearance with { Material = material },
            });
            return;
        }
        var full = new MaterialOverride
        {
            Preset = material.Preset,
            TintColor = material.TintColor,
            TintStrength = material.TintStrength,
            MaterialIntensity = material.MaterialIntensity,
            Luminosity = material.Luminosity,
            BorderStrength = material.BorderStrength,
            EdgeHighlightStrength = material.EdgeHighlightStrength,
            ShadowStrength = material.ShadowStrength,
            CornerRadius = material.CornerRadius,
            OverallOpacity = material.OverallOpacity,
        };
        await UpdateOverrideAsync(selection, _ => full);
    }

    private async Task ApplyMaterialOverrideAsync(
        Func<MaterialSettings, MaterialSettings> global,
        Func<MaterialOverride, MaterialOverride> surface)
    {
        if (_editMode.Selection is { } selection)
            await UpdateOverrideAsync(selection, surface);
        else
            await _settings.UpdateAsync(current => current with
            {
                Appearance = current.Appearance with
                {
                    Material = global(current.Appearance.Material),
                },
            });
    }

    private async Task UpdateOverrideAsync(
        EditSelection selection,
        Func<MaterialOverride, MaterialOverride> update)
    {
        await _settings.UpdateAsync(current =>
        {
            var source = selection.Kind == EditableSurfaceKind.Bar
                ? current.BarAppearanceOverrides : current.WidgetAppearanceOverrides;
            var values = source.ToDictionary(pair => pair.Key, pair => pair.Value);
            values.TryGetValue(selection.Id, out var existing);
            values[selection.Id] = update(existing ?? new MaterialOverride());
            return selection.Kind == EditableSurfaceKind.Bar
                ? current with { BarAppearanceOverrides = values }
                : current with { WidgetAppearanceOverrides = values };
        });
    }

    private async void SavePreset_Click(object sender, RoutedEventArgs args)
    {
        var name = await RequestNameAsync("Save appearance preset", "My Glass preset");
        if (name is null) return;
        await _settings.UpdateAsync(current => current with
        {
            CustomPresets = [.. current.CustomPresets,
                new AppearancePresetDefinition(Guid.NewGuid(), name,
                    SelectedMaterial(current) with { Preset = MaterialPreset.Custom }, false)],
        });
    }
    private async void DuplicatePreset_Click(object sender, RoutedEventArgs args)
    {
        if ((PresetSelector.SelectedItem as ComboBoxItem)?.Tag is not AppearancePresetDefinition preset) return;
        var name = await RequestNameAsync("Duplicate preset", $"{preset.Name} Copy");
        if (name is null) return;
        await _settings.UpdateAsync(current => current with
        {
            CustomPresets = [.. current.CustomPresets,
                new AppearancePresetDefinition(Guid.NewGuid(), name,
                    preset.Material with { Preset = MaterialPreset.Custom }, false)],
        });
    }
    private async void RenamePreset_Click(object sender, RoutedEventArgs args)
    {
        if ((PresetSelector.SelectedItem as ComboBoxItem)?.Tag is not
            AppearancePresetDefinition { IsBuiltIn: false } preset) return;
        var name = await RequestNameAsync("Rename preset", preset.Name);
        if (name is null) return;
        await _settings.UpdateAsync(current => current with
        {
            CustomPresets = current.CustomPresets
                .Select(item => item.PresetId == preset.PresetId ? item with { Name = name } : item)
                .ToArray(),
        });
    }
    private async void DeletePreset_Click(object sender, RoutedEventArgs args)
    {
        if ((PresetSelector.SelectedItem as ComboBoxItem)?.Tag is not AppearancePresetDefinition { IsBuiltIn: false } preset) return;
        await _settings.UpdateAsync(current => current with
        {
            CustomPresets = current.CustomPresets.Where(item => item.PresetId != preset.PresetId).ToArray(),
        });
    }
    private async void ResetAppearance_Click(object sender, RoutedEventArgs args)
    {
        if (_editMode.Selection is { } selection)
        {
            await _settings.UpdateAsync(current =>
            {
                var bars = current.BarAppearanceOverrides.Where(pair => pair.Key != selection.Id)
                    .ToDictionary(pair => pair.Key, pair => pair.Value);
                var widgets = current.WidgetAppearanceOverrides.Where(pair => pair.Key != selection.Id)
                    .ToDictionary(pair => pair.Key, pair => pair.Value);
                return current with
                {
                    BarAppearanceOverrides = selection.Kind == EditableSurfaceKind.Bar ? bars : current.BarAppearanceOverrides,
                    WidgetAppearanceOverrides = selection.Kind == EditableSurfaceKind.Widget ? widgets : current.WidgetAppearanceOverrides,
                };
            });
        }
        else await _settings.UpdateAsync(current => current with { Appearance = new GlobalAppearanceSettings() });
    }

}
