using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.UI;

namespace PrivateTimeTrace.Controls;

/// <summary>A native in-app acrylic surface, with separate light and readable content layers.</summary>
[ContentProperty(Name = nameof(Body))]
public sealed class GlassPanel : UserControl
{
    public static readonly DependencyProperty BodyProperty = DependencyProperty.Register(nameof(Body), typeof(object), typeof(GlassPanel), new PropertyMetadata(null, OnBodyChanged));
    public static readonly DependencyProperty IsLiquidProperty = DependencyProperty.Register(nameof(IsLiquid), typeof(bool), typeof(GlassPanel), new PropertyMetadata(true, OnMaterialChanged));
    public object? Body { get => GetValue(BodyProperty); set => SetValue(BodyProperty, value); }
    public bool IsLiquid { get => (bool)GetValue(IsLiquidProperty); set => SetValue(IsLiquidProperty, value); }

    private readonly ContentPresenter _presenter = new();
    private readonly AcrylicBrush _acrylic = new() { TintColor = Color.FromArgb(255, 240, 246, 254), FallbackColor = Color.FromArgb(255, 235, 243, 251) };
    private readonly Border _surface;
    private readonly Border _rim;
    private readonly Border _innerRim;

    public GlassPanel()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        Padding = new Thickness(24);
        var root = new Grid();
        _surface = new Border { CornerRadius = new CornerRadius(22), Background = _acrylic, Shadow = new ThemeShadow() };
        root.Children.Add(_surface);
        _rim = new Border { CornerRadius = new CornerRadius(22), BorderThickness = new Thickness(1.2), IsHitTestVisible = false,
            BorderBrush = Gradient((0, "#FFFFFFFF"), (.28, "#95FFFFFF"), (.55, "#22A0B9D4"), (.82, "#A0FFFFFF"), (1, "#FFFFFFFF")) };
        _innerRim = new Border { Margin = new Thickness(2), CornerRadius = new CornerRadius(20), BorderThickness = new Thickness(1), IsHitTestVisible = false,
            BorderBrush = Gradient((0, "#AAFFFFFF"), (.25, "#05FFFFFF"), (.75, "#08FFFFFF"), (1, "#80FFFFFF")) };
        root.Children.Add(_rim);
        root.Children.Add(_innerRim);
        _presenter.SetBinding(ContentPresenter.PaddingProperty, new Microsoft.UI.Xaml.Data.Binding { Source = this, Path = new PropertyPath(nameof(Padding)) });
        root.Children.Add(_presenter);
        Content = root;
        Loaded += (_, _) => ApplyMaterial();
    }

    private void ApplyMaterial()
    {
        var liquid = IsLiquid && !App.ReducedEffects;
        _acrylic.TintOpacity = liquid ? .18 : .82;
        _acrylic.TintLuminosityOpacity = liquid ? .36 : .92;
        _acrylic.AlwaysUseFallback = App.HighContrast;
        // Both palettes use dark ink; the opaque fallback must retain a light surface.
        _acrylic.FallbackColor = Parse("#EBF3FB");
        _surface.Translation = new Vector3(0, 0, liquid ? 18 : 4);
        _rim.Opacity = liquid ? 1 : .55;
        _innerRim.Opacity = liquid ? 1 : .15;
    }

    private static void OnBodyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) => ((GlassPanel)sender)._presenter.Content = args.NewValue;
    private static void OnMaterialChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) => ((GlassPanel)sender).ApplyMaterial();
    internal static Color Parse(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6) hex = "FF" + hex;
        return Color.FromArgb(System.Convert.ToByte(hex[..2], 16), System.Convert.ToByte(hex[2..4], 16), System.Convert.ToByte(hex[4..6], 16), System.Convert.ToByte(hex[6..8], 16));
    }
    internal static LinearGradientBrush Gradient(params (double Offset, string Color)[] stops)
    {
        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
        foreach (var stop in stops) brush.GradientStops.Add(new GradientStop { Offset = stop.Offset, Color = Parse(stop.Color) });
        return brush;
    }
}
