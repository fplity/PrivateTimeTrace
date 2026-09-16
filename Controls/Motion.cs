using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace PrivateTimeTrace.Controls;

public static class Motion
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(Motion), new PropertyMetadata(false, EnabledChanged));
    private static readonly DependencyProperty BehaviorProperty = DependencyProperty.RegisterAttached("Behavior", typeof(object), typeof(Motion), new PropertyMetadata(null));
    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);
    private static void EnabledChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is not ButtonBase button) return;
        if (button.GetValue(BehaviorProperty) is ButtonMotion old) old.Detach();
        button.SetValue(BehaviorProperty, (bool)args.NewValue ? new ButtonMotion(button) : null);
    }
    private sealed class ButtonMotion
    {
        private readonly ButtonBase _button;
        private FrameworkElement? _surface;
        private LiquidLight? _light;
        private bool _pressed;
        private Vector3 _target = Vector3.One;
        public ButtonMotion(ButtonBase button)
        {
            _button = button;
            button.Loaded += Loaded;
            button.Unloaded += Unloaded;
            button.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(Pressed), true);
            button.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(Released), true);
            button.PointerCaptureLost += Released;
            button.PointerMoved += Moved;
            button.PointerExited += Exited;
            if (button.IsLoaded) Loaded(button, new RoutedEventArgs());
        }
        private void Loaded(object sender, RoutedEventArgs args)
        {
            if (App.IsClosing) return;
            _surface = Find(_button, "MotionSurface");
            if (_light is null && Find(_button, "SurfaceLight") is { } host) _light = new LiquidLight(host, 22, compact: true);
        }
        private void Unloaded(object sender, RoutedEventArgs args)
        {
            if (_surface is not null)
            {
                var visual = ElementCompositionPreview.GetElementVisual(_surface);
                visual.StopAnimation("Scale");
                visual.Scale = Vector3.One;
            }
            _light?.Dispose();
            _light = null;
            _surface = null;
            _pressed = false;
            _target = Vector3.One;
        }
        private void Pressed(object sender, PointerRoutedEventArgs args) { _pressed = true; Scale(new Vector3(1.025f, .91f, 1)); }
        private void Released(object sender, PointerRoutedEventArgs args) { _pressed = false; Scale(Vector3.One); }
        private void Moved(object sender, PointerRoutedEventArgs args)
        {
            _light?.Track(args.GetCurrentPoint(_button).Position);
            if (!_pressed) Scale(new Vector3(1.015f, 1.035f, 1));
        }
        private void Exited(object sender, PointerRoutedEventArgs args) { _pressed = false; _light?.Rest(); Scale(Vector3.One); }
        private void Scale(Vector3 value)
        {
            if (_surface is null || !_surface.IsLoaded || App.ReducedEffects || _target == value) return;
            _target = value;
            var visual = ElementCompositionPreview.GetElementVisual(_surface);
            visual.CenterPoint = new Vector3((float)_surface.ActualWidth / 2, (float)_surface.ActualHeight / 2, 0);
            using var animation = visual.Compositor.CreateSpringVector3Animation();
            animation.FinalValue = value;
            animation.DampingRatio = .64f;
            animation.Period = TimeSpan.FromMilliseconds(290);
            visual.StartAnimation("Scale", animation);
        }
        public void Detach()
        {
            Unloaded(_button, new RoutedEventArgs());
            _button.Loaded -= Loaded;
            _button.Unloaded -= Unloaded;
            _button.RemoveHandler(UIElement.PointerPressedEvent, new PointerEventHandler(Pressed));
            _button.RemoveHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(Released));
            _button.PointerCaptureLost -= Released;
            _button.PointerMoved -= Moved;
            _button.PointerExited -= Exited;
        }
    }
    private static FrameworkElement? Find(DependencyObject node, string name)
    {
        if (node is FrameworkElement element && element.Name == name) return element;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            if (Find(VisualTreeHelper.GetChild(node, i), name) is { } found) return found;
        return null;
    }
}
