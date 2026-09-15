using System.Globalization;

namespace PrivateTimeTrace.Data;

public sealed class StudyRecord
{
    public long Id { get; init; }
    public string Note { get; init; } = string.Empty;
    public DateTime StartedAt { get; init; }
    public DateTime EndedAt { get; init; }
    public TimeSpan Duration => EndedAt.ToUniversalTime() - StartedAt.ToUniversalTime();
    public string NoteDisplay => string.IsNullOrWhiteSpace(Note) ? "未命名学习" : Note;
    public string StartedAtDisplay => StartedAt.ToString("yyyy年M月d日 HH:mm", CultureInfo.CurrentCulture);
    public string DurationDisplay => Services.StudyAnalytics.FormatDuration(Duration);
}

public sealed record ActiveSession(string Note, DateTime StartedAt);

public enum ReportPeriod
{
    Day,
    Week,
    Month,
    Year,
    All,
}

public sealed record ChartPoint(string Label, double Minutes);
public sealed record TopicTotal(string Label, double Minutes, int Count);
