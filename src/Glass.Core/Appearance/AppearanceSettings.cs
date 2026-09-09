namespace Glass.Core.Appearance;

public enum ThemeMode { System, Light, Dark }

public enum MaterialPreset { Clear, Frost, Smoke, Crystal, Solid, Custom }

public enum MotionPreference { System, Full, Reduced }

public enum AccentPreference { System, Custom }

public enum MagnificationMode { Off, Subtle, Expressive, Custom }

public enum WidgetSurfaceDensity { Compact, Comfortable, Spacious }

public enum BarVisualMode { Unified, Segmented, Minimal }

public enum RenderingQualityPreference { Auto, Full, Balanced, Reduced, Solid }

public sealed record MaterialSettings(
    MaterialPreset Preset,
    string TintColor,
    double TintStrength,
    double MaterialIntensity,
    double Luminosity,
    double BorderStrength,
    double EdgeHighlightStrength,
    double ShadowStrength,
    double CornerRadius,
    double OverallOpacity)
{
    public static MaterialSettings GlassClear { get; } = new(
        MaterialPreset.Clear, "#F4F7FA", 0.10, 0.72, 0.92, 0.20, 0.28, 0.24, 20, 1);

    public MaterialSettings Normalize() => this with
    {
        TintColor = ColorValue.Normalize(TintColor, "#F4F7FA"),
        TintStrength = Unit(TintStrength, 0.10),
        MaterialIntensity = Unit(MaterialIntensity, 0.72),
        Luminosity = Unit(Luminosity, 0.92),
        BorderStrength = Unit(BorderStrength, 0.20),
        EdgeHighlightStrength = Unit(EdgeHighlightStrength, 0.28),
        ShadowStrength = Unit(ShadowStrength, 0.24),
        CornerRadius = Math.Clamp(Finite(CornerRadius, 20), 0, 40),
        OverallOpacity = Math.Clamp(Finite(OverallOpacity, 1), 0.45, 1),
    };

    private static double Unit(double value, double fallback) =>
        Math.Clamp(Finite(value, fallback), 0, 1);

    private static double Finite(double value, double fallback) =>
        double.IsFinite(value) ? value : fallback;
}

public sealed record MaterialOverride
{
    public MaterialPreset? Preset { get; init; }
    public string? TintColor { get; init; }
    public double? TintStrength { get; init; }
    public double? MaterialIntensity { get; init; }
    public double? Luminosity { get; init; }
    public double? BorderStrength { get; init; }
    public double? EdgeHighlightStrength { get; init; }
    public double? ShadowStrength { get; init; }
    public double? CornerRadius { get; init; }
    public double? OverallOpacity { get; init; }

    public bool IsEmpty => Preset is null && TintColor is null && TintStrength is null &&
        MaterialIntensity is null && Luminosity is null && BorderStrength is null &&
        EdgeHighlightStrength is null && ShadowStrength is null && CornerRadius is null &&
        OverallOpacity is null;

    public MaterialSettings ApplyTo(MaterialSettings inherited) => (inherited with
    {
        Preset = Preset ?? inherited.Preset,
        TintColor = TintColor ?? inherited.TintColor,
        TintStrength = TintStrength ?? inherited.TintStrength,
        MaterialIntensity = MaterialIntensity ?? inherited.MaterialIntensity,
        Luminosity = Luminosity ?? inherited.Luminosity,
        BorderStrength = BorderStrength ?? inherited.BorderStrength,
        EdgeHighlightStrength = EdgeHighlightStrength ?? inherited.EdgeHighlightStrength,
        ShadowStrength = ShadowStrength ?? inherited.ShadowStrength,
        CornerRadius = CornerRadius ?? inherited.CornerRadius,
        OverallOpacity = OverallOpacity ?? inherited.OverallOpacity,
    }).Normalize();
}

public sealed record GlobalAppearanceSettings
{
    public ThemeMode ThemeMode { get; init; } = ThemeMode.System;
    public AccentPreference AccentPreference { get; init; } = AccentPreference.System;
    public string CustomAccentColor { get; init; } = "#6EA8FF";
    public MotionPreference MotionPreference { get; init; } = MotionPreference.System;
    public MaterialSettings Material { get; init; } = MaterialSettings.GlassClear;
    public double BarPadding { get; init; } = 8;
    public double ItemSpacing { get; init; } = 6;
    public double ApplicationIconSize { get; init; } = 32;
    public MagnificationMode Magnification { get; init; } = MagnificationMode.Subtle;
    public double MagnificationMaximumScale { get; init; } = 1.18;
    public WidgetSurfaceDensity WidgetDensity { get; init; } = WidgetSurfaceDensity.Comfortable;

    public GlobalAppearanceSettings Normalize() => this with
    {
        CustomAccentColor = ColorValue.Normalize(CustomAccentColor, "#6EA8FF"),
        Material = (Material ?? MaterialSettings.GlassClear).Normalize(),
        BarPadding = Math.Clamp(Finite(BarPadding, 8), 2, 24),
        ItemSpacing = Math.Clamp(Finite(ItemSpacing, 6), 0, 20),
        ApplicationIconSize = Math.Clamp(Finite(ApplicationIconSize, 32), 20, 48),
        MagnificationMaximumScale = Math.Clamp(Finite(MagnificationMaximumScale, 1.18), 1, 1.35),
    };

    private static double Finite(double value, double fallback) =>
        double.IsFinite(value) ? value : fallback;
}

public sealed record TaskbarInteractionSettings
{
    public bool ActivateSingleWindowDirectly { get; init; } = true;
    public bool ToggleForegroundWindowMinimize { get; init; } = true;
    public bool ShowRunningIndicators { get; init; } = true;
    public bool ShowTooltips { get; init; } = true;
    public bool RespectFullscreenApplications { get; init; } = true;
}

public sealed record ProductBehaviorSettings
{
    public RenderingQualityPreference RenderingQuality { get; init; } =
        RenderingQualityPreference.Auto;
    public bool StartWithWindows { get; init; }
    public bool OnboardingCompleted { get; init; }
}

public sealed record AppearancePresetDefinition(
    Guid PresetId,
    string Name,
    MaterialSettings Material,
    bool IsBuiltIn)
{
    public AppearancePresetDefinition Normalize() => this with
    {
        Name = string.IsNullOrWhiteSpace(Name) ? "Custom preset" : Name.Trim(),
        Material = (Material ?? MaterialSettings.GlassClear).Normalize(),
    };
}

public sealed record GlassSettings
{
    public GlobalAppearanceSettings Appearance { get; init; } = new();
    public TaskbarInteractionSettings Taskbar { get; init; } = new();
    public ProductBehaviorSettings Behavior { get; init; } = new();
    public IReadOnlyDictionary<Guid, MaterialOverride> BarAppearanceOverrides { get; init; } =
        new Dictionary<Guid, MaterialOverride>();
    public IReadOnlyDictionary<Guid, MaterialOverride> WidgetAppearanceOverrides { get; init; } =
        new Dictionary<Guid, MaterialOverride>();
    public IReadOnlyList<AppearancePresetDefinition> CustomPresets { get; init; } = [];

    public GlassSettings Normalize() => this with
    {
        Appearance = (Appearance ?? new GlobalAppearanceSettings()).Normalize(),
        Taskbar = Taskbar ?? new TaskbarInteractionSettings(),
        Behavior = Behavior ?? new ProductBehaviorSettings(),
        BarAppearanceOverrides = NormalizeOverrides(BarAppearanceOverrides),
        WidgetAppearanceOverrides = NormalizeOverrides(WidgetAppearanceOverrides),
        CustomPresets = (CustomPresets ?? [])
            .Where(preset => preset is not null && !preset.IsBuiltIn)
            .GroupBy(preset => preset.PresetId)
            .Select(group => group.First().Normalize())
            .ToArray(),
    };

    private static IReadOnlyDictionary<Guid, MaterialOverride> NormalizeOverrides(
        IReadOnlyDictionary<Guid, MaterialOverride>? source) =>
        (source ?? new Dictionary<Guid, MaterialOverride>())
            .Where(pair => pair.Key != Guid.Empty && pair.Value is not null && !pair.Value.IsEmpty)
            .ToDictionary(pair => pair.Key, pair => pair.Value);
}

public static class AppearanceResolver
{
    public static MaterialSettings ForBar(GlassSettings settings, Guid barId) =>
        Resolve(settings, settings.BarAppearanceOverrides, barId);

    public static MaterialSettings ForWidget(GlassSettings settings, Guid widgetId) =>
        Resolve(settings, settings.WidgetAppearanceOverrides, widgetId);

    private static MaterialSettings Resolve(
        GlassSettings settings,
        IReadOnlyDictionary<Guid, MaterialOverride> overrides,
        Guid id)
    {
        var global = settings.Normalize().Appearance.Material;
        return overrides.TryGetValue(id, out var value) ? value.ApplyTo(global) : global;
    }
}

internal static class ColorValue
{
    public static string Normalize(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        var candidate = value.Trim().ToUpperInvariant();
        if (candidate.Length is not (7 or 9) || candidate[0] != '#') return fallback;
        return candidate[1..].All(Uri.IsHexDigit) ? candidate : fallback;
    }
}
