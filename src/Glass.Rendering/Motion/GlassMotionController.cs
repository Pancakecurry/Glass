using System.Numerics;
using Glass.Core.Appearance;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;

namespace Glass.Rendering.Motion;

public sealed class GlassMotionController(
    MotionPreference preference,
    bool systemAnimationsEnabled)
{
    public MotionPreference Preference { get; set; } = preference;
    public bool SystemAnimationsEnabled { get; set; } = systemAnimationsEnabled;
    public bool RuntimeAllowsFullMotion { get; set; } = true;

    public void AnimateScale(FrameworkElement element, MotionIntent intent, double? scale = null)
    {
        var visual = Prepare(element);
        var spec = MotionPolicy.Resolve(intent, Preference, EffectiveAnimations);
        var target = (float)(scale ?? spec.Scale);
        if (spec.IsImmediate)
        {
            visual.Scale = new Vector3(target, target, 1);
            return;
        }

        if (spec.UsesSpring)
        {
            var spring = visual.Compositor.CreateSpringVector3Animation();
            spring.FinalValue = new Vector3(target, target, 1);
            spring.DampingRatio = 0.88f;
            spring.Period = TimeSpan.FromMilliseconds(120);
            visual.StartAnimation(nameof(visual.Scale), spring);
            return;
        }

        var animation = visual.Compositor.CreateVector3KeyFrameAnimation();
        animation.Duration = spec.Duration;
        animation.InsertKeyFrame(1, new Vector3(target, target, 1),
            StandardEasing(visual.Compositor));
        visual.StartAnimation(nameof(visual.Scale), animation);
    }

    public void AnimateOpacity(FrameworkElement element, MotionIntent intent, float target)
    {
        var visual = Prepare(element);
        var spec = MotionPolicy.Resolve(intent, Preference, EffectiveAnimations);
        if (spec.IsImmediate)
        {
            visual.Opacity = target;
            return;
        }
        var animation = visual.Compositor.CreateScalarKeyFrameAnimation();
        animation.Duration = spec.Duration;
        animation.InsertKeyFrame(1, target, StandardEasing(visual.Compositor));
        visual.StartAnimation(nameof(visual.Opacity), animation);
    }

    public void AnimateTranslation(
        FrameworkElement element,
        MotionIntent intent,
        Vector3 target)
    {
        var visual = Prepare(element);
        var spec = MotionPolicy.Resolve(intent, Preference, EffectiveAnimations);
        if (spec.IsImmediate)
        {
            visual.Offset = target;
            return;
        }
        if (spec.UsesSpring)
        {
            var spring = visual.Compositor.CreateSpringVector3Animation();
            spring.FinalValue = target;
            spring.DampingRatio = 0.9f;
            spring.Period = TimeSpan.FromMilliseconds(135);
            visual.StartAnimation(nameof(visual.Offset), spring);
            return;
        }
        var animation = visual.Compositor.CreateVector3KeyFrameAnimation();
        animation.Duration = spec.Duration;
        animation.InsertKeyFrame(1, target, StandardEasing(visual.Compositor));
        visual.StartAnimation(nameof(visual.Offset), animation);
    }

    public void ApplyMagnification(
        IReadOnlyList<FrameworkElement> items,
        int activeIndex,
        MagnificationMode mode,
        double configuredMaximum)
    {
        var enabled = Preference != MotionPreference.Reduced && EffectiveAnimations;
        var maximum = MotionPolicy.MagnificationScale(mode, configuredMaximum, enabled);
        for (var index = 0; index < items.Count; index++)
        {
            var distance = Math.Abs(index - activeIndex);
            var scale = distance switch
            {
                0 => maximum,
                1 => 1 + ((maximum - 1) * 0.44),
                _ => 1,
            };
            AnimateScale(items[index], MotionIntent.Hover, scale);
        }
    }

    public void ResetMagnification(IReadOnlyList<FrameworkElement> items)
    {
        foreach (var item in items) AnimateScale(item, MotionIntent.Hover, 1);
    }

    private bool EffectiveAnimations =>
        SystemAnimationsEnabled && RuntimeAllowsFullMotion;

    private static Visual Prepare(FrameworkElement element)
    {
        var visual = ElementCompositionPreview.GetElementVisual(element);
        visual.CenterPoint = new Vector3(
            (float)(element.ActualWidth / 2),
            (float)(element.ActualHeight / 2), 0);
        return visual;
    }

    private static CubicBezierEasingFunction StandardEasing(Compositor compositor) =>
        compositor.CreateCubicBezierEasingFunction(
            new Vector2(0.2f, 0), new Vector2(0, 1));
}
