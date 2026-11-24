using Vokabeln.ViewModels;

namespace Vokabeln.AppLib.Logging;

// Log ve hata kayıtlarının deposu (ayrı SQLite dosyası: Logs.db)
public interface ILogStore
{
    // Logger sağlayıcısı arka planda toplu yazar
    void AddLogs(IReadOnlyList<AppLogEntry> entries);
    Task<PagedResult<AppLogEntry>> GetLogsAsync(AppLogFilter filter, int page, int pageSize);
    Task<int> DeleteLogsAsync(IEnumerable<long> ids);
    Task<int> DeleteAllLogsAsync();

    // Eklenen kaydın numarası döner (kullanıcıya "Hata No" olarak gösterilir)
    Task<long> AddErrorAsync(ErrorLogEntry entry);
    Task<PagedResult<ErrorLogEntry>> GetErrorsAsync(ErrorLogFilter filter, int page, int pageSize);
    Task<ErrorLogEntry?> GetErrorAsync(long id);
    Task<int> DeleteErrorsAsync(IEnumerable<long> ids);
    Task<int> DeleteAllErrorsAsync();
}
