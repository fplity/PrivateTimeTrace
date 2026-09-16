using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Windows.Foundation;

namespace PrivateTimeTrace.Controls;

/// <summary>Independent optical layers; content and hit targets never move.</summary>
internal sealed class LiquidLight : IDisposable
{
    private readonly FrameworkElement _host;
    private readonly ContainerVisual _root;
    private readonly CompositionPropertySet _state;
    private readonly CompositionRoundedRectangleGeometry _clipGeometry;
    private readonly CompositionRoundedRectangleGeometry _rimGeometry;
    private readonly List<CompositionObject> _animated = new();
    private readonly float _radius;
    private bool _disposed;
    private bool _liquid = true;
    private bool _active;
    private Vector2 _lastPointer = new(-10);

    public LiquidLight(FrameworkElement host, float radius = 22, bool compact = false)
    {
        _host = host;
        _radius = radius;
        var compositor = ElementCompositionPreview.GetElementVisual(host).Compositor;
        _root = compositor.CreateContainerVisual();
        _root.RelativeSizeAdjustment = Vector2.One;
        _root.Opacity = App.ReducedEffects ? 0 : .32f;
        _state = compositor.CreatePropertySet();
        _state.InsertVector2("Pointer", new Vector2(.3f, .2f));
        _clipGeometry = compositor.CreateRoundedRectangleGeometry();
        _clipGeometry.CornerRadius = new Vector2(radius);
        _root.Clip = compositor.CreateGeometricClip(_clipGeometry);

        var pool = Radial(compositor, compact ? new Vector2(.8f, 1.8f) : new Vector2(.66f, 1.05f),
            (0, "#B0FFFFFF"), (.32f, "#50FFFFFF"), (1, "#00FFFFFF"));
        Follow(pool, "EllipseCenter", "light.Pointer");
        AddLayer(pool, compact ? .9f : .66f);
        var shade = Radial(compositor, new Vector2(.85f, 1.1f),
            (0, "#28245C97"), (.45f, "#153B83B4"), (1, "#003B83B4"));
        Follow(shade, "EllipseCenter", "Vector2(1.1, 1.0) - light.Pointer * 0.55");
        AddLayer(shade, .65f);
        var spectrum = Radial(compositor, new Vector2(.75f, 1.1f),
            (0, "#51BBB9FF"), (.35f, "#2599DCFF"), (1, "#0099DCFF"));
        Follow(spectrum, "EllipseCenter", "Vector2(0.8, 0.85) - light.Pointer * 0.38");
        AddLayer(spectrum, compact ? .65f : .5f);
        // A curved, narrow caustic moves separately from the broad incident light.
        var caustic = Radial(compositor, new Vector2(.85f, .76f),
            (0, "#00FFFFFF"), (.73f, "#00FFFFFF"), (.87f, "#48FFFFFF"), (.92f, "#14B7E2FF"), (1, "#00FFFFFF"));
        Follow(caustic, "EllipseCenter", "Vector2(0.12, -0.28) + light.Pointer * Vector2(0.55, 0.6)");
        AddLayer(caustic, compact ? .85f : .8f);
        var rimLight = Radial(compositor, new Vector2(.6f, .9f),
            (0, "#FFFFFFFF"), (.25f, "#EEFFFFFF"), (.6f, "#72D5EBFF"), (1, "#00FFFFFF"));
        Follow(rimLight, "EllipseCenter", "light.Pointer");
        _rimGeometry = compositor.CreateRoundedRectangleGeometry();
        _rimGeometry.Offset = new Vector2(1);
        _rimGeometry.CornerRadius = new Vector2(Math.Max(0, radius - 1));
        var rim = compositor.CreateSpriteShape(_rimGeometry);
        rim.StrokeBrush = rimLight;
        rim.StrokeThickness = compact ? 1.6f : 1.8f;
        var rimVisual = compositor.CreateShapeVisual();
        rimVisual.RelativeSizeAdjustment = Vector2.One;
        rimVisual.Shapes.Add(rim);
        _root.Children.InsertAtTop(rimVisual);
        ElementCompositionPreview.SetElementChildVisual(host, _root);
        host.SizeChanged += SizeChanged;
        Resize();
    }

    private static CompositionRadialGradientBrush Radial(Compositor compositor, Vector2 radius, params (float Offset, string Color)[] stops)
    {
        var brush = compositor.CreateRadialGradientBrush();
        brush.EllipseRadius = radius;
        foreach (var stop in stops) brush.ColorStops.Add(compositor.CreateColorGradientStop(stop.Offset, GlassPanel.Parse(stop.Color)));
        return brush;
    }
    private void Follow(CompositionObject target, string property, string expression)
    {
        using var animation = _root.Compositor.CreateExpressionAnimation(expression);
        animation.SetReferenceParameter("light", _state);
        target.StartAnimation(property, animation);
        _animated.Add(target);
    }
    private void AddLayer(CompositionBrush brush, float opacity)
    {
        var visual = _root.Compositor.CreateSpriteVisual();
        visual.RelativeSizeAdjustment = Vector2.One;
        visual.Brush = brush;
        visual.Opacity = opacity;
        _root.Children.InsertAtTop(visual);
    }
    public void SetMaterial(bool liquid)
    {
        if (_disposed) return;
        _liquid = liquid;
        if (App.ReducedEffects)
        {
            _state.StopAnimation("Pointer");
            _root.StopAnimation("Opacity");
            _root.Opacity = 0;
            return;
        }
        Fade(_active);
    }
    public void Track(Point point, bool active = true)
    {
        if (_disposed || !_host.IsLoaded || App.ReducedEffects) return;
        var pointer = new Vector2((float)Math.Clamp(point.X / Math.Max(1, _host.ActualWidth), -.4, 1.4),
            (float)Math.Clamp(point.Y / Math.Max(1, _host.ActualHeight), -.4, 1.4));
        if (Vector2.DistanceSquared(pointer, _lastPointer) > .00002f)
        {
            _lastPointer = pointer;
            using var animation = _root.Compositor.CreateVector2KeyFrameAnimation();
            using var easing = _root.Compositor.CreateCubicBezierEasingFunction(new Vector2(.16f, 1), Vector2.One);
            // No frame zero: rapid input retargets from the currently presented position.
            animation.InsertKeyFrame(1, pointer, easing);
            animation.Duration = TimeSpan.FromMilliseconds(180);
            _state.StartAnimation("Pointer", animation);
        }
        if (_active != active) Fade(active);
    }
    public void Rest() { if (!_disposed && !App.ReducedEffects) Fade(false); }
    private void Fade(bool active)
    {
        _active = active;
        using var animation = _root.Compositor.CreateScalarKeyFrameAnimation();
        animation.InsertKeyFrame(1, (_liquid ? 1 : .24f) * (active ? 1 : .32f));
        animation.Duration = TimeSpan.FromMilliseconds(active ? 160 : 460);
        _root.StartAnimation("Opacity", animation);
    }
    private void SizeChanged(object sender, SizeChangedEventArgs args) => Resize();
    private void Resize()
    {
        if (_disposed || App.IsClosing) return;
        var size = new Vector2((float)_host.ActualWidth, (float)_host.ActualHeight);
        _clipGeometry.Size = Vector2.Max(Vector2.Zero, size);
        _rimGeometry.Size = Vector2.Max(Vector2.Zero, size - new Vector2(2));
        var radius = Math.Max(0, Math.Min(_radius, Math.Min(size.X, size.Y) / 2));
        _clipGeometry.CornerRadius = new Vector2(radius);
        _rimGeometry.CornerRadius = new Vector2(Math.Max(0, radius - 1));
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _host.SizeChanged -= SizeChanged;
        _state.StopAnimation("Pointer");
        _root.StopAnimation("Opacity");
        foreach (var target in _animated) target.StopAnimation("EllipseCenter");
        ElementCompositionPreview.SetElementChildVisual(_host, null);
        _root.Children.RemoveAll();
        _root.Dispose();
        _state.Dispose();
    }
}
