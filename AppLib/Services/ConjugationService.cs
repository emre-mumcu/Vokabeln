using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Vokabeln.AppData;
using Vokabeln.AppData.Entities;
using Vokabeln.AppData.Enums;
using Vokabeln.AppLib.Filters;
using Vokabeln.ViewModels;

namespace Vokabeln.AppLib.Services;

public class ConjugationService(AppDbContext dbContext) : IConjugationService
{
    // Her zaman kendi tablosunda; sorgular ortak taban sınıf üzerinden yazılır
    private IQueryable<ConjugationBase> Query(Tense tense) => tense switch
    {
        Tense.Praesens => dbContext.Praesens,
        Tense.Praeteritum => dbContext.Praeteritum,
        Tense.Perfekt => dbContext.Perfekt,
        Tense.FuturI => dbContext.FuturI,
        Tense.KonjunktivII => dbContext.KonjunktivII,
        _ => throw new ArgumentOutOfRangeException(nameof(tense))
    };

    private static ConjugationBase Create(Tense tense) => tense switch
    {
        Tense.Praesens => new PraesensEntity(),
        Tense.Praeteritum => new PraeteritumEntity(),
        Tense.Perfekt => new PerfektEntity(),
        Tense.FuturI => new FuturIEntity(),
        Tense.KonjunktivII => new KonjunktivIIEntity(),
        _ => throw new ArgumentOutOfRangeException(nameof(tense))
    };

    public async Task<Dictionary<Tense, ConjugationBase>> GetForWordAsync(int wordId)
    {
        var result = new Dictionary<Tense, ConjugationBase>();

        foreach (var tense in Enum.GetValues<Tense>())
        {
            var conjugation = await FindAsync(wordId, tense);

            if (conjugation is not null)
                result[tense] = conjugation;
        }

        return result;
    }

    public Task<ConjugationBase?> FindAsync(int wordId, Tense tense)
        => Query(tense).AsNoTracking().FirstOrDefaultAsync(x => x.WordId == wordId);

    public async Task<ConjugationFormViewModel?> GetFormAsync(int wordId, Tense tense)
    {
        var word = await dbContext.Words.AsNoTracking().FirstOrDefaultAsync(x => x.Id == wordId);

        if (word is null)
            return null;

        EnsureVerb(word);

        var existing = await FindAsync(wordId, tense);

        return new ConjugationFormViewModel
        {
            WordId = word.Id,
            Tense = tense,
            Word = word.Word,
            Auxiliary = word.Auxiliary,
            Exists = existing is not null,
            Forms = existing?.GetForms().ToList() ?? [null, null, null, null, null, null]
        };
    }

    public async Task SaveAsync(ConjugationFormViewModel form)
    {
        var word = await dbContext.Words.FirstOrDefaultAsync(x => x.Id == form.WordId)
            ?? throw new KeyNotFoundException($"Id: {form.WordId} NOT found!");

        EnsureVerb(word);

        var forms = form.Forms.Select(NormalizeForm).ToList();
        var entity = await Query(form.Tense).FirstOrDefaultAsync(x => x.WordId == word.Id);

        if (forms.All(x => x is null))
        {
            // Tüm alanlar boşaltıldıysa çekim silinir
            if (entity is not null)
                dbContext.Remove(entity);
        }
        else
        {
            if (entity is null)
            {
                entity = Create(form.Tense);
                entity.WordId = word.Id;
                dbContext.Add(entity);
            }

            entity.SetForms(forms);
        }

        // Çekim değişince kelime de güncellenmiş sayılır (mobil uygulamaya aktarım için)
        dbContext.Entry(word).Property(x => x.UpdatedAt).IsModified = true;

        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteAsync(int wordId, Tense tense)
    {
        var entity = await Query(tense).FirstOrDefaultAsync(x => x.WordId == wordId);

        if (entity is null)
            return;

        dbContext.Remove(entity);

        var word = await dbContext.Words.FirstOrDefaultAsync(x => x.Id == wordId);
        if (word is not null)
            dbContext.Entry(word).Property(x => x.UpdatedAt).IsModified = true;

        await dbContext.SaveChangesAsync();
    }

    public Task<List<int>> GetVerbIdsWithAsync(Tense tense)
        => Query(tense)
            .AsNoTracking()
            .Where(x => x.Word!.WordType == WordType.Verb)
            .Where(x => x.Ich != null || x.Du != null || x.ErSieEs != null || x.Wir != null || x.Ihr != null || x.SieSie != null)
            .Select(x => x.WordId)
            .ToListAsync();

    private static void EnsureVerb(WordEntity word)
    {
        if (word.WordType != WordType.Verb)
            throw new AppException("Çekim yalnızca fiiller için eklenebilir.");
    }

    // "  geht/läuft " -> "geht / läuft"; boşsa null
    public static string? NormalizeForm(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var parts = value
            .Split(ConjugationBase.FormSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => Regex.Replace(x, @"\s+", " "))
            .ToList();

        return parts.Count == 0 ? null : string.Join($" {ConjugationBase.FormSeparator} ", parts);
    }
}
