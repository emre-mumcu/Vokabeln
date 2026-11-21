using Microsoft.EntityFrameworkCore;
using System;
using Vokabeln.AppData;
using Vokabeln.AppData.Entities;
using Vokabeln.ViewModels;

namespace Vokabeln.AppLib.Services;

public class WordService(AppDbContext dbContext) : IWordService
{
    public async Task<WordEntity?> GetByIdAsync(int id)
    {
        return await dbContext.Words.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<WordEntity> SaveAsync(WordEntity entity)
    {
        WordEntity target;

        if (entity.Id == 0)
        {
            target = entity;
            dbContext.Words.Add(target);
        }
        else
        {
            target = await dbContext.Words.FindAsync(entity.Id)
                 ?? throw new KeyNotFoundException($"Id: {entity.Id} NOT found!");

            target.Word = entity.Word;
            target.WordType = entity.WordType;
            target.Plural = entity.Plural;
            target.Meaning = entity.Meaning;
            target.Sample = entity.Sample;
        }

        await dbContext.SaveChangesAsync();
        return target;
    }

	public Task<PagedResult<WordEntity>> GetPagedAsync(int page, int pageSize)
	{
		return dbContext.Words
            .AsNoTracking()
			.OrderBy(x => x.Word)
			.ToPagedResultAsync(page, pageSize);
	}

	public async Task DeleteAsync(int id)
	{
		var entity = await dbContext.Words.FindAsync(id)
					 ?? throw new KeyNotFoundException($"Id: {id} NOT found!");

		dbContext.Words.Remove(entity);
		await dbContext.SaveChangesAsync();
	}
}
