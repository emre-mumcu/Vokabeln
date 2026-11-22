using System;
using Vokabeln.AppData.Entities;
using Vokabeln.AppData.Enums;
using Vokabeln.ViewModels;

namespace Vokabeln.AppLib.Services;

public interface IWordService
{
    Task<WordEntity?> GetByIdAsync(int id);
    Task<WordEntity> SaveAsync(WordEntity entity);
	Task<PagedResult<WordEntity>> GetPagedAsync(int page, int pageSize);
	Task DeleteAsync(int id);
	Task<List<WordEntity>> GetAllAsync();
	Task<ImportResult> ImportAsync(IEnumerable<WordEntity> words, ImportMode mode);
	Task<string> CreateBackupAsync();
	Task RestoreAsync(string uploadedDbPath);
	Task<DbStatsViewModel> GetStatsAsync();
	Task<int> DeleteSafetyBackupsAsync();
	Task<List<WordEntity>> GetForPrintAsync(WordType? type, bool der, bool die, bool das);
	Task<List<TableStat>> GetTablesAsync();
	Task<TableDetail?> GetTableDetailAsync(string table);
	Task<SqlResult> ExecuteSqlAsync(string sql, bool allowWrite);
}
