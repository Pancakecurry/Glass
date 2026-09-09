namespace Glass.Core.Product;

public readonly record struct ProductVersion(int Major, int Minor, int Patch, int Revision)
{
    public static ProductVersion Current { get; } = FromAssembly();

    public static bool TryParse(string? value, out ProductVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var components = value.Split('.');
        if (components.Length is < 3 or > 4 || components.Any(component =>
            !ushort.TryParse(component, out _))) return false;
        var parsed = components.Select(int.Parse).ToArray();
        version = new(parsed[0], parsed[1], parsed[2], parsed.Length == 4 ? parsed[3] : 0);
        return true;
    }

    public string Informational => $"{Major}.{Minor}.{Patch}";
    public string Package => $"{Major}.{Minor}.{Patch}.{Revision}";
    public override string ToString() => Informational;

    private static ProductVersion FromAssembly()
    {
        var assembly = typeof(ProductVersion).Assembly.GetName().Version;
        return assembly is null
            ? default
            : new ProductVersion(assembly.Major, assembly.Minor,
                Math.Max(0, assembly.Build), Math.Max(0, assembly.Revision));
    }
}
