using Microsoft.EntityFrameworkCore;
using Vokabeln.AppData;
using Vokabeln.AppData.Entities;
using Vokabeln.AppData.Enums;
using Vokabeln.AppLib.Filters;
using Vokabeln.ViewModels;

namespace Vokabeln.AppLib.Services;

// Anlam oyunu: kelimenin anlamı 4 şıktan seçilir
public class WordPlayService(AppDbContext dbContext, IGameStatsService gameStatsService)
    : GameServiceBase(dbContext, gameStatsService), IWordPlayService
{
    private const int OptionCount = 4;

    public async Task<WordPlayState> StartAsync(int questionCount, WordType? type, ArticleFilter article)
    {
        var ids = await Playable(type, article)
            .Select(x => x.Id)
            .ToListAsync();

        if (ids.Count == 0)
            throw new AppException("Seçilen filtreye uygun, anlamı girilmiş kelime bulunamadı.");

        // Şık üretebilmek için en az iki farklı anlam gerekir
        // (Take kullanılmaz: sıralamasız Take, EF Core'un her oyunda uyarı loglamasına yol açıyordu)
        var meaningCount = await WithMeaning(Db.Words.AsNoTracking())
            .Select(x => x.Meaning)
            .Distinct()
            .CountAsync();

        if (meaningCount < 2)
            throw new AppException("Oyun için anlamı farklı en az 2 kelime gerekli.");

        var game = new GameEntity
        {
            Type = GameType.Meaning,
            WordType = type,
            Article = type == WordType.Noun ? article : ArticleFilter.All
        };

        return await CreateGameAsync(game, ids, questionCount);
    }

    public async Task<WordPlayQuestionViewModel?> GetQuestionAsync(WordPlayState state)
    {
        var game = await GetGameAsync(state.GameId);
        var (word, total) = await GetCurrentWordAsync(state);

        if (word is null)
            return null;

        return new WordPlayQuestionViewModel
        {
            State = state,
            Total = total,
            Score = await ScoreAsync(game.Id),
            Word = word,
            Options = await BuildOptionsAsync(word)
        };
    }

    public async Task<WordPlayQuestionViewModel?> AnswerAsync(WordPlayState state, string? optionIds, int selectedId)
    {
        var game = await GetGameAsync(state.GameId);
        var (word, total) = await GetCurrentWordAsync(state);

        if (word is null)
            return null;

        // Soruda gösterilen şıklar aynı sırayla yeniden kurulur
        var ids = ParseIds(optionIds);

        var meanings = await Db.Words
            .AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .Select(x => new { x.Id, x.Meaning })
            .ToDictionaryAsync(x => x.Id, x => x.Meaning);

        var options = ids
            .Where(id => !string.IsNullOrWhiteSpace(meanings.GetValueOrDefault(id)))
            .Select(id => new QuizOption(id, meanings[id]!.Trim()))
            .ToList();

        // Şıklar eksik ya da değiştirilmişse yenileri üretilir
        if (!options.Any(x => x.Id == word.Id))
            options = await BuildOptionsAsync(word);

        var answer = await SaveAnswerAsync(game, new GameAnswerEntity
        {
            WordId = word.Id,
            SelectedWordId = selectedId,
            IsCorrect = selectedId == word.Id // Doğru şık, kelimenin kendi anlamıdır
        });

        return new WordPlayQuestionViewModel
        {
            State = state,
            Total = total,
            Score = await ScoreAsync(game.Id),
            Word = word,
            Options = options,
            Answered = true,
            SelectedOptionId = answer?.SelectedWordId ?? selectedId
        };
    }

    private IQueryable<WordEntity> Playable(WordType? type, ArticleFilter article)
    {
        var query = WithMeaning(Db.Words.AsNoTracking());

        if (type is not null)
            query = query.Where(x => x.WordType == type.Value);

        // Artikel filtresi yalnızca isimlerde uygulanır
        if (type == WordType.Noun && article != ArticleFilter.All)
        {
            var prefix = article.ToString().ToLowerInvariant() + " "; // "der ", "die ", "das "
            query = query.Where(x => x.Word.StartsWith(prefix));
        }

        return query;
    }

    private static IQueryable<WordEntity> WithMeaning(IQueryable<WordEntity> query)
        => query.Where(x => x.Meaning != null && x.Meaning != "");

    private async Task<List<QuizOption>> BuildOptionsAsync(WordEntity word)
    {
        var correct = word.Meaning?.Trim() ?? "";

        var pool = await WithMeaning(Db.Words.AsNoTracking())
            .Where(x => x.Id != word.Id)
            .Select(x => new { x.Id, x.Meaning, x.WordType })
            .ToListAsync();

        // Yanlış şıklar önce aynı türden seçilir, yetmezse diğer türlerden tamamlanır
        var options = pool
            .Select(x => new { x.Id, Meaning = x.Meaning!.Trim(), x.WordType })
            .Where(x => !string.Equals(x.Meaning, correct, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.WordType == word.WordType ? 0 : 1)
            .ThenBy(_ => Random.Shared.Next())
            .DistinctBy(x => x.Meaning, StringComparer.OrdinalIgnoreCase)
            .Take(OptionCount - 1)
            .Select(x => new QuizOption(x.Id, x.Meaning))
            .Append(new QuizOption(word.Id, correct))
            .ToArray();

        Random.Shared.Shuffle(options);

        return options.ToList();
    }
}
