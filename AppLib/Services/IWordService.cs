using System;
using Vokabeln.AppData.Entities;
using Vokabeln.ViewModels;

namespace Vokabeln.AppLib.Services;

public interface IWordService
{
    Task<WordEntity?> GetByIdAsync(int id);
    Task<WordEntity> SaveAsync(WordEntity entity);
	Task<PagedResult<WordEntity>> GetPagedAsync(int page, int pageSize);
	Task DeleteAsync(int id);
}
