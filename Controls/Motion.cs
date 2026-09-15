using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;

namespace PrivateTimeTrace.Controls;

internal static class Motion
{
    private static readonly HashSet<ButtonBase> Attached = new();
    public static void Wire(DependencyObject root)
    {
        if (root is ButtonBase button && Attached.Add(button))
        {
            button.PointerPressed += (_, _) => Scale(button, .97f);
            button.PointerReleased += (_, _) => Scale(button, 1);
            button.PointerCaptureLost += (_, _) => Scale(button, 1);
            button.PointerExited += (_, _) => Scale(button, 1);
            button.Unloaded += (_, _) => Attached.Remove(button);
        }
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) Wire(VisualTreeHelper.GetChild(root, i));
    }
    private static void Scale(FrameworkElement element, float scale)
    {
        if (!element.IsLoaded || App.ReducedEffects) return;
        var visual = ElementCompositionPreview.GetElementVisual(element);
        visual.CenterPoint = new Vector3((float)element.ActualWidth / 2, (float)element.ActualHeight / 2, 0);
        var animation = visual.Compositor.CreateSpringVector3Animation();
        animation.FinalValue = new Vector3(scale, scale, 1);
        animation.DampingRatio = .78f;
        animation.Period = TimeSpan.FromMilliseconds(240);
        visual.StartAnimation("Scale", animation);
    }
    public static void Reveal(UIElement element)
    {
        if (App.ReducedEffects || element is FrameworkElement { IsLoaded: false }) return;
        var visual = ElementCompositionPreview.GetElementVisual(element);
        var animation = visual.Compositor.CreateScalarKeyFrameAnimation();
        animation.InsertKeyFrame(0, .15f);
        animation.InsertKeyFrame(1, 1);
        animation.Duration = TimeSpan.FromMilliseconds(240);
        visual.StartAnimation("Opacity", animation);
    }
}
