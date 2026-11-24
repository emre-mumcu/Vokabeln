using Microsoft.EntityFrameworkCore;
using Vokabeln.AppData;
using Vokabeln.AppData.Entities;
using Vokabeln.AppLib.Filters;
using Vokabeln.ViewModels;

namespace Vokabeln.AppLib.Services;

// Anlam, artikel ve çekim oyunlarının ortak işleri: oyun kaydı, soru sırası, cevap kaydı, puan ve bitirme.
// Oyun durumu (WordPlayState) formlarda gizli alanlarla taşınır; puan veritabanındaki cevaplardan hesaplanır.
public abstract class GameServiceBase(AppDbContext dbContext, IGameStatsService gameStatsService)
{
    protected const int MinQuestions = 1;
    protected const int MaxQuestions = 100;

    protected AppDbContext Db => dbContext;

    // Aday kelimeler karıştırılır, istenen sayıda soru seçilir ve oyun kaydedilir
    protected async Task<WordPlayState> CreateGameAsync(GameEntity game, IEnumerable<int> candidateIds, int questionCount)
    {
        var ids = candidateIds.Distinct().ToArray();

        if (ids.Length == 0)
            throw new AppException("Seçilen filtreye uygun kelime bulunamadı.");

        Random.Shared.Shuffle(ids);

        var wordIds = ids.Take(Math.Clamp(questionCount, MinQuestions, MaxQuestions)).ToList();

        game.StartedAt = DateTime.Now;
        game.QuestionCount = wordIds.Count;

        dbContext.Games.Add(game);
        await dbContext.SaveChangesAsync();

        return new WordPlayState
        {
            GameId = game.Id,
            WordIds = string.Join(",", wordIds),
            Index = 0
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
            Type = game.Type,
            Score = game.CorrectCount,
            Answered = game.AnsweredCount,
            QuestionCount = game.QuestionCount,
            Stats = await gameStatsService.GetStatsAsync(game.Type)
        };
    }

    protected async Task<GameEntity> GetGameAsync(int gameId)
        => await dbContext.Games.AsNoTracking().FirstOrDefaultAsync(x => x.Id == gameId)
            ?? throw new KeyNotFoundException("Oyun bulunamadı (istatistikler sıfırlanmış olabilir).");

    protected Task<int> ScoreAsync(int gameId)
        => dbContext.GameAnswers.CountAsync(x => x.GameId == gameId && x.IsCorrect);

    protected async Task<(WordEntity? Word, int Total)> GetCurrentWordAsync(WordPlayState state)
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

    // Soru daha önce cevaplandıysa (sayfa yenileme, geri tuşu) ilk cevap geçerlidir; bitmiş oyuna yeni cevap yazılmaz.
    // Kaydedilen ya da daha önce kaydedilmiş cevabı döndürür (bitmiş oyunda ilk kez cevaplanan soru için null).
    protected async Task<GameAnswerEntity?> SaveAnswerAsync(GameEntity game, GameAnswerEntity answer)
    {
        var existing = await dbContext.GameAnswers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.GameId == game.Id && x.WordId == answer.WordId);

        if (existing is not null || game.FinishedAt is not null)
            return existing;

        answer.GameId = game.Id;
        answer.AnsweredAt = DateTime.Now;

        dbContext.GameAnswers.Add(answer);
        await dbContext.SaveChangesAsync();

        return answer;
    }

    protected static List<int> ParseIds(string? csv)
        => (csv ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var id) ? id : 0)
            .Where(id => id > 0)
            .ToList();
}
