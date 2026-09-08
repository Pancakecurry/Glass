namespace Glass.Core.Appearance;

public enum MotionIntent
{
    Hover,
    Press,
    Appear,
    Dismiss,
    Snap,
    Reveal,
    AutoHide,
    SurfaceLift,
    Flyout,
    WidgetStateTransition,
}

public readonly record struct MotionSpec(TimeSpan Duration, bool UsesSpring, double Scale)
{
    public bool IsImmediate => Duration == TimeSpan.Zero;
}

public static class MotionPolicy
{
    public static MotionSpec Resolve(
        MotionIntent intent,
        MotionPreference preference,
        bool systemAnimationsEnabled)
    {
        var reduced = preference == MotionPreference.Reduced ||
            (preference == MotionPreference.System && !systemAnimationsEnabled);
        if (reduced)
        {
            return intent is MotionIntent.Appear or MotionIntent.Dismiss or MotionIntent.Flyout
                ? new MotionSpec(TimeSpan.FromMilliseconds(80), false, 1)
                : new MotionSpec(TimeSpan.Zero, false, 1);
        }

        return intent switch
        {
            MotionIntent.Hover => new(TimeSpan.FromMilliseconds(120), false, 1.04),
            MotionIntent.Press => new(TimeSpan.FromMilliseconds(75), false, 0.96),
            MotionIntent.Snap => new(TimeSpan.FromMilliseconds(190), true, 1),
            MotionIntent.AutoHide or MotionIntent.Reveal =>
                new(TimeSpan.FromMilliseconds(220), true, 1),
            MotionIntent.SurfaceLift => new(TimeSpan.FromMilliseconds(160), true, 1.015),
            MotionIntent.Flyout => new(TimeSpan.FromMilliseconds(150), false, 1),
            MotionIntent.WidgetStateTransition => new(TimeSpan.FromMilliseconds(180), false, 1),
            _ => new(TimeSpan.FromMilliseconds(180), false, 1),
        };
    }

    public static double MagnificationScale(
        MagnificationMode mode,
        double configuredMaximum,
        bool systemAnimationsEnabled)
    {
        if (!systemAnimationsEnabled || mode == MagnificationMode.Off) return 1;
        return mode switch
        {
            MagnificationMode.Subtle => 1.14,
            MagnificationMode.Expressive => 1.28,
            _ => Math.Clamp(double.IsFinite(configuredMaximum) ? configuredMaximum : 1.18, 1, 1.35),
        };
    }
}
