using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;

namespace Glass.Rendering.Composition;

public static class CompositionAnimator
{
    private static readonly Vector3 RestScale = Vector3.One;
    private static readonly Vector3 EmphasisScale = new(1.08f, 1.08f, 1f);

    public static void PrepareScale(UIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        var visual = ElementCompositionPreview.GetElementVisual(element);
        visual.CenterPoint = new Vector3(
            (float)element.ActualSize.X / 2f,
            (float)element.ActualSize.Y / 2f,
            0f);
        visual.Scale = RestScale;
    }

    public static void AnimateEmphasis(UIElement element, bool emphasized)
    {
        ArgumentNullException.ThrowIfNull(element);

        var visual = ElementCompositionPreview.GetElementVisual(element);
        visual.CenterPoint = new Vector3(
            (float)element.ActualSize.X / 2f,
            (float)element.ActualSize.Y / 2f,
            0f);

        var animation = visual.Compositor.CreateSpringVector3Animation();
        animation.FinalValue = emphasized ? EmphasisScale : RestScale;
        animation.DampingRatio = 0.82f;
        animation.Period = TimeSpan.FromMilliseconds(70);
        visual.StartAnimation(nameof(visual.Scale), animation);
    }
}
