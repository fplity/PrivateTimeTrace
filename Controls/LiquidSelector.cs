using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace PrivateTimeTrace.Controls;

/// <summary>One continuous lens shared by native radio buttons, including keyboard/UIA selection.</summary>
[ContentProperty(Name = nameof(Items))]
public sealed class LiquidSelector : UserControl
{
    public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(nameof(Items), typeof(UIElement), typeof(LiquidSelector), new PropertyMetadata(null, ItemsChanged));
    public static readonly DependencyProperty IsLiquidProperty = DependencyProperty.Register(nameof(IsLiquid), typeof(bool), typeof(LiquidSelector), new PropertyMetadata(true, MaterialChanged));
    public UIElement? Items { get => (UIElement?)GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }
    public bool IsLiquid { get => (bool)GetValue(IsLiquidProperty); set => SetValue(IsLiquidProperty, value); }
    private readonly Grid _track = new();
    private readonly Canvas _lensHost = new() { IsHitTestVisible = false };
    private readonly Grid _lightHost = new() { IsHitTestVisible = false };
    private readonly List<RadioButton> _buttons = new();
    private CompositionPropertySet? _motion;
    private ContainerVisual? _lens;
    private LiquidLight? _light;
    private readonly List<(CompositionObject Target, string Property)> _bindings = new();
    private RadioButton? _selected;
    private Rect _bounds;
    private bool _hasPosition;
    private bool _pressed;

    public LiquidSelector()
    {
        IsTabStop = false;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        var border = new Border { CornerRadius = new CornerRadius(24), Child = _track };
        Bind(border, Border.BackgroundProperty, nameof(Background));
        Bind(border, Border.BorderBrushProperty, nameof(BorderBrush));
        Bind(border, Border.BorderThicknessProperty, nameof(BorderThickness));
        Bind(border, Border.PaddingProperty, nameof(Padding));
        _track.Children.Add(_lensHost);
        _track.Children.Add(_lightHost);
        Content = border;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += (_, _) => UpdateSelection(false);
        AddHandler(PointerPressedEvent, new PointerEventHandler(Pressed), true);
        AddHandler(PointerReleasedEvent, new PointerEventHandler(Released), true);
        PointerCaptureLost += Released;
        PointerExited += Released;
        AddHandler(PointerMovedEvent, new PointerEventHandler(PointerMovedInSelector), true);
        PointerExited += PointerLeftSelector;
        PreviewKeyDown += PreviewKey;
    }
    private void Bind(DependencyObject target, DependencyProperty property, string path) =>
        Microsoft.UI.Xaml.Data.BindingOperations.SetBinding(target, property,
            new Microsoft.UI.Xaml.Data.Binding { Source = this, Path = new PropertyPath(path) });
    private static void ItemsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var selector = (LiquidSelector)sender;
        selector.Unwire();
        if (args.OldValue is UIElement old) selector._track.Children.Remove(old);
        if (args.NewValue is UIElement item) selector._track.Children.Add(item);
        if (selector.IsLoaded) selector.Wire();
    }
    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (App.IsClosing || _lens is not null) return;
        var compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
        _motion = compositor.CreatePropertySet();
        _motion.InsertVector3("Center", Vector3.Zero);
        _motion.InsertVector2("Extent", new Vector2(4));
        _motion.InsertVector2("Stretch", Vector2.One);
        _lens = compositor.CreateContainerVisual();
        _lens.Opacity = 0;
        Expression(_lens, "Size", "motion.Extent * motion.Stretch");
        Expression(_lens, "Offset", "motion.Center - Vector3(motion.Extent.X * motion.Stretch.X * 0.5, motion.Extent.Y * motion.Stretch.Y * 0.5, 0)");
        var geometry = compositor.CreateRoundedRectangleGeometry();
        geometry.Offset = Vector2.One;
        Expression(geometry, "Size", "Max(Vector2(0, 0), motion.Extent * motion.Stretch - Vector2(2, 2))");
        Expression(geometry, "CornerRadius", "Vector2(Max(0, Min(21, motion.Extent.Y * motion.Stretch.Y * 0.5 - 1)), Max(0, Min(21, motion.Extent.Y * motion.Stretch.Y * 0.5 - 1)))");
        var fill = compositor.CreateLinearGradientBrush();
        fill.StartPoint = Vector2.Zero;
        fill.EndPoint = new Vector2(.25f, 1);
        foreach (var (offset, color) in new[] { (0f, "#EDFFFFFF"), (.35f, "#9BE7F5FF"), (.72f, "#70A0D5FA"), (1f, "#CDD9F2FF") })
            fill.ColorStops.Add(compositor.CreateColorGradientStop(offset, GlassPanel.Parse(color)));
        var rim = compositor.CreateLinearGradientBrush();
        rim.EndPoint = Vector2.One;
        foreach (var (offset, color) in new[] { (0f, "#FFFFFFFF"), (.42f, "#B0FFFFFF"), (.64f, "#458ABAE0"), (1f, "#F0FFFFFF") })
            rim.ColorStops.Add(compositor.CreateColorGradientStop(offset, GlassPanel.Parse(color)));
        var shape = compositor.CreateSpriteShape(geometry);
        shape.FillBrush = fill;
        shape.StrokeBrush = rim;
        shape.StrokeThickness = 1.2f;
        var plate = compositor.CreateShapeVisual();
        plate.RelativeSizeAdjustment = Vector2.One;
        plate.Shapes.Add(shape);
        _lens.Children.InsertAtTop(plate);
        ElementCompositionPreview.SetElementChildVisual(_lensHost, _lens);
        _light = new LiquidLight(_lightHost, 24, compact: true);
        _light.SetMaterial(IsLiquid);
        Wire();
    }
    private void Expression(CompositionObject target, string property, string expression)
    {
        using var animation = _lens!.Compositor.CreateExpressionAnimation(expression);
        animation.SetReferenceParameter("motion", _motion!);
        target.StartAnimation(property, animation);
        _bindings.Add((target, property));
    }
    private void Wire()
    {
        Unwire();
        FindButtons(Items);
        foreach (var button in _buttons)
        {
            button.Checked += Checked;
            button.SizeChanged += ButtonSizeChanged;
            button.AddHandler(PointerMovedEvent, new PointerEventHandler(PointerMovedInSelector), true);
            button.AddHandler(PointerPressedEvent, new PointerEventHandler(Pressed), true);
            button.AddHandler(PointerReleasedEvent, new PointerEventHandler(Released), true);
            button.PointerExited += PointerLeftSelector;
            button.PointerCaptureLost += Released;
            button.PreviewKeyDown += PreviewKey;
        }
        UpdateSelection(false);
    }
    private void FindButtons(DependencyObject? node)
    {
        if (node is null) return;
        if (node is RadioButton radio) { _buttons.Add(radio); return; }
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) FindButtons(VisualTreeHelper.GetChild(node, i));
    }
    private void Unwire()
    {
        foreach (var button in _buttons)
        {
            button.Checked -= Checked;
            button.SizeChanged -= ButtonSizeChanged;
            button.RemoveHandler(PointerMovedEvent, new PointerEventHandler(PointerMovedInSelector));
            button.RemoveHandler(PointerPressedEvent, new PointerEventHandler(Pressed));
            button.RemoveHandler(PointerReleasedEvent, new PointerEventHandler(Released));
            button.PointerExited -= PointerLeftSelector;
            button.PointerCaptureLost -= Released;
            button.PreviewKeyDown -= PreviewKey;
        }
        _buttons.Clear();
    }
    private void Checked(object sender, RoutedEventArgs args) { _pressed = false; UpdateSelection(true); }
    private void ButtonSizeChanged(object sender, SizeChangedEventArgs args) => UpdateSelection(false);
    private void UpdateSelection(bool animate)
    {
        if (!IsLoaded || App.IsClosing || _motion is null || _lens is null) return;
        var selected = _buttons.FirstOrDefault(button => button.IsChecked == true);
        if (selected is null || selected.ActualWidth < 1 || selected.ActualHeight < 1) return;
        var origin = selected.TransformToVisual(_track).TransformPoint(new Point());
        var bounds = new Rect(origin, new Size(selected.ActualWidth, selected.ActualHeight));
        if (_hasPosition && _selected == selected && bounds == _bounds) return;
        var center = new Vector3((float)(bounds.X + bounds.Width / 2), (float)(bounds.Y + bounds.Height / 2), 0);
        var extent = new Vector2((float)bounds.Width, (float)bounds.Height);
        var dx = bounds.X - _bounds.X;
        var dy = bounds.Y - _bounds.Y;
        var shouldAnimate = animate && _hasPosition && !App.ReducedEffects;
        _selected = selected;
        _bounds = bounds;
        _hasPosition = true;
        _lens.Opacity = 1;
        if (!shouldAnimate)
        {
            foreach (var property in new[] { "Center", "Extent", "Stretch" }) _motion.StopAnimation(property);
            _motion.InsertVector3("Center", center);
            _motion.InsertVector2("Extent", extent);
            _motion.InsertVector2("Stretch", Vector2.One);
            return;
        }
        // A bounded, gently overshooting flight keeps the lens responsive, including repeated reversals.
        using var easing = _lens.Compositor.CreateCubicBezierEasingFunction(new Vector2(.2f, .9f), new Vector2(.25f, 1));
        using var position = _lens.Compositor.CreateVector3KeyFrameAnimation();
        var travel = new Vector3((float)dx, (float)dy, 0);
        position.InsertKeyFrame(.64f, center + travel * (IsLiquid ? .045f : .012f), easing);
        position.InsertKeyFrame(.84f, center - travel * .008f, easing);
        position.InsertKeyFrame(1, center, easing);
        position.Duration = TimeSpan.FromMilliseconds(IsLiquid ? 520 : 380);
        position.StopBehavior = AnimationStopBehavior.LeaveCurrentValue;
        _motion.StartAnimation("Center", position);
        using var size = _lens.Compositor.CreateVector2KeyFrameAnimation();
        size.InsertKeyFrame(1, extent, easing);
        size.Duration = TimeSpan.FromMilliseconds(380);
        _motion.StartAnimation("Extent", size);
        var strength = IsLiquid ? .24f : .1f;
        var stretch = Math.Abs(dx) >= Math.Abs(dy) ? new Vector2(1 + strength, 1 - strength * .42f) : new Vector2(1 - strength * .26f, 1 + strength);
        using var flex = _lens.Compositor.CreateVector2KeyFrameAnimation();
        flex.InsertKeyFrame(.24f, stretch, easing);
        flex.InsertKeyFrame(.65f, new Vector2(1 - (stretch.X - 1) * .22f, 1 - (stretch.Y - 1) * .22f), easing);
        flex.InsertKeyFrame(1, Vector2.One, easing);
        flex.Duration = TimeSpan.FromMilliseconds(IsLiquid ? 560 : 400);
        _motion.StartAnimation("Stretch", flex);
    }
    private void Pressed(object sender, PointerRoutedEventArgs args)
    {
        if (_motion is null || App.ReducedEffects) return;
        _pressed = true;
        using var animation = _motion.Compositor.CreateVector2KeyFrameAnimation();
        animation.InsertKeyFrame(1, new Vector2(1.035f, .93f));
        animation.Duration = TimeSpan.FromMilliseconds(100);
        _motion.StartAnimation("Stretch", animation);
    }
    private void Released(object sender, PointerRoutedEventArgs args)
    {
        // PointerExited also fires while moving between radio children; never interrupt a travelling lens.
        if (!_pressed || _motion is null || App.ReducedEffects) return;
        _pressed = false;
        using var animation = _motion.Compositor.CreateVector2KeyFrameAnimation();
        animation.InsertKeyFrame(.45f, new Vector2(.99f, 1.025f));
        animation.InsertKeyFrame(1, Vector2.One);
        animation.Duration = TimeSpan.FromMilliseconds(280);
        _motion.StartAnimation("Stretch", animation);
    }
    private void PreviewKey(object sender, KeyRoutedEventArgs args)
    {
        if (args.Handled) return;
        var key = args.Key;
        if (key is not (Windows.System.VirtualKey.Left or Windows.System.VirtualKey.Right or Windows.System.VirtualKey.Up or Windows.System.VirtualKey.Down or Windows.System.VirtualKey.Home or Windows.System.VirtualKey.End)) return;
        var buttons = _buttons.Where(button => button.IsEnabled && button.Visibility == Visibility.Visible).ToList();
        if (buttons.Count == 0) return;
        var current = buttons.FindIndex(button => button.IsChecked == true);
        var next = key switch
        {
            Windows.System.VirtualKey.Home => 0,
            Windows.System.VirtualKey.End => buttons.Count - 1,
            Windows.System.VirtualKey.Left or Windows.System.VirtualKey.Up => (current - 1 + buttons.Count) % buttons.Count,
            _ => (current + 1) % buttons.Count
        };
        buttons[next].Focus(FocusState.Keyboard);
        buttons[next].IsChecked = true;
        args.Handled = true;
    }
    private void PointerMovedInSelector(object sender, PointerRoutedEventArgs args)
    {
        _light?.Track(args.GetCurrentPoint(_lightHost).Position);
    }
    private void PointerLeftSelector(object sender, PointerRoutedEventArgs args)
    {
        var point = args.GetCurrentPoint(this).Position;
        if (point.X <= 0 || point.Y <= 0 || point.X >= ActualWidth || point.Y >= ActualHeight) _light?.Rest();
    }
    private static void MaterialChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) => ((LiquidSelector)sender)._light?.SetMaterial((bool)args.NewValue);
    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        Unwire();
        _light?.Dispose();
        _light = null;
        if (_motion is not null) foreach (var property in new[] { "Center", "Extent", "Stretch" }) _motion.StopAnimation(property);
        foreach (var (target, property) in _bindings) target.StopAnimation(property);
        _bindings.Clear();
        ElementCompositionPreview.SetElementChildVisual(_lensHost, null);
        _lens?.Children.RemoveAll();
        _lens?.Dispose();
        _motion?.Dispose();
        _lens = null;
        _motion = null;
        _selected = null;
        _hasPosition = false;
        _pressed = false;
    }
}
