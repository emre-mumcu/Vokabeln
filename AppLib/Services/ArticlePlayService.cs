using Microsoft.EntityFrameworkCore;
using Vokabeln.AppData;
using Vokabeln.AppData.Entities;
using Vokabeln.AppData.Enums;
using Vokabeln.AppLib.Filters;
using Vokabeln.ViewModels;

namespace Vokabeln.AppLib.Services;

// Artikel oyunu: ismin artikeli (der/die/das) tahmin edilir. Artikel kelimenin başından okunur ("der Tisch").
public class ArticlePlayService(AppDbContext dbContext, IGameStatsService gameStatsService)
    : GameServiceBase(dbContext, gameStatsService), IArticlePlayService
{
    public static readonly string[] Articles = ["der", "die", "das"];

    public async Task<WordPlayState> StartAsync(int questionCount)
    {
        // Yalnızca artikeliyle kaydedilmiş isimler sorulabilir
        var ids = await Db.Words
            .AsNoTracking()
            .Where(x => x.WordType == WordType.Noun)
            .Where(x => EF.Functions.Like(x.Word, "der %") || EF.Functions.Like(x.Word, "die %") || EF.Functions.Like(x.Word, "das %"))
            .Select(x => x.Id)
            .ToListAsync();

        if (ids.Count == 0)
            throw new AppException("Artikeliyle kaydedilmiş isim bulunamadı (ör. \"der Tisch\").");

        return await CreateGameAsync(new GameEntity { Type = GameType.Article, WordType = WordType.Noun }, ids, questionCount);
    }

    public async Task<ArticleQuestionViewModel?> GetQuestionAsync(WordPlayState state)
    {
        var game = await GetGameAsync(state.GameId);
        var (word, total) = await GetCurrentWordAsync(state);

        if (word is null)
            return null;

        return Build(state, total, await ScoreAsync(game.Id), word);
    }

    public async Task<ArticleQuestionViewModel?> AnswerAsync(WordPlayState state, string article)
    {
        var game = await GetGameAsync(state.GameId);
        var (word, total) = await GetCurrentWordAsync(state);

        if (word is null)
            return null;

        var model = Build(state, total, 0, word);
        var selected = article.Trim().ToLowerInvariant();

        var answer = await SaveAnswerAsync(game, new GameAnswerEntity
        {
            WordId = word.Id,
            Answer = selected,
            IsCorrect = selected == model.CorrectArticle
        });

        model.Answered = true;
        model.SelectedArticle = answer?.Answer ?? selected;
        model.Score = await ScoreAsync(game.Id);

        return model;
    }

    private static ArticleQuestionViewModel Build(WordPlayState state, int total, int score, WordEntity word)
    {
        var (article, noun) = Split(word.Word);

        return new ArticleQuestionViewModel
        {
            State = state,
            Total = total,
            Score = score,
            Word = word,
            Noun = noun,
            CorrectArticle = article
        };
    }

    // "der Tisch" -> ("der", "Tisch")
    private static (string Article, string Noun) Split(string word)
    {
        var w = word.Trim();
        var space = w.IndexOf(' ');

        return space > 0
            ? (w[..space].ToLowerInvariant(), w[(space + 1)..].Trim())
            : ("", w);
    }
}
