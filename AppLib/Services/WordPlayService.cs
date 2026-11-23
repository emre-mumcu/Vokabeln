using Microsoft.EntityFrameworkCore;
using Vokabeln.AppData;
using Vokabeln.AppData.Entities;
using Vokabeln.AppData.Enums;
using Vokabeln.AppLib.Filters;
using Vokabeln.ViewModels;

namespace Vokabeln.AppLib.Services;

public class WordPlayService(AppDbContext dbContext, IGameStatsService gameStatsService) : IWordPlayService
{
    private const int MinQuestions = 1;
    private const int MaxQuestions = 100;
    private const int OptionCount = 4;

    public async Task<WordPlayState> StartAsync(int questionCount, WordType? type, ArticleFilter article)
    {
        questionCount = Math.Clamp(questionCount, MinQuestions, MaxQuestions);

        var ids = await Playable(type, article)
            .Select(x => x.Id)
            .ToArrayAsync();

        if (ids.Length == 0)
            throw new AppException("Seçilen filtreye uygun, anlamı girilmiş kelime bulunamadı.");

        // Şık üretebilmek için en az iki farklı anlam gerekir
        var meaningCount = await WithMeaning(dbContext.Words.AsNoTracking())
            .Select(x => x.Meaning)
            .Distinct()
            .Take(2)
            .CountAsync();

        if (meaningCount < 2)
            throw new AppException("Oyun için anlamı farklı en az 2 kelime gerekli.");

        Random.Shared.Shuffle(ids);

        var wordIds = ids.Take(questionCount).ToList();

        var game = new GameEntity
        {
            StartedAt = DateTime.Now,
            WordType = type,
            Article = type == WordType.Noun ? article : ArticleFilter.All,
            QuestionCount = wordIds.Count
        };

        dbContext.Games.Add(game);
        await dbContext.SaveChangesAsync();

        return new WordPlayState
        {
            GameId = game.Id,
            WordIds = string.Join(",", wordIds),
            Index = 0
        };
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

        var meanings = await dbContext.Words
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

        // Soru daha önce cevaplandıysa (sayfa yenileme, geri tuşu) ilk cevap geçerlidir
        var answer = await dbContext.GameAnswers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.GameId == game.Id && x.WordId == word.Id);

        // Bitmiş bir oyuna yeni cevap yazılmaz
        if (answer is null && game.FinishedAt is null)
        {
            answer = new GameAnswerEntity
            {
                GameId = game.Id,
                WordId = word.Id,
                SelectedWordId = selectedId,
                IsCorrect = selectedId == word.Id, // Doğru şık, kelimenin kendi anlamıdır
                AnsweredAt = DateTime.Now
            };

            dbContext.GameAnswers.Add(answer);
            await dbContext.SaveChangesAsync();
        }

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

    public async Task<WordPlayResultViewModel> FinishAsync(WordPlayState state)
    {
        var game = await dbContext.Games.FirstOrDefaultAsync(x => x.Id == state.GameId)
            ?? throw new KeyNotFoundException("Oyun bulunamadı (istatistikler sıfırlanmış olabilir).");

        // Sonuç sayfası yenilense de oyun bir kez bitirilir
        if (game.FinishedAt is null)
        {
            game.AnsweredCount = await dbContext.GameAnswers.CountAsync(x => x.GameId == game.Id);
            game.CorrectCount = await ScoreAsync(game.Id);
            game.FinishedAt = DateTime.Now;

            // Hiç cevap verilmeden bitirilen oyun istatistiklere girmez
            if (game.AnsweredCount == 0)
                dbContext.Games.Remove(game);

            await dbContext.SaveChangesAsync();
        }

        return new WordPlayResultViewModel
        {
            Score = game.CorrectCount,
            Answered = game.AnsweredCount,
            QuestionCount = game.QuestionCount,
            Stats = await gameStatsService.GetStatsAsync()
        };
    }

    private async Task<GameEntity> GetGameAsync(int gameId)
        => await dbContext.Games.AsNoTracking().FirstOrDefaultAsync(x => x.Id == gameId)
            ?? throw new KeyNotFoundException("Oyun bulunamadı (istatistikler sıfırlanmış olabilir).");

    private Task<int> ScoreAsync(int gameId)
        => dbContext.GameAnswers.CountAsync(x => x.GameId == gameId && x.IsCorrect);

    private IQueryable<WordEntity> Playable(WordType? type, ArticleFilter article)
    {
        var query = WithMeaning(dbContext.Words.AsNoTracking());

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

    private async Task<(WordEntity? Word, int Total)> GetCurrentWordAsync(WordPlayState state)
    {
        var ids = ParseIds(state.WordIds);

        // Sorular bitti
        if (state.Index < 0 || state.Index >= ids.Count)
            return (null, ids.Count);

        var word = await dbContext.Words
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == ids[state.Index])
            ?? throw new KeyNotFoundException("Sorudaki kelime bulunamadı (oyun sırasında silinmiş olabilir).");

        return (word, ids.Count);
    }

    private async Task<List<QuizOption>> BuildOptionsAsync(WordEntity word)
    {
        var correct = word.Meaning?.Trim() ?? "";

        var pool = await WithMeaning(dbContext.Words.AsNoTracking())
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

    private static List<int> ParseIds(string? csv)
        => (csv ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var id) ? id : 0)
            .Where(id => id > 0)
            .ToList();
}
