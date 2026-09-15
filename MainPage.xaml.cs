using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PrivateTimeTrace.Controls;
using PrivateTimeTrace.Data;
using PrivateTimeTrace.ViewModels;

namespace PrivateTimeTrace;

public sealed partial class MainPage : Page
{
    public MainPageViewModel ViewModel { get; } = new(new StudyDatabase(App.DatabasePath));
    public UIElement TitleBarElement => DragRegion;
    private bool _ready;
    private bool _syncingControls;
    public MainPage()
    {
        InitializeComponent();
        _ready = true;
        // Selection changes also cover keyboard and accessibility activation.
        FrostedStyleButton.Checked += Style_Click;
        LiquidStyleButton.Checked += Style_Click;
        TrendLine.Checked += Trend_Click;
        TrendBar.Checked += Trend_Click;
        TopicLine.Checked += Topic_Click;
        TopicBar.Checked += Topic_Click;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        TopicChart.CategoryClicked += (_, topic) => { ViewModel.ShowTopic(topic); RecordsNav.IsChecked = true; };
        Loaded += async (_, _) =>
        {
            await ViewModel.InitializeAsync();
            SyncPreferences();
            Motion.Wire(this);
        };
    }
    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (App.IsClosing) return;
        if (args.PropertyName is nameof(ViewModel.Style) or nameof(ViewModel.TrendKind) or nameof(ViewModel.TopicKind)) SyncPreferences();
        if (args.PropertyName == nameof(ViewModel.Style) && IsLoaded) Motion.Reveal(MainStack);
    }
    private void SyncPreferences()
    {
        if (!_ready) return;
        _syncingControls = true;
        try
        {
        FrostedStyleButton.IsChecked = ViewModel.IsFrostedStyle;
        LiquidStyleButton.IsChecked = ViewModel.UseLiquidStyle;
        TrendLine.IsChecked = ViewModel.TrendKind == ChartKind.Line;
        TrendBar.IsChecked = ViewModel.TrendKind == ChartKind.Bar;
        TopicLine.IsChecked = ViewModel.TopicKind == ChartKind.Line;
        TopicBar.IsChecked = ViewModel.TopicKind == ChartKind.Bar;
        FrostVeil.Opacity = ViewModel.UseLiquidStyle ? 0 : .91;
        Wallpaper.Opacity = ViewModel.UseLiquidStyle ? 1 : .7;
        ThemeCaption.Text = ViewModel.UseLiquidStyle ? "液态流光" : "霜白玻璃";
        }
        finally { _syncingControls = false; }
    }
    private async void Style_Click(object sender, RoutedEventArgs args)
    {
        if (!_ready || _syncingControls) return;
        if (sender is FrameworkElement { Tag: string name } && Enum.TryParse<VisualStyle>(name, out var style) && ViewModel.Style != style) await ViewModel.SetStyleAsync(style);
    }
    private async void Trend_Click(object sender, RoutedEventArgs args)
    {
        if (!_ready || _syncingControls) return;
        if (sender is FrameworkElement { Tag: string name } && Enum.TryParse<ChartKind>(name, out var kind) && ViewModel.TrendKind != kind) await ViewModel.SetTrendKindAsync(kind);
    }
    private async void Topic_Click(object sender, RoutedEventArgs args)
    {
        if (!_ready || _syncingControls) return;
        if (sender is FrameworkElement { Tag: string name } && Enum.TryParse<ChartKind>(name, out var kind) && ViewModel.TopicKind != kind) await ViewModel.SetTopicKindAsync(kind);
    }
    private void Period_Click(object sender, RoutedEventArgs args)
    {
        if (sender is FrameworkElement { Tag: string name } && Enum.TryParse<ReportPeriod>(name, out var period)) ViewModel.SetPeriod(period);
    }
    private void Navigation_Click(object sender, RoutedEventArgs args)
    {
        if (sender is FrameworkElement { Tag: string section })
        {
            ViewModel.Navigate(section);
            OverviewNav.IsChecked = section == "Overview"; RecordsNav.IsChecked = section == "Records"; AnalyticsNav.IsChecked = section == "Analytics";
            MainScroll.ChangeView(null, 0, null);
            Motion.Reveal(MainStack);
        }
    }
    private void TopicDetails_Click(object sender, RoutedEventArgs args)
    {
        if (sender is FrameworkElement { Tag: string topic }) { ViewModel.ShowTopic(topic); RecordsNav.IsChecked = true; MainScroll.ChangeView(null, 0, null); }
    }
    private async void DeleteRecord_Click(object sender, RoutedEventArgs args)
    {
        if (sender is not Button { Tag: long id }) return;
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = "删除这条学习记录？", Content = "删除后，本地统计与图表会同步更新。此操作无法撤销。", PrimaryButtonText = "删除记录", CloseButtonText = "保留", DefaultButton = ContentDialogButton.Close };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary) await ViewModel.DeleteRecordAsync(id);
    }
    private void MainScroll_SizeChanged(object sender, SizeChangedEventArgs args)
    {
        if (!_ready) return;
        var compactNavigation = ActualWidth < 1050;
        NavigationColumn.Width = new GridLength(compactNavigation ? 90 : 198);
        var textVisibility = compactNavigation ? Visibility.Collapsed : Visibility.Visible;
        OverviewNavText.Visibility = RecordsNavText.Visibility = AnalyticsNavText.Visibility = LocalSavedText.Visibility = ThemeCaption.Visibility = textVisibility;
        var narrow = args.NewSize.Width < 890;
        Grid.SetColumn(TopicCard, narrow ? 0 : 1); Grid.SetRow(TopicCard, narrow ? 1 : 0);
        ChartGrid.ColumnDefinitions[1].Width = narrow ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        var stackedSummary = args.NewSize.Width < 800;
        Grid.SetColumn(SummaryGrid, stackedSummary ? 0 : 1);
        Grid.SetRow(SummaryGrid, stackedSummary ? 1 : 0);
        SummaryGrid.Margin = stackedSummary ? new Thickness(0, 18, 0, 0) : new Thickness(0);
        TimerStack.Margin = stackedSummary ? new Thickness(0) : new Thickness(0, 0, 24, 0);
        FocusGrid.ColumnDefinitions[1].Width = stackedSummary ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        MainStack.Margin = new Thickness(narrow ? 20 : 28, 12, narrow ? 20 : 28, 28);
        HeaderGrid.Margin = new Thickness(narrow ? 24 : 32, 16, narrow ? 24 : 32, 8);
    }
    private void Window_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args)
    {
        if (!ViewModel.UseLiquidStyle || App.ReducedEffects) return;
        var position = args.GetCurrentPoint(WindowRoot).Position;
        var transform = (TranslateTransform)Wallpaper.RenderTransform;
        transform.X = (position.X / Math.Max(1, ActualWidth) - .5) * 5;
        transform.Y = (position.Y / Math.Max(1, ActualHeight) - .5) * 4;
    }
}
