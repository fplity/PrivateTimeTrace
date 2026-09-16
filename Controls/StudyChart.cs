using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using PrivateTimeTrace.Data;
using PrivateTimeTrace.Services;
using Windows.Foundation;

namespace PrivateTimeTrace.Controls;

public sealed class StudyChart : Canvas
{
    public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(nameof(Points), typeof(IReadOnlyList<ChartPoint>), typeof(StudyChart), new PropertyMetadata(null, Changed));
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(nameof(Kind), typeof(ChartKind), typeof(StudyChart), new PropertyMetadata(ChartKind.Line, Changed));
    public IReadOnlyList<ChartPoint>? Points { get => (IReadOnlyList<ChartPoint>?)GetValue(PointsProperty); set => SetValue(PointsProperty, value); }
    public ChartKind Kind { get => (ChartKind)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public bool Categories { get; set; }
    public event EventHandler<string>? CategoryClicked;
    public StudyChart() { Height = 244; SizeChanged += (_, _) => Draw(); Loaded += (_, _) => Draw(); }
    private static void Changed(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var chart = (StudyChart)sender;
        chart.Draw();
    }

    private void Draw()
    {
        Children.Clear();
        if (ActualWidth < 80) return;
        var points = Points ?? Array.Empty<ChartPoint>();
        // Wide sets remain readable and reachable through the containing ScrollViewer.
        MinWidth = points.Count > 12 ? Math.Min(12000, points.Count * 46 + 60) : 0;
        const double left = 42, top = 28, bottom = 40, right = 18;
        var width = Math.Max(1, ActualWidth - left - right);
        var height = Math.Max(1, ActualHeight - top - bottom);
        var maximum = Math.Max(30, points.Select(point => point.Minutes).DefaultIfEmpty(0).Max());
        var step = Math.Pow(10, Math.Floor(Math.Log10(maximum / 4)));
        step *= new[] { 1.0, 2, 2.5, 5, 10 }.First(value => value * step >= maximum / 4);
        var ceiling = step * 4;
        var inHours = ceiling >= 120;
        var ink = new SolidColorBrush(GlassPanel.Parse(App.HighContrast ? "#162E4D" : "#52657D"));
        for (var index = 0; index <= 4; index++)
        {
            var y = top + height * index / 4;
            Children.Add(new Line { X1 = left, X2 = left + width, Y1 = y, Y2 = y, Stroke = new SolidColorBrush(GlassPanel.Parse("#176883A3")), StrokeThickness = 1 });
            var value = ceiling * (4 - index) / 4;
            Label($"{(inHours ? value / 60 : value):0.#}{(inHours ? "h" : "m")}", 0, y - 8, left - 9, ink, TextAlignment.Right);
        }

        if (points.Count == 0 || points.All(point => point.Minutes == 0))
        {
            Label("还没有专注记录", left, top + height * .36, width, ink, TextAlignment.Center, 15);
            Label("完成一次学习，让进步在这里留下痕迹", left, top + height * .36 + 28, width, ink, TextAlignment.Center, 12);
            return;
        }

        var slot = width / points.Count;
        var coordinates = points.Select((point, index) => new Point(left + slot * (index + .5), top + height * (1 - Math.Max(0, point.Minutes) / ceiling))).ToList();
        if (Kind == ChartKind.Line)
        {
            var area = new Polygon { Fill = GlassPanel.Gradient((0, "#383F92FA"), (1, "#004E9AFF")), IsHitTestVisible = false };
            area.Points.Add(new Point(coordinates[0].X, top + height));
            foreach (var point in coordinates) area.Points.Add(point);
            area.Points.Add(new Point(coordinates[^1].X, top + height));
            Children.Add(area);
            var line = new Polyline { Stroke = new SolidColorBrush(GlassPanel.Parse("#318BEE")), StrokeThickness = 2.2, IsHitTestVisible = false };
            foreach (var point in coordinates) line.Points.Add(point);
            Children.Add(line);
        }

        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];
            var position = coordinates[index];
            Shape mark;
            if (Kind == ChartKind.Bar)
            {
                var barWidth = Math.Max(5, Math.Min(40, slot * .5));
                mark = new Rectangle { Width = barWidth, Height = Math.Max(0, top + height - position.Y), RadiusX = 6, RadiusY = 6,
                    Fill = GlassPanel.Gradient((0, "#CC5DA5FF"), (.2, "#DB5DA5FF"), (1, "#70438FE4")), Stroke = new SolidColorBrush(GlassPanel.Parse("#A8DAECFF")), StrokeThickness = 1 };
                SetLeft(mark, position.X - barWidth / 2); SetTop(mark, position.Y);
            }
            else
            {
                mark = new Ellipse { Width = 9, Height = 9, Fill = new SolidColorBrush(GlassPanel.Parse("#248CF4")), Stroke = new SolidColorBrush(Microsoft.UI.Colors.White), StrokeThickness = 1.5 };
                SetLeft(mark, position.X - 4.5); SetTop(mark, position.Y - 4.5);
            }
            var summary = $"{point.Label} · {StudyAnalytics.FormatDuration(TimeSpan.FromMinutes(point.Minutes))}";
            ToolTipService.SetToolTip(mark, summary);
            AutomationProperties.SetName(mark, summary);
            if (Categories) mark.Tapped += (_, _) => CategoryClicked?.Invoke(this, point.Label);
            Children.Add(mark);
            if (points.Count <= 12 && point.Minutes > 0)
                Label(inHours ? $"{point.Minutes / 60:0.#}h" : $"{point.Minutes:0.#}m", position.X - slot / 2, position.Y - 23, slot, ink, TextAlignment.Center, 11);
            var label = Label(point.Label, position.X - slot / 2 + 2, top + height + 14, slot - 4, ink, TextAlignment.Center, 11);
            ToolTipService.SetToolTip(label, summary);
        }
    }

    private TextBlock Label(string text, double x, double y, double width, Brush ink, TextAlignment alignment, double size = 11)
    {
        var label = new TextBlock { Text = text, Width = Math.Max(1, width), Foreground = ink, FontSize = size, TextAlignment = alignment, TextTrimming = TextTrimming.CharacterEllipsis };
        SetLeft(label, x); SetTop(label, y); Children.Add(label); return label;
    }
}
