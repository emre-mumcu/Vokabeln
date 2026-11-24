using System.Diagnostics;
using System.Threading.Channels;

namespace Vokabeln.AppLib.Logging;

// ILogger kayıtlarını Log Kayıtları'na (Logs.db) yazar.
// Seviyeler appsettings'te "Logging:Database:LogLevel" altından ayarlanır (ProviderAlias).
// Kayıtlar kuyruğa alınır ve arka planda toplu yazılır: istekler veritabanını beklemez.
// Kuyruk dolarsa (ör. yazma hatası) yeni kayıtlar atılır; uygulama loglama yüzünden yavaşlamaz.
[ProviderAlias("Database")]
public sealed class DatabaseLoggerProvider : ILoggerProvider
{
    private const int QueueSize = 10_000;
    private const int BatchSize = 200;

    private readonly ILogStore store;
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly Channel<AppLogEntry> queue = Channel.CreateBounded<AppLogEntry>(
        new BoundedChannelOptions(QueueSize) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });
    private readonly Task writer;

    public DatabaseLoggerProvider(ILogStore store, IHttpContextAccessor httpContextAccessor)
    {
        this.store = store;
        this.httpContextAccessor = httpContextAccessor;
        writer = Task.Run(WriteLoopAsync);
    }

    public ILogger CreateLogger(string categoryName) => new DatabaseLogger(categoryName, this);

    internal void Enqueue(AppLogEntry entry) => queue.Writer.TryWrite(entry);

    internal string? CurrentRequestPath()
    {
        try
        {
            return httpContextAccessor.HttpContext?.Request.Path.Value;
        }
        catch (ObjectDisposedException)
        {
            return null;
        }
    }

    private async Task WriteLoopAsync()
    {
        var batch = new List<AppLogEntry>(BatchSize);

        while (await queue.Reader.WaitToReadAsync())
        {
            while (batch.Count < BatchSize && queue.Reader.TryRead(out var entry))
                batch.Add(entry);

            try
            {
                store.AddLogs(batch);
            }
            catch (Exception ex)
            {
                // Loglama hatası uygulamayı durdurmaz; yalnızca debug çıktısına yazılır (yeniden loglanırsa döngü olur)
                Debug.WriteLine($"Log kayıtları yazılamadı: {ex.Message}");
            }

            batch.Clear();
        }
    }

    // Uygulama kapanırken kuyruktaki kayıtlar yazılır
    public void Dispose()
    {
        queue.Writer.TryComplete();
        writer.Wait(TimeSpan.FromSeconds(5));
    }

    private sealed class DatabaseLogger(string category, DatabaseLoggerProvider provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        // Seviye filtresi LoggerFactory tarafından (appsettings) uygulanır
        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            provider.Enqueue(new AppLogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = logLevel,
                Category = category,
                EventId = eventId.Id,
                Message = formatter(state, exception),
                Exception = exception?.ToString(),
                RequestPath = provider.CurrentRequestPath(),
                TraceId = Activity.Current?.TraceId.ToString()
            });
        }
    }
}
