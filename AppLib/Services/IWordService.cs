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
	Task<PagedResult<WordEntity>> GetPagedAsync(int page, int pageSize, WordSearch? search);
	Task<List<QuickSearchItem>> QuickSearchAsync(string? q, int take);
	Task DeleteAsync(int id);
	Task<List<WordEntity>> GetAllAsync();
	Task<ImportResult> ImportAsync(IEnumerable<WordExport> words, ImportMode mode);
	Task<List<WordExport>> GetForExportAsync();
	Task<string> CreateBackupAsync();
	Task RestoreAsync(string uploadedDbPath);
	Task<DbStatsViewModel> GetStatsAsync();
	Task<int> DeleteSafetyBackupsAsync();
	DbFilesViewModel GetDbFiles();
	Task<CheckpointResult> CheckpointAsync();
	Task<List<WordEntity>> GetForPrintAsync(WordType? type, bool der, bool die, bool das);
	Task<List<WordEntity>> GetForPrintAsync(WordSearch? search);
	Task<List<TableStat>> GetTablesAsync();
	Task<TableDetail?> GetTableDetailAsync(string table);
	Task<SqlResult> ExecuteSqlAsync(string sql, bool allowWrite);
}
