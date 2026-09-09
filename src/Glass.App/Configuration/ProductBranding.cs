namespace Glass.App.Configuration;

/// <summary>
/// Single location for the eventual public product name and app-facing strings.
/// Project and namespace names intentionally remain Glass until the codename changes.
/// </summary>
internal static class ProductBranding
{
    public const string EngineeringCodename = "Glass";
    public const string DevelopmentControlsWindowTitle =
        EngineeringCodename + " — Development Shell Controls";

    public const string ControlCenterWindowTitle = EngineeringCodename + " Control Center";

    public const string BarWindowTitle = EngineeringCodename + " bar";
}
