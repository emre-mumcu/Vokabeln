using System.Globalization;
using Microsoft.Data.Sqlite;
using Vokabeln.ViewModels;

namespace Vokabeln.AppLib.Logging;

// Log ve hata kayıtları uygulama veritabanından ayrı bir SQLite dosyasında tutulur (ConnectionStrings:LogConnection, varsayılan Logs.db):
// Reset DB / Backup / Restore kayıtları etkilemez, EF Core kullanılmadığı için EF'in kendi logları döngü oluşturmaz.
// Tablolar ilk kullanımda oluşturulur (migration gerekmez).
public class SqliteLogStore : ILogStore
{
    private readonly string connectionString;

    public SqliteLogStore(IConfiguration configuration)
    {
        connectionString = configuration.GetConnectionString("LogConnection") ?? "Data Source=Logs.db";
        Initialize();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }

    private async Task<SqliteConnection> OpenAsync()
    {
        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        return connection;
    }

    private void Initialize()
    {
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            PRAGMA journal_mode = WAL;

            CREATE TABLE IF NOT EXISTS AppLogs (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                Timestamp   TEXT    NOT NULL,
                Level       INTEGER NOT NULL,
                Category    TEXT    NOT NULL,
                EventId     INTEGER NOT NULL,
                Message     TEXT    NOT NULL,
                Exception   TEXT    NULL,
                RequestPath TEXT    NULL,
                TraceId     TEXT    NULL
            );
            CREATE INDEX IF NOT EXISTS IX_AppLogs_Level ON AppLogs (Level);

            CREATE TABLE IF NOT EXISTS ErrorLogs (
                Id            INTEGER PRIMARY KEY AUTOINCREMENT,
                Timestamp     TEXT    NOT NULL,
                StatusCode    INTEGER NOT NULL,
                Method        TEXT    NULL,
                Path          TEXT    NULL,
                QueryString   TEXT    NULL,
                ExceptionType TEXT    NULL,
                Message       TEXT    NOT NULL,
                Details       TEXT    NULL,
                UserName      TEXT    NULL,
                UserAgent     TEXT    NULL,
                Referer       TEXT    NULL,
                TraceId       TEXT    NULL
            );
            CREATE INDEX IF NOT EXISTS IX_ErrorLogs_StatusCode ON ErrorLogs (StatusCode);
            """;
        cmd.ExecuteNonQuery();
    }

    // ---------- Log Kayıtları

    public void AddLogs(IReadOnlyList<AppLogEntry> entries)
    {
        if (entries.Count == 0) return;

        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO AppLogs (Timestamp, Level, Category, EventId, Message, Exception, RequestPath, TraceId)
            VALUES ($timestamp, $level, $category, $eventId, $message, $exception, $path, $traceId);
            """;

        var timestamp = cmd.Parameters.Add("$timestamp", SqliteType.Text);
        var level = cmd.Parameters.Add("$level", SqliteType.Integer);
        var category = cmd.Parameters.Add("$category", SqliteType.Text);
        var eventId = cmd.Parameters.Add("$eventId", SqliteType.Integer);
        var message = cmd.Parameters.Add("$message", SqliteType.Text);
        var exception = cmd.Parameters.Add("$exception", SqliteType.Text);
        var path = cmd.Parameters.Add("$path", SqliteType.Text);
        var traceId = cmd.Parameters.Add("$traceId", SqliteType.Text);

        foreach (var e in entries)
        {
            timestamp.Value = ToText(e.Timestamp);
            level.Value = (int)e.Level;
            category.Value = e.Category;
            eventId.Value = e.EventId;
            message.Value = e.Message;
            exception.Value = (object?)e.Exception ?? DBNull.Value;
            path.Value = (object?)e.RequestPath ?? DBNull.Value;
            traceId.Value = (object?)e.TraceId ?? DBNull.Value;
            cmd.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public async Task<PagedResult<AppLogEntry>> GetLogsAsync(AppLogFilter filter, int page, int pageSize)
    {
        var where = new List<string>();
        var parameters = new Dictionary<string, object>();

        if (filter.MinLevel is { } min)
        {
            where.Add("Level >= $level");
            parameters["$level"] = (int)min;
        }

        if (!string.IsNullOrWhiteSpace(filter.Q))
        {
            where.Add("(instr(lower(Message), lower($q)) > 0 OR instr(lower(Category), lower($q)) > 0 OR instr(lower(ifnull(Exception, '')), lower($q)) > 0)");
            parameters["$q"] = filter.Q.Trim();
        }

        return await PageAsync("AppLogs", where, parameters, page, pageSize, ReadLog);
    }

    public Task<int> DeleteLogsAsync(IEnumerable<long> ids) => DeleteAsync("AppLogs", ids);

    public Task<int> DeleteAllLogsAsync() => DeleteAllAsync("AppLogs");

    private static AppLogEntry ReadLog(SqliteDataReader r) => new()
    {
        Id = r.GetInt64(r.GetOrdinal("Id")),
        Timestamp = FromText(r.GetString(r.GetOrdinal("Timestamp"))),
        Level = (LogLevel)r.GetInt32(r.GetOrdinal("Level")),
        Category = r.GetString(r.GetOrdinal("Category")),
        EventId = r.GetInt32(r.GetOrdinal("EventId")),
        Message = r.GetString(r.GetOrdinal("Message")),
        Exception = Nullable(r, "Exception"),
        RequestPath = Nullable(r, "RequestPath"),
        TraceId = Nullable(r, "TraceId")
    };

    // ---------- Hata Kayıtları

    public async Task<long> AddErrorAsync(ErrorLogEntry e)
    {
        await using var connection = await OpenAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO ErrorLogs (Timestamp, StatusCode, Method, Path, QueryString, ExceptionType, Message, Details, UserName, UserAgent, Referer, TraceId)
            VALUES ($timestamp, $status, $method, $path, $query, $type, $message, $details, $user, $agent, $referer, $traceId);
            SELECT last_insert_rowid();
            """;

        cmd.Parameters.AddWithValue("$timestamp", ToText(e.Timestamp));
        cmd.Parameters.AddWithValue("$status", e.StatusCode);
        cmd.Parameters.AddWithValue("$method", (object?)e.Method ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$path", (object?)e.Path ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$query", (object?)e.QueryString ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$type", (object?)e.ExceptionType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$message", e.Message);
        cmd.Parameters.AddWithValue("$details", (object?)e.Details ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$user", (object?)e.UserName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$agent", (object?)e.UserAgent ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$referer", (object?)e.Referer ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$traceId", (object?)e.TraceId ?? DBNull.Value);

        return Convert.ToInt64(await cmd.ExecuteScalarAsync());
    }

    public async Task<PagedResult<ErrorLogEntry>> GetErrorsAsync(ErrorLogFilter filter, int page, int pageSize)
    {
        var where = new List<string>();
        var parameters = new Dictionary<string, object>();

        if (filter.StatusCode is { } status)
        {
            where.Add("StatusCode = $status");
            parameters["$status"] = status;
        }

        if (!string.IsNullOrWhiteSpace(filter.Q))
        {
            where.Add("(instr(lower(Message), lower($q)) > 0 OR instr(lower(ifnull(Path, '')), lower($q)) > 0 OR instr(lower(ifnull(ExceptionType, '')), lower($q)) > 0)");
            parameters["$q"] = filter.Q.Trim();
        }

        return await PageAsync("ErrorLogs", where, parameters, page, pageSize, ReadError);
    }

    public async Task<ErrorLogEntry?> GetErrorAsync(long id)
        => await SingleAsync("ErrorLogs", id, ReadError);

    public Task<int> DeleteErrorsAsync(IEnumerable<long> ids) => DeleteAsync("ErrorLogs", ids);

    public Task<int> DeleteAllErrorsAsync() => DeleteAllAsync("ErrorLogs");

    private static ErrorLogEntry ReadError(SqliteDataReader r) => new()
    {
        Id = r.GetInt64(r.GetOrdinal("Id")),
        Timestamp = FromText(r.GetString(r.GetOrdinal("Timestamp"))),
        StatusCode = r.GetInt32(r.GetOrdinal("StatusCode")),
        Method = Nullable(r, "Method"),
        Path = Nullable(r, "Path"),
        QueryString = Nullable(r, "QueryString"),
        ExceptionType = Nullable(r, "ExceptionType"),
        Message = r.GetString(r.GetOrdinal("Message")),
        Details = Nullable(r, "Details"),
        UserName = Nullable(r, "UserName"),
        UserAgent = Nullable(r, "UserAgent"),
        Referer = Nullable(r, "Referer"),
        TraceId = Nullable(r, "TraceId")
    };

    // ---------- ortak

    // Tablo adları yalnızca bu sınıftan sabit olarak gelir (kullanıcı girdisi değil)
    private async Task<PagedResult<T>> PageAsync<T>(string table, List<string> where, Dictionary<string, object> parameters,
        int page, int pageSize, Func<SqliteDataReader, T> read)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var whereSql = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";

        await using var connection = await OpenAsync();

        int total;
        await using (var count = connection.CreateCommand())
        {
            count.CommandText = $"SELECT COUNT(*) FROM {table} {whereSql};";
            foreach (var (name, value) in parameters) count.Parameters.AddWithValue(name, value);
            total = Convert.ToInt32(await count.ExecuteScalarAsync());
        }

        // Son sayfadan sonrası istenirse (ör. silme sonrası) son sayfa gösterilir
        var lastPage = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Min(page, lastPage);

        var items = new List<T>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = $"SELECT * FROM {table} {whereSql} ORDER BY Id DESC LIMIT $take OFFSET $skip;";
            foreach (var (name, value) in parameters) cmd.Parameters.AddWithValue(name, value);
            cmd.Parameters.AddWithValue("$take", pageSize);
            cmd.Parameters.AddWithValue("$skip", (page - 1) * pageSize);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                items.Add(read(reader));
        }

        return new PagedResult<T> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    private async Task<T?> SingleAsync<T>(string table, long id, Func<SqliteDataReader, T> read) where T : class
    {
        await using var connection = await OpenAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT * FROM {table} WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$id", id);

        await using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? read(reader) : null;
    }

    private async Task<int> DeleteAsync(string table, IEnumerable<long> ids)
    {
        var list = ids.Distinct().ToList();
        if (list.Count == 0) return 0;

        await using var connection = await OpenAsync();
        var deleted = 0;

        // SQLite parametre sınırı için parçalar halinde
        foreach (var chunk in list.Chunk(500))
        {
            await using var cmd = connection.CreateCommand();
            var names = chunk.Select((id, i) =>
            {
                cmd.Parameters.AddWithValue($"$id{i}", id);
                return $"$id{i}";
            }).ToList();

            cmd.CommandText = $"DELETE FROM {table} WHERE Id IN ({string.Join(", ", names)});";
            deleted += await cmd.ExecuteNonQueryAsync();
        }

        return deleted;
    }

    // Tümü silinince numaralar da baştan başlar ve dosya küçültülür
    private async Task<int> DeleteAllAsync(string table)
    {
        await using var connection = await OpenAsync();

        int deleted;
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = $"DELETE FROM {table};";
            deleted = await cmd.ExecuteNonQueryAsync();
        }

        await using (var sequence = connection.CreateCommand())
        {
            sequence.CommandText = "DELETE FROM sqlite_sequence WHERE name = $table;";
            sequence.Parameters.AddWithValue("$table", table);
            await sequence.ExecuteNonQueryAsync();
        }

        await using (var vacuum = connection.CreateCommand())
        {
            vacuum.CommandText = "VACUUM;";
            await vacuum.ExecuteNonQueryAsync();
        }

        return deleted;
    }

    private static string ToText(DateTime utc) => utc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTime FromText(string text) => DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static string? Nullable(SqliteDataReader r, string column)
    {
        var i = r.GetOrdinal(column);
        return r.IsDBNull(i) ? null : r.GetString(i);
    }
}
