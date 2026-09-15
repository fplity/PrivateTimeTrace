using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using PrivateTimeTrace.Data;
using PrivateTimeTrace.Services;

namespace PrivateTimeTrace.ViewModels;

public partial class MainPageViewModel : ObservableObject
{
    private readonly StudyDatabase _database;
    private readonly DispatcherQueueTimer _clock;
    private readonly SemaphoreSlim _preferencesGate = new(1, 1);
    private bool _initialized;
    private DateTime _lastDate = DateTime.Today;

    [ObservableProperty] public partial string CurrentSection { get; set; } = "Overview";
    [ObservableProperty] public partial string SessionNote { get; set; } = string.Empty;
    [ObservableProperty] public partial ActiveSession? ActiveSession { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; set; } = true;
    [ObservableProperty] public partial string ErrorMessage { get; set; } = string.Empty;
    [ObservableProperty] public partial string ElapsedDisplay { get; set; } = "00:00:00";
    [ObservableProperty] public partial VisualStyle Style { get; set; } = VisualStyle.Liquid;
    [ObservableProperty] public partial ChartKind TrendKind { get; set; } = ChartKind.Line;
    [ObservableProperty] public partial ChartKind TopicKind { get; set; } = ChartKind.Bar;
    [ObservableProperty] public partial ReportPeriod SelectedPeriod { get; set; } = ReportPeriod.Week;
    [ObservableProperty] public partial string PeriodRange { get; set; } = string.Empty;
    [ObservableProperty] public partial string TodayTotalDisplay { get; set; } = "0秒";
    [ObservableProperty] public partial string WeekTotalDisplay { get; set; } = "0秒";
    [ObservableProperty] public partial string SelectedTotalDisplay { get; set; } = "0秒";
    [ObservableProperty] public partial string SelectedRecordCountDisplay { get; set; } = "0次记录";
    [ObservableProperty] public partial IReadOnlyList<ChartPoint> TrendPoints { get; set; } = Array.Empty<ChartPoint>();
    [ObservableProperty] public partial IReadOnlyList<ChartPoint> TopicPoints { get; set; } = Array.Empty<ChartPoint>();
    [ObservableProperty] public partial IReadOnlyList<TopicTotal> TopicTotals { get; set; } = Array.Empty<TopicTotal>();
    [ObservableProperty] public partial string TopicFilter { get; set; } = string.Empty;

    public ObservableCollection<StudyRecord> Records { get; } = new();
    public ObservableCollection<StudyRecord> RecentRecords { get; } = new();
    public ObservableCollection<StudyRecord> FilteredRecords { get; } = new();
    public bool IsOverviewVisible => CurrentSection == "Overview";
    public bool IsRecordsVisible => CurrentSection == "Records";
    public bool AreChartsVisible => !IsRecordsVisible;
    public bool IsAnalyticsVisible => CurrentSection == "Analytics";
    public bool UseLiquidStyle => Style == VisualStyle.Liquid;
    public bool IsFrostedStyle => Style == VisualStyle.Frosted;
    public bool IsRunning => ActiveSession is not null;
    public bool CanEditNote => !IsBusy && !IsRunning;
    public bool HasError => ErrorMessage.Length > 0;
    public string PageTitle => IsRecordsVisible ? "学习记录" : IsAnalyticsVisible ? "数据分析" : "概览";
    public string DateLabel => DateTime.Today.ToString("M月d日，dddd", System.Globalization.CultureInfo.GetCultureInfo("zh-CN"));
    public string StartButtonText => IsRunning ? "结束并保存" : "开始专注";
    public string SessionStatus => IsRunning ? "正在专注" : "准备好开始了吗";
    public string ActiveNoteDisplay => ActiveSession?.Note is { Length: > 0 } note ? note : "未命名学习";
    public bool NoRecords => Records.Count == 0;
    public bool NoFilteredRecords => FilteredRecords.Count == 0;
    public string RecordsHeading => TopicFilter.Length > 0 ? $"主题 · {TopicFilter}" : "全部记录";
    public string TopicHint => TopicPoints.Count > 12 ? $"{TopicPoints.Count}个主题 · 可横向滚动" : "按所选周期累计 · 点击主题查看记录";

    public MainPageViewModel(StudyDatabase database)
    {
        _database = database;
        _clock = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _clock.Interval = TimeSpan.FromSeconds(1);
        _clock.Tick += (_, _) =>
        {
            var elapsed = ActiveSession is null ? TimeSpan.Zero : DateTime.UtcNow - ActiveSession.StartedAt.ToUniversalTime();
            ElapsedDisplay = $"{Math.Max(0, (long)elapsed.TotalHours):00}:{Math.Max(0, elapsed.Minutes):00}:{Math.Max(0, elapsed.Seconds):00}";
            if (_lastDate != DateTime.Today) { _lastDate = DateTime.Today; RefreshAnalytics(); OnPropertyChanged(nameof(DateLabel)); }
        };
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        try
        {
            await _database.InitializeAsync();
            var preferences = await _database.LoadPreferencesAsync();
            Style = preferences.Style;
            TrendKind = preferences.TrendKind;
            TopicKind = preferences.TopicKind;
            ActiveSession = await _database.LoadActiveSessionAsync();
            SessionNote = ActiveSession?.Note ?? string.Empty;
            _initialized = true;
            _clock.Start();
            await ReloadRecordsAsync();
        }
        catch (Exception exception) { ShowError("无法读取本地数据", exception); }
        finally { IsBusy = false; }
    }

    public void Navigate(string section)
    {
        CurrentSection = section;
        TopicFilter = string.Empty;
        UpdateFilteredRecords();
        RefreshAnalytics();
    }

    public void ShowTopic(string topic)
    {
        CurrentSection = "Records";
        TopicFilter = topic;
        UpdateFilteredRecords();
    }

    public async Task SetStyleAsync(VisualStyle style) { Style = style; await SavePreferencesAsync(); }
    public async Task SetTrendKindAsync(ChartKind kind) { TrendKind = kind; await SavePreferencesAsync(); }
    public async Task SetTopicKindAsync(ChartKind kind) { TopicKind = kind; await SavePreferencesAsync(); }
    public void SetPeriod(ReportPeriod period) { SelectedPeriod = period; RefreshAnalytics(); }

    // Stop UI refresh on close; the persisted active session continues independently.
    public void StopClock() => _clock.Stop();

    private bool CanToggleTimer() => _initialized && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanToggleTimer))]
    private async Task StartOrStopAsync()
    {
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            if (IsRunning)
            {
                await _database.CompleteSessionAsync(DateTime.Now);
                ActiveSession = null;
                SessionNote = string.Empty;
                ElapsedDisplay = "00:00:00";
                await ReloadRecordsAsync();
            }
            else
            {
                await _database.SaveActiveSessionAsync(new ActiveSession(SessionNote.Trim(), DateTime.Now));
                ActiveSession = await _database.LoadActiveSessionAsync();
                SessionNote = ActiveSession?.Note ?? SessionNote;
            }
        }
        catch (Exception exception) { ShowError("计时未能保存", exception); }
        finally { IsBusy = false; }
    }

    public async Task DeleteRecordAsync(long id)
    {
        try { await _database.DeleteRecordAsync(id); await ReloadRecordsAsync(); }
        catch (Exception exception) { ShowError("删除失败", exception); }
    }

    private async Task SavePreferencesAsync()
    {
        if (!_initialized) return;
        await _preferencesGate.WaitAsync();
        try { await _database.SavePreferencesAsync(new AppPreferences(Style, TrendKind, TopicKind)); }
        catch (Exception exception) { ShowError("无法记住显示偏好", exception); }
        finally { _preferencesGate.Release(); }
    }

    private async Task ReloadRecordsAsync()
    {
        var loaded = await _database.LoadRecordsAsync();
        Records.Clear();
        RecentRecords.Clear();
        foreach (var record in loaded) Records.Add(record);
        foreach (var record in loaded.Take(3)) RecentRecords.Add(record);
        OnPropertyChanged(nameof(NoRecords));
        UpdateFilteredRecords();
        RefreshAnalytics();
    }

    private void UpdateFilteredRecords()
    {
        FilteredRecords.Clear();
        var records = TopicFilter.Length > 0
            ? StudyAnalytics.InPeriod(Records, SelectedPeriod, DateTime.Today).Where(record => record.NoteDisplay.Trim() == TopicFilter)
            : Records.AsEnumerable();
        foreach (var record in records) FilteredRecords.Add(record);
        OnPropertyChanged(nameof(NoFilteredRecords));
        OnPropertyChanged(nameof(RecordsHeading));
    }

    private void RefreshAnalytics()
    {
        var today = DateTime.Today;
        TodayTotalDisplay = StudyAnalytics.FormatDuration(StudyAnalytics.TotalDuration(Records, ReportPeriod.Day, today));
        WeekTotalDisplay = StudyAnalytics.FormatDuration(StudyAnalytics.TotalDuration(Records, ReportPeriod.Week, today));
        SelectedTotalDisplay = StudyAnalytics.FormatDuration(StudyAnalytics.TotalDuration(Records, SelectedPeriod, today));
        SelectedRecordCountDisplay = $"{StudyAnalytics.InPeriod(Records, SelectedPeriod, today).Count}次记录";
        PeriodRange = StudyAnalytics.PeriodLabel(SelectedPeriod, today);
        TrendPoints = StudyAnalytics.Trend(Records, SelectedPeriod, today);
        TopicTotals = StudyAnalytics.Topics(Records, SelectedPeriod, today);
        TopicPoints = TopicTotals.Select(topic => new ChartPoint(topic.Label, topic.Minutes)).ToList();
        OnPropertyChanged(nameof(TopicHint));
    }

    private void ShowError(string title, Exception exception) => ErrorMessage = $"{title}：{exception.Message}";
    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
    partial void OnStyleChanged(VisualStyle value) { OnPropertyChanged(nameof(UseLiquidStyle)); OnPropertyChanged(nameof(IsFrostedStyle)); }
    partial void OnIsBusyChanged(bool value) { StartOrStopCommand.NotifyCanExecuteChanged(); OnPropertyChanged(nameof(CanEditNote)); }
    partial void OnCurrentSectionChanged(string value)
    {
        OnPropertyChanged(nameof(IsOverviewVisible)); OnPropertyChanged(nameof(IsRecordsVisible));
        OnPropertyChanged(nameof(IsAnalyticsVisible)); OnPropertyChanged(nameof(AreChartsVisible)); OnPropertyChanged(nameof(PageTitle));
    }
    partial void OnActiveSessionChanged(ActiveSession? value)
    {
        OnPropertyChanged(nameof(IsRunning)); OnPropertyChanged(nameof(StartButtonText));
        OnPropertyChanged(nameof(SessionStatus)); OnPropertyChanged(nameof(ActiveNoteDisplay)); OnPropertyChanged(nameof(CanEditNote));
    }
}
