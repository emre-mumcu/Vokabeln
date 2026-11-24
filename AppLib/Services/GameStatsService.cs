using Microsoft.EntityFrameworkCore;
using Vokabeln.AppData;
using Vokabeln.AppData.Enums;
using Vokabeln.AppLib.Extensions;
using Vokabeln.ViewModels;

namespace Vokabeln.AppLib.Services;

public class GameStatsService(AppDbContext dbContext) : IGameStatsService
{
    private const int RecentGameCount = 10;
    private const int MostMissedCount = 10;

    public async Task<GameStatsViewModel> GetStatsAsync(GameType type)
    {
        // Oyun bazlı istatistiklerde yalnızca bitirilmiş ve en az bir soru cevaplanmış oyunlar sayılır
        var finished = dbContext.Games
            .AsNoTracking()
            .Where(x => x.Type == type)
            .Where(x => x.FinishedAt != null && x.AnsweredCount > 0);

        var gameCount = await finished.CountAsync();

        var best = await finished
            .MaxAsync(x => (double?)x.CorrectCount / x.AnsweredCount) ?? 0;

        var lastPlayed = await finished.MaxAsync(x => x.FinishedAt);

        var recentRaw = await finished
            .OrderByDescending(x => x.FinishedAt)
            .Take(RecentGameCount)
            .Select(x => new { x.Type, x.FinishedAt, x.WordType, x.Article, x.Tense, x.CorrectCount, x.AnsweredCount })
            .ToListAsync();

        var recent = recentRaw
            .Select(x => new GameSummary(x.FinishedAt!.Value, FilterText(x.Type, x.WordType, x.Article, x.Tense), x.CorrectCount, x.AnsweredCount))
            .ToList();

        // Cevap bazlı istatistikler (yarıda bırakılan oyunların cevapları da dahil)
        var answers = dbContext.GameAnswers.AsNoTracking().Where(x => x.Game!.Type == type);

        var answerCount = await answers.CountAsync();
        var correctCount = await answers.CountAsync(x => x.IsCorrect);

        var mostMissedRaw = await answers
            .GroupBy(x => new { x.WordId, x.Word!.Word })
            .Select(g => new
            {
                g.Key.Word,
                Wrong = g.Count(x => !x.IsCorrect),
                Correct = g.Count(x => x.IsCorrect)
            })
            .Where(x => x.Wrong > 0)
            .OrderByDescending(x => x.Wrong)
            .ThenBy(x => x.Correct)
            .Take(MostMissedCount)
            .ToListAsync();

        var mostMissed = mostMissedRaw
            .Select(x => new MissedWord(x.Word, x.Wrong, x.Correct))
            .ToList();

        return new GameStatsViewModel
        {
            Type = type,
            GameCount = gameCount,
            AnswerCount = answerCount,
            CorrectCount = correctCount,
            BestPercent = (int)Math.Round(100 * best),
            LastPlayed = lastPlayed,
            RecentGames = recent,
            MostMissed = mostMissed
        };
    }

    public async Task<int> ResetAsync()
    {
        await dbContext.GameAnswers.ExecuteDeleteAsync();
        return await dbContext.Games.ExecuteDeleteAsync();
    }

    private static string FilterText(GameType gameType, WordType? type, ArticleFilter article, Tense? tense)
    {
        if (gameType == GameType.Article)
            return "İsimler";

        if (gameType == GameType.Conjugation)
            return tense?.GetDescription() ?? "-";

        if (type is null)
            return "Tümü";

        if (type == WordType.Noun && article != ArticleFilter.All)
            return $"{type} ({article.ToString().ToLowerInvariant()})";

        return type.Value.ToString();
    }
}
