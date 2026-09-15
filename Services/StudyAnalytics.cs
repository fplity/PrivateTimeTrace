using PrivateTimeTrace.Data;

namespace PrivateTimeTrace.Services;

public static class StudyAnalytics
{
    public static IReadOnlyList<StudyRecord> InPeriod(IEnumerable<StudyRecord> records, ReportPeriod period, DateTime anchor)
    {
        var (start, end) = Range(period, anchor.Date);
        return records.Where(record => Overlap(record.StartedAt, record.EndedAt, start, end) > TimeSpan.Zero).ToList();
    }

    public static IReadOnlyList<ChartPoint> Trend(IEnumerable<StudyRecord> records, ReportPeriod period, DateTime anchor)
    {
        var snapshot = records.ToList();
        var (start, end) = Range(period, anchor.Date);
        var buckets = new List<(string Label, DateTime Start, DateTime End)>();
        switch (period)
        {
            case ReportPeriod.Day:
                for (var hour = 0; hour < 24; hour++)
                {
                    var bucketStart = start.AddHours(hour);
                    buckets.Add(($"{hour:00}", bucketStart, bucketStart.AddHours(1)));
                }
                break;
            case ReportPeriod.Week:
                for (var day = 0; day < 7; day++)
                {
                    var bucketStart = start.AddDays(day);
                    buckets.Add((bucketStart.ToString("M/d"), bucketStart, bucketStart.AddDays(1)));
                }
                break;
            case ReportPeriod.Month:
                for (var day = 0; day < DateTime.DaysInMonth(start.Year, start.Month); day++)
                {
                    var bucketStart = start.AddDays(day);
                    buckets.Add((bucketStart.Day.ToString(), bucketStart, bucketStart.AddDays(1)));
                }
                break;
            case ReportPeriod.Year:
                for (var month = 1; month <= 12; month++)
                {
                    var bucketStart = new DateTime(start.Year, month, 1);
                    buckets.Add(($"{month}月", bucketStart, bucketStart.AddMonths(1)));
                }
                break;
            case ReportPeriod.All:
                var first = snapshot.Select(record => new DateTime(record.StartedAt.Year, record.StartedAt.Month, 1)).DefaultIfEmpty(new DateTime(anchor.Year, anchor.Month, 1)).Min();
                var last = snapshot.Select(record => new DateTime(record.EndedAt.AddTicks(-1).Year, record.EndedAt.AddTicks(-1).Month, 1)).DefaultIfEmpty(first).Max();
                var count = (last.Year - first.Year) * 12 + last.Month - first.Month + 1;
                for (var index = 0; index < count; index++)
                {
                    var bucketStart = first.AddMonths(index);
                    buckets.Add((bucketStart.ToString("yy年M月"), bucketStart, bucketStart.AddMonths(1)));
                }
                break;
        }

        return buckets.Select(bucket => new ChartPoint(
            bucket.Label,
            snapshot.Sum(record => Overlap(record.StartedAt, record.EndedAt, bucket.Start, bucket.End).TotalMinutes))).ToList();
    }

    public static IReadOnlyList<TopicTotal> Topics(IEnumerable<StudyRecord> records, ReportPeriod period = ReportPeriod.All, DateTime? anchor = null) => InPeriod(records, period, anchor ?? DateTime.Today)
        .GroupBy(record => string.IsNullOrWhiteSpace(record.Note) ? "未命名学习" : record.Note.Trim())
        .Select(group => new TopicTotal(group.Key, TotalDuration(group, period, anchor ?? DateTime.Today).TotalMinutes, group.Count()))
        .OrderByDescending(topic => topic.Minutes)
        .ThenBy(topic => topic.Label)
        .ToList();

    public static string PeriodLabel(ReportPeriod period, DateTime anchor) => period switch
    {
        ReportPeriod.Day => $"今天 · {anchor:M/d}",
        ReportPeriod.Week => $"{StartOfWeek(anchor):M/d} — {StartOfWeek(anchor).AddDays(6):M/d}",
        ReportPeriod.Month => $"{anchor:yyyy年M月}",
        ReportPeriod.Year => $"{anchor:yyyy年}",
        ReportPeriod.All => "从第一次记录开始",
        _ => string.Empty,
    };

    public static string FormatDuration(IEnumerable<StudyRecord> records)
    {
        var duration = TimeSpan.FromTicks(records.Sum(record => record.Duration.Ticks));
        return FormatDuration(duration);
    }

    public static string FormatDuration(TimeSpan duration) => duration.TotalHours >= 1
        ? $"{(int)duration.TotalHours}小时 {duration.Minutes}分"
        : duration.TotalMinutes >= 1 ? $"{(int)duration.TotalMinutes}分钟" : $"{Math.Max(0, (int)duration.TotalSeconds)}秒";

    public static TimeSpan TotalDuration(IEnumerable<StudyRecord> records, ReportPeriod period, DateTime anchor)
    {
        var (start, end) = Range(period, anchor.Date);
        return TimeSpan.FromTicks(records.Sum(record => Overlap(record.StartedAt, record.EndedAt, start, end).Ticks));
    }

    private static (DateTime Start, DateTime End) Range(ReportPeriod period, DateTime anchor) => period switch
    {
        ReportPeriod.Day => (anchor, anchor.AddDays(1)),
        ReportPeriod.Week => (StartOfWeek(anchor), StartOfWeek(anchor).AddDays(7)),
        ReportPeriod.Month => (new DateTime(anchor.Year, anchor.Month, 1), new DateTime(anchor.Year, anchor.Month, 1).AddMonths(1)),
        ReportPeriod.Year => (new DateTime(anchor.Year, 1, 1), new DateTime(anchor.Year + 1, 1, 1)),
        ReportPeriod.All => (DateTime.MinValue, DateTime.MaxValue),
        _ => (anchor, anchor.AddDays(1)),
    };

    private static DateTime StartOfWeek(DateTime date)
    {
        var diff = (7 + (int)date.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        return date.Date.AddDays(-diff);
    }

    private static TimeSpan Overlap(DateTime recordStart, DateTime recordEnd, DateTime bucketStart, DateTime bucketEnd)
    {
        var start = recordStart > bucketStart ? recordStart : bucketStart;
        var end = recordEnd < bucketEnd ? recordEnd : bucketEnd;
        return end > start ? end.ToUniversalTime() - start.ToUniversalTime() : TimeSpan.Zero;
    }
}
