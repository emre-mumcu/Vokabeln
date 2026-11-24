namespace Vokabeln.AppLib.Logging;

// Log Kayıtları: ILogger ile yazılan her kayıt (seviyeler appsettings "Logging:Database" altından ayarlanır)
public class AppLogEntry
{
    public long Id { get; set; }

    // UTC
    public DateTime Timestamp { get; set; }

    public LogLevel Level { get; set; }
    public string Category { get; set; } = "";
    public int EventId { get; set; }
    public string Message { get; set; } = "";

    // Exception.ToString(): tip, mesaj, stack trace ve iç hatalar
    public string? Exception { get; set; }

    // İstek sırasında yazıldıysa
    public string? RequestPath { get; set; }
    public string? TraceId { get; set; }
}

// Hata Kayıtları: işlenmeyen hatalar (500) ve bulunamayan sayfalar (404)
public class ErrorLogEntry
{
    public long Id { get; set; }

    // UTC
    public DateTime Timestamp { get; set; }

    public int StatusCode { get; set; }
    public string? Method { get; set; }
    public string? Path { get; set; }
    public string? QueryString { get; set; }

    // 404'te boştur
    public string? ExceptionType { get; set; }
    public string Message { get; set; } = "";
    public string? Details { get; set; }

    public string? UserName { get; set; }
    public string? UserAgent { get; set; }
    public string? Referer { get; set; }
    public string? TraceId { get; set; }

    // İstek ve (varsa) hatadan kayıt oluşturur
    public static ErrorLogEntry From(HttpContext context, int statusCode, Exception? exception, string? path = null, string? queryString = null)
    {
        var request = context.Request;

        return new ErrorLogEntry
        {
            Timestamp = DateTime.UtcNow,
            StatusCode = statusCode,
            Method = request.Method,
            Path = path ?? request.Path.Value,
            QueryString = queryString ?? (request.QueryString.HasValue ? request.QueryString.Value : null),
            ExceptionType = exception?.GetType().FullName,
            Message = exception?.Message ?? (statusCode == StatusCodes.Status404NotFound ? "Sayfa bulunamadı" : $"HTTP {statusCode}"),
            Details = exception?.ToString(),
            UserName = context.User.Identity?.IsAuthenticated == true ? context.User.Identity.Name : null,
            UserAgent = request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null,
            Referer = request.Headers.Referer.ToString() is { Length: > 0 } referer ? referer : null,
            TraceId = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier
        };
    }
}

// Liste filtreleri
public class ErrorLogFilter
{
    // null = tümü
    public int? StatusCode { get; set; }
    public string? Q { get; set; }
}

public class AppLogFilter
{
    // Bu seviye ve üstü (null = tümü)
    public LogLevel? MinLevel { get; set; }
    public string? Q { get; set; }
}
