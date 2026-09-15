using Microsoft.Data.Sqlite;

namespace PrivateTimeTrace.Data;

public sealed class StudyDatabase
{
    private readonly string _connectionString;

    public StudyDatabase(string? databasePath = null)
    {
        databasePath ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PrivateTimeTrace", "private-time-trace.db");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(databasePath))!);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
        }.ToString();
    }

    public async Task InitializeAsync()
    {
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS study_records (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                note TEXT NOT NULL,
                started_at TEXT NOT NULL,
                ended_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS active_session (
                id INTEGER PRIMARY KEY CHECK (id = 1),
                note TEXT NOT NULL,
                started_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS app_settings (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<StudyRecord>> LoadRecordsAsync()
    {
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, note, started_at, ended_at FROM study_records ORDER BY started_at DESC";
        await using var reader = await command.ExecuteReaderAsync();
        var records = new List<StudyRecord>();
        while (await reader.ReadAsync())
        {
            records.Add(new StudyRecord
            {
                Id = reader.GetInt64(0),
                Note = reader.GetString(1),
                StartedAt = DateTime.Parse(reader.GetString(2), null, System.Globalization.DateTimeStyles.RoundtripKind).ToLocalTime(),
                EndedAt = DateTime.Parse(reader.GetString(3), null, System.Globalization.DateTimeStyles.RoundtripKind).ToLocalTime(),
            });
        }
        return records;
    }

    public async Task<long> AddRecordAsync(StudyRecord record)
    {
        if (record.EndedAt.ToUniversalTime() <= record.StartedAt.ToUniversalTime())
            throw new ArgumentException("结束时间必须晚于开始时间。");
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO study_records(note, started_at, ended_at) VALUES ($note, $started, $ended); SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$note", record.Note);
        command.Parameters.AddWithValue("$started", record.StartedAt.ToUniversalTime().ToString("O"));
        command.Parameters.AddWithValue("$ended", record.EndedAt.ToUniversalTime().ToString("O"));
        return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }

    public async Task DeleteRecordAsync(long id)
    {
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM study_records WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<ActiveSession?> LoadActiveSessionAsync()
    {
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT note, started_at FROM active_session WHERE id = 1";
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        return new ActiveSession(
            reader.GetString(0),
            DateTime.Parse(reader.GetString(1), null, System.Globalization.DateTimeStyles.RoundtripKind).ToLocalTime());
    }

    public async Task SaveActiveSessionAsync(ActiveSession session)
    {
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO active_session(id, note, started_at) VALUES (1, $note, $started)
            ON CONFLICT(id) DO NOTHING;
            """;
        command.Parameters.AddWithValue("$note", session.Note);
        command.Parameters.AddWithValue("$started", session.StartedAt.ToUniversalTime().ToString("O"));
        await command.ExecuteNonQueryAsync();
    }

    public async Task ClearActiveSessionAsync()
    {
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM active_session WHERE id = 1";
        await command.ExecuteNonQueryAsync();
    }

    // Saving a record and removing its active timer must succeed or fail together.
    public async Task CompleteSessionAsync(DateTime endedAt)
    {
        await using var connection = await OpenAsync();
        using var transaction = connection.BeginTransaction();
        await using var read = connection.CreateCommand();
        read.Transaction = transaction;
        read.CommandText = "SELECT note, started_at FROM active_session WHERE id = 1";
        string note, started;
        await using (var reader = await read.ExecuteReaderAsync())
        {
            if (!await reader.ReadAsync()) return;
            note = reader.GetString(0);
            started = reader.GetString(1);
        }
        if (endedAt.ToUniversalTime() <= DateTime.Parse(started, null, System.Globalization.DateTimeStyles.RoundtripKind).ToUniversalTime())
            throw new InvalidOperationException("系统时间早于计时起点，请校准时间后再保存。");
        await using var write = connection.CreateCommand();
        write.Transaction = transaction;
        write.CommandText = "INSERT INTO study_records(note, started_at, ended_at) VALUES ($note, $started, $ended); DELETE FROM active_session WHERE id = 1;";
        write.Parameters.AddWithValue("$note", note);
        write.Parameters.AddWithValue("$started", started);
        write.Parameters.AddWithValue("$ended", endedAt.ToUniversalTime().ToString("O"));
        await write.ExecuteNonQueryAsync();
        transaction.Commit();
    }

    public async Task<AppPreferences> LoadPreferencesAsync()
    {
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT key, value FROM app_settings";
        var values = new Dictionary<string, string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) values[reader.GetString(0)] = reader.GetString(1);
        T Read<T>(string key, T fallback) where T : struct, Enum =>
            values.TryGetValue(key, out var text) && Enum.TryParse<T>(text, out var value) && Enum.IsDefined(value) ? value : fallback;
        return new(Read("style", VisualStyle.Liquid), Read("trend_kind", ChartKind.Line), Read("topic_kind", ChartKind.Bar));
    }

    public async Task SavePreferencesAsync(AppPreferences preferences)
    {
        await using var connection = await OpenAsync();
        using var transaction = connection.BeginTransaction();
        foreach (var (key, value) in new[] { ("style", preferences.Style.ToString()), ("trend_kind", preferences.TrendKind.ToString()), ("topic_kind", preferences.TopicKind.ToString()) })
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO app_settings(key,value) VALUES ($key,$value) ON CONFLICT(key) DO UPDATE SET value=excluded.value";
            command.Parameters.AddWithValue("$key", key);
            command.Parameters.AddWithValue("$value", value);
            await command.ExecuteNonQueryAsync();
        }
        transaction.Commit();
    }

    private async Task<SqliteConnection> OpenAsync()
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }
}
