namespace Glass.Core.Shell;

public readonly record struct SurfaceId(Guid Value)
{
    public static SurfaceId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}
