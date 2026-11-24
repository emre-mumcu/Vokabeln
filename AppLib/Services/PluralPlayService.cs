using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Vokabeln.AppData;
using Vokabeln.AppData.Entities;
using Vokabeln.AppData.Enums;
using Vokabeln.AppLib.Filters;
using Vokabeln.ViewModels;

namespace Vokabeln.AppLib.Services;

// Çoğul oyunu: tekil isim gösterilir ("der Tisch"), çoğulu yazılır ("Tische" ya da "die Tische").
// Yalnızca çoğulu girilmiş isimler sorulur.
public class PluralPlayService(AppDbContext dbContext, IGameStatsService gameStatsService)
    : GameServiceBase(dbContext, gameStatsService), IPluralPlayService
{
    private static readonly CultureInfo German = new("de-DE");

    public Task<int> CountPlayableAsync() => Playable(ArticleFilter.All).CountAsync();

    public async Task<WordPlayState> StartAsync(int questionCount, ArticleFilter article)
    {
        var ids = await Playable(article).Select(x => x.Id).ToListAsync();

        if (ids.Count == 0)
            throw new AppException("Seçilen filtreye uygun, çoğulu girilmiş isim bulunamadı.");

        return await CreateGameAsync(new GameEntity { Type = GameType.Plural, WordType = WordType.Noun, Article = article }, ids, questionCount);
    }

    public async Task<PluralQuestionViewModel?> GetQuestionAsync(WordPlayState state)
    {
        var game = await GetGameAsync(state.GameId);
        var (word, total) = await GetCurrentWordAsync(state);

        if (word is null)
            return null;

        return new PluralQuestionViewModel
        {
            State = state,
            Total = total,
            Score = await ScoreAsync(game.Id),
            Word = word
        };
    }

    public async Task<PluralQuestionViewModel?> AnswerAsync(WordPlayState state, string given)
    {
        var game = await GetGameAsync(state.GameId);
        var (word, total) = await GetCurrentWordAsync(state);

        if (word is null)
            return null;

        var text = Regex.Replace(given.Trim(), @"\s+", " ");

        var answer = await SaveAnswerAsync(game, new GameAnswerEntity
        {
            WordId = word.Id,
            Answer = text,
            IsCorrect = IsCorrect(text, word.Plural ?? "")
        });

        // Soru daha önce cevaplandıysa ilk cevap gösterilir
        var shown = answer?.Answer ?? text;

        return new PluralQuestionViewModel
        {
            State = state,
            Total = total,
            Score = await ScoreAsync(game.Id),
            Word = word,
            Answered = true,
            Given = shown,
            IsCorrect = IsCorrect(shown, word.Plural ?? "")
        };
    }

    private IQueryable<WordEntity> Playable(ArticleFilter article)
    {
        var query = Db.Words
            .AsNoTracking()
            .Where(x => x.WordType == WordType.Noun)
            .Where(x => x.Plural != null && x.Plural.Trim() != "");

        // Tekil ismin artikeline göre ("der Tisch")
        if (article != ArticleFilter.All)
        {
            var prefix = article.ToString().ToLowerInvariant() + " ";
            query = query.Where(x => x.Word.StartsWith(prefix));
        }

        return query;
    }

    // Esnek kontrol: büyük/küçük harf ve fazla boşluk önemsiz, baştaki "die" yazılsa da yazılmasa da olur.
    // Birden fazla doğru çoğul varsa ("die Kommas / die Kommata") biri ya da birkaçı "/" ile yazılabilir; yazılanların hepsi doğru olmalı.
    public static bool IsCorrect(string? given, string expected)
    {
        var expectedForms = Split(expected).Select(Normalize).Where(x => x.Length > 0).ToHashSet();
        var givenForms = Split(given).Select(Normalize).Where(x => x.Length > 0).ToList();

        return expectedForms.Count > 0 && givenForms.Count > 0 && givenForms.All(expectedForms.Contains);
    }

    private static IEnumerable<string> Split(string? value)
        => (value ?? "").Split(ConjugationBase.FormSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string Normalize(string value)
    {
        var text = Regex.Replace(value.Trim(), @"\s+", " ").ToLower(German);
        return text.StartsWith("die ", StringComparison.Ordinal) ? text[4..].Trim() : text;
    }
}
