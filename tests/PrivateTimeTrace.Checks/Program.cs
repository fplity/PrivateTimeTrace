using Microsoft.Data.Sqlite;
using PrivateTimeTrace.Data;
using PrivateTimeTrace.Services;

// Tests and UI fixtures never open the user's default database.
// This explicit read-only mode compares original data across an app upgrade.
if (args is ["--fingerprint", var fingerprintPath])
{
    if (!File.Exists(fingerprintPath)) throw new FileNotFoundException(fingerprintPath);
    var original = new StudyDatabase(fingerprintPath);
    var records = await original.LoadRecordsAsync();
    var active = await original.LoadActiveSessionAsync();
    static string Hash(object? value) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(value)));
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { Count = records.Count, RecordsHash = Hash(records.OrderBy(record => record.Id).ToArray()), HasActiveSession = active is not null, ActiveHash = Hash(active) }));
    return;
}
if (args is ["--seed", var seedPath])
{
    if (File.Exists(seedPath)) throw new InvalidOperationException("Refusing to overwrite an existing database.");
    var seed = new StudyDatabase(seedPath);
    await seed.InitializeAsync();
    var monday = DateTime.Today.AddDays(-((7 + (int)DateTime.Today.DayOfWeek - 1) % 7));
    var topics = new[] { "Java 核心", "算法练习", "英语阅读", "设计研习" };
    // A completed historical week plus today's sessions; fixture data only.
    for (var day = -7; day <= 0; day++)
        for (var index = 0; index < 3; index++)
        {
            var start = monday.AddDays(day).AddHours(6 + index * 2);
            if (start > DateTime.Now.AddHours(-3)) start = DateTime.Now.AddHours(-8 + index * 2);
            await seed.AddRecordAsync(Record(start, start.AddMinutes(30 + (day + 7) * 9 + index * 11), topics[(day + 7 + index) % topics.Length]));
        }
    Console.WriteLine($"Seeded isolated fixture: {Path.GetFullPath(seedPath)}");
    return;
}
if (args is ["--inspect", var inspectPath])
{
    if (!File.Exists(inspectPath)) throw new FileNotFoundException(inspectPath);
    var inspect = new StudyDatabase(inspectPath);
    var prefs = await inspect.LoadPreferencesAsync();
    var records = await inspect.LoadRecordsAsync();
    var active = await inspect.LoadActiveSessionAsync();
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { Style = prefs.Style.ToString(), Trend = prefs.TrendKind.ToString(), Topic = prefs.TopicKind.ToString(), Count = records.Count, Active = active is not null, ActiveNote = active?.Note, LatestNote = records.FirstOrDefault()?.Note }));
    return;
}
if (args is ["--assert-settings", var settingsPath, var style, var trend, var topic])
{
    if (!File.Exists(settingsPath)) throw new FileNotFoundException(settingsPath);
    var settings = await new StudyDatabase(settingsPath).LoadPreferencesAsync();
    Check(settings == new AppPreferences(Enum.Parse<VisualStyle>(style), Enum.Parse<ChartKind>(trend), Enum.Parse<ChartKind>(topic)), "UI preferences persisted independently");
    return;
}

var passed = 0;
var root = Path.Combine(Path.GetTempPath(), "PrivateTimeTrace-checks-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
Console.WriteLine($"Isolated test artifacts: {root}");
var anchor = new DateTime(2026, 9, 14);
var crossing = new[] { Record(anchor.AddMinutes(-30), anchor.AddMinutes(30), "Java") };
Run("Cross-midnight: today receives only 30 minutes", () => Near(30, StudyAnalytics.TotalDuration(crossing, ReportPeriod.Day, anchor).TotalMinutes));
Run("Cross-midnight: previous day receives only 30 minutes", () => Near(30, StudyAnalytics.TotalDuration(crossing, ReportPeriod.Day, anchor.AddDays(-1)).TotalMinutes));
Run("Exclusive period end does not double count", () => Check(StudyAnalytics.InPeriod(new[] { Record(anchor.AddHours(-1), anchor) }, ReportPeriod.Day, anchor).Count == 0, "Boundary"));
var set = new[] { crossing[0], Record(anchor.AddDays(-3), anchor.AddDays(-3).AddHours(2), "Java"), Record(anchor.AddHours(1), anchor.AddHours(2), "英语"), Record(anchor.AddMonths(-4), anchor.AddMonths(-4).AddMinutes(50), "  英语  "), Record(anchor.AddDays(-1).AddMinutes(-40), anchor.AddDays(-1).AddMinutes(20), " ") };
foreach (var period in Enum.GetValues<ReportPeriod>())
    Run($"{period}: total = sum(trend) = sum(topics)", () =>
    {
        var total = StudyAnalytics.TotalDuration(set, period, anchor).TotalMinutes;
        Near(total, StudyAnalytics.Trend(set, period, anchor).Sum(point => point.Minutes));
        Near(total, StudyAnalytics.Topics(set, period, anchor).Sum(point => point.Minutes));
    });
Run("All-time trend retains history beyond 12 months", () =>
{
    var longSet = new[] { Record(anchor.AddYears(-2), anchor.AddYears(-2).AddHours(1)), Record(anchor, anchor.AddHours(1)) };
    Check(StudyAnalytics.Trend(longSet, ReportPeriod.All, anchor).Count == 25, "25 months");
    Near(120, StudyAnalytics.Trend(longSet, ReportPeriod.All, anchor).Sum(point => point.Minutes));
});
Run("Topic labels trim whitespace and include unnamed sessions", () =>
{
    var topics = StudyAnalytics.Topics(set);
    Check(topics.Single(topic => topic.Label == "英语").Count == 2, "Grouping");
    Check(topics.Any(topic => topic.Label == "未命名学习"), "Unnamed");
});
Run("Duration shows seconds for short sessions", () => Check(StudyAnalytics.FormatDuration(TimeSpan.FromSeconds(7)) == "7秒", "Seconds"));
Run("Empty charts and totals are safe", () =>
{
    Near(0, StudyAnalytics.Trend(Array.Empty<StudyRecord>(), ReportPeriod.Week, anchor).Sum(x => x.Minutes));
    Check(StudyAnalytics.Topics(Array.Empty<StudyRecord>()).Count == 0, "Empty topics");
});

var path = Path.Combine(root, "test.db");
var db = new StudyDatabase(path);
await db.InitializeAsync();
await Test("Fresh database defaults", async () => Check(await db.LoadPreferencesAsync() == new AppPreferences(), "Defaults"));
await Test("All eight style/chart combinations persist across reopen", async () =>
{
    foreach (var visual in Enum.GetValues<VisualStyle>())
        foreach (var a in Enum.GetValues<ChartKind>())
            foreach (var b in Enum.GetValues<ChartKind>())
            {
                var prefs = new AppPreferences(visual, a, b);
                await db.SavePreferencesAsync(prefs);
                Check(await new StudyDatabase(path).LoadPreferencesAsync() == prefs, "Reopen settings");
            }
});
await Test("Unknown settings fall back safely", async () =>
{
    await using var connection = new SqliteConnection($"Data Source={path}");
    await connection.OpenAsync();
    var command = connection.CreateCommand();
    command.CommandText = "UPDATE app_settings SET value='999'";
    await command.ExecuteNonQueryAsync();
    Check(await db.LoadPreferencesAsync() == new AppPreferences(), "Unknown enum fallback");
});
await Test("Active timer survives reopen and second start cannot overwrite it", async () =>
{
    var active = new ActiveSession("原始专注", DateTime.Now.AddMinutes(-10));
    await db.SaveActiveSessionAsync(active);
    await db.SaveActiveSessionAsync(new ActiveSession("重复开始", DateTime.Now));
    Check(await new StudyDatabase(path).LoadActiveSessionAsync() == active, "Restore active");
});
await Test("Invalid end time leaves active timer intact", async () =>
{
    try { await db.CompleteSessionAsync(DateTime.Now.AddHours(-1)); throw new Exception("Expected invalid end"); }
    catch (InvalidOperationException) { }
    Check(await db.LoadActiveSessionAsync() is not null, "Timer retained");
    Check((await db.LoadRecordsAsync()).Count == 0, "No partial record");
});
await Test("Completing timer atomically saves once and clears active state", async () =>
{
    await db.CompleteSessionAsync(DateTime.Now);
    await db.CompleteSessionAsync(DateTime.Now);
    Check(await db.LoadActiveSessionAsync() is null, "Active cleared");
    Check((await db.LoadRecordsAsync()).Count == 1, "Single record");
});
await Test("Delete removes only requested record", async () =>
{
    var secondId = await db.AddRecordAsync(Record(anchor, anchor.AddMinutes(15), "保留"));
    var first = (await db.LoadRecordsAsync()).Single(record => record.Id != secondId);
    await db.DeleteRecordAsync(first.Id);
    Check((await db.LoadRecordsAsync()).Single().Id == secondId, "Targeted delete");
});
await Test("Invalid record duration is rejected", async () =>
{
    try { await db.AddRecordAsync(Record(anchor, anchor)); throw new Exception("Expected invalid duration"); }
    catch (ArgumentException) { }
    Check((await db.LoadRecordsAsync()).Count == 1, "Invalid not inserted");
});
await Test("Legacy database migrates without losing study data", async () =>
{
    var legacyPath = Path.Combine(root, "legacy.db");
    await using (var connection = new SqliteConnection($"Data Source={legacyPath}"))
    {
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE study_records(id INTEGER PRIMARY KEY AUTOINCREMENT,note TEXT NOT NULL,started_at TEXT NOT NULL,ended_at TEXT NOT NULL); INSERT INTO study_records(note,started_at,ended_at) VALUES ('旧记录','2026-09-14T00:00:00.0000000Z','2026-09-14T01:00:00.0000000Z');";
        await command.ExecuteNonQueryAsync();
    }
    var legacy = new StudyDatabase(legacyPath);
    await legacy.InitializeAsync();
    Check((await legacy.LoadRecordsAsync()).Single().Note == "旧记录", "Legacy retained");
    Check(await legacy.LoadPreferencesAsync() == new AppPreferences(), "New settings");
});
Console.WriteLine($"PASS: {passed} checks");

void Run(string name, Action action) { action(); passed++; Console.WriteLine("PASS " + name); }
async Task Test(string name, Func<Task> action) { await action(); passed++; Console.WriteLine("PASS " + name); }
static void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL " + name); }
static void Near(double expected, double actual) => Check(Math.Abs(expected - actual) < .0001, $"Expected {expected}, actual {actual}");
static StudyRecord Record(DateTime start, DateTime end, string note = "学习") => new() { StartedAt = start, EndedAt = end, Note = note };
