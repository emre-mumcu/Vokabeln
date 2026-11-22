using Microsoft.EntityFrameworkCore;
using System;
using Vokabeln.AppData;
using Vokabeln.AppData.Entities;
using Vokabeln.AppData.Enums;
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

	public async Task<List<WordEntity>> GetAllAsync()
	{
		return await dbContext.Words
			.AsNoTracking()
			.OrderBy(x => x.Word)
			.ToListAsync();
	}

	public async Task<ImportResult> ImportAsync(IEnumerable<WordEntity> words, ImportMode mode)
	{
		var existing = (await dbContext.Words
				.AsNoTracking()
				.Select(x => x.Word)
				.ToListAsync())
			.ToHashSet(StringComparer.OrdinalIgnoreCase);

		var inFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		var candidates = new List<WordEntity>();
		var added = new List<string>();
		var skipped = new List<SkippedWord>();
		var failed = new List<FailedWord>();

		// 1) Ayıklama
		foreach (var w in words)
		{
			var word = w.Word?.Trim();

			if (string.IsNullOrEmpty(word))
			{
				skipped.Add(new SkippedWord("(boş)", "Kelime alanı boş"));
				continue;
			}

			if (existing.Contains(word))
			{
				skipped.Add(new SkippedWord(word, "Veritabanında zaten var"));
				continue;
			}

			if (!inFile.Add(word))
			{
				skipped.Add(new SkippedWord(word, "Dosyada birden fazla kez geçiyor"));
				continue;
			}

			candidates.Add(new WordEntity
			{
				Id = 0,
				Word = word,
				WordType = w.WordType,
				Plural = w.Plural?.Trim(),
				Meaning = w.Meaning?.Trim(),
				Sample = w.Sample?.Trim()
			});
		}

		// 2) Kaydetme
		if (mode == ImportMode.Bulk)
		{
			if (candidates.Count > 0)
			{
				dbContext.Words.AddRange(candidates);

				try
				{
					await dbContext.SaveChangesAsync();
					added.AddRange(candidates.Select(x => x.Word));
				}
				catch (DbUpdateException ex)
				{
					// Toplu kayıt tek parça geri alınır: hiçbir kelime eklenmedi
					foreach (var entity in candidates)
						dbContext.Entry(entity).State = EntityState.Detached;

					var error = ex.InnerException?.Message ?? ex.Message;
					failed.AddRange(candidates.Select(x => new FailedWord(x.Word, error)));
				}
			}
		}
		else
		{
			foreach (var entity in candidates)
			{
				dbContext.Words.Add(entity);

				try
				{
					await dbContext.SaveChangesAsync();
					added.Add(entity.Word);
				}
				catch (DbUpdateException ex)
				{
					dbContext.Entry(entity).State = EntityState.Detached;
					failed.Add(new FailedWord(entity.Word, ex.InnerException?.Message ?? ex.Message));
				}
			}
		}

		return new ImportResult(added, skipped, failed);
	}
}
