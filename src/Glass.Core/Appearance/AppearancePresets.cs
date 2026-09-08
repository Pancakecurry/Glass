namespace Glass.Core.Appearance;

public static class AppearancePresets
{
    public static IReadOnlyList<AppearancePresetDefinition> BuiltIn { get; } =
    [
        Preset("4de455f2-379e-43bd-93a3-6e4624f85901", "Glass Clear",
            MaterialSettings.GlassClear),
        Preset("ce48e0ee-cdee-48d1-8f3b-3a85fd4f3150", "Glass Frost",
            new(MaterialPreset.Frost, "#EEF2F6", 0.20, 0.86, 0.94, 0.24, 0.18, 0.22, 20, 1)),
        Preset("e907c759-6f8d-4078-b839-e511b168c69b", "Glass Smoke",
            new(MaterialPreset.Smoke, "#20242A", 0.34, 0.82, 0.62, 0.22, 0.16, 0.28, 20, 1)),
        Preset("54d34a0a-21bd-4a07-9125-6c8a406917a3", "Glass Crystal",
            new(MaterialPreset.Crystal, "#F8FBFF", 0.08, 0.64, 1, 0.26, 0.42, 0.26, 18, 1)),
        Preset("f08ecf19-8221-4a6d-89e5-f7fc7dfef834", "Minimal Solid",
            new(MaterialPreset.Solid, "#202124", 1, 1, 0.64, 0.12, 0, 0.14, 14, 1)),
    ];

    public static MaterialSettings Resolve(Guid presetId) =>
        BuiltIn.FirstOrDefault(preset => preset.PresetId == presetId)?.Material ??
        MaterialSettings.GlassClear;

    private static AppearancePresetDefinition Preset(
        string id,
        string name,
        MaterialSettings material) =>
        new(Guid.Parse(id), name, material.Normalize(), true);
}
