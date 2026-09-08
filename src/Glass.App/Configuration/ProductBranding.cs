namespace Glass.App.Configuration;

/// <summary>
/// Single location for the eventual public product name and app-facing strings.
/// Project and namespace names intentionally remain Glass until the codename changes.
/// </summary>
internal static class ProductBranding
{
    public const string EngineeringCodename = "Glass";
    public const string TechnicalSpikeWindowTitle =
        EngineeringCodename + " — Windows Technical Spike";

    public const string TechnicalProbeWindowTitle =
        EngineeringCodename + " — Technical Probe";
}
