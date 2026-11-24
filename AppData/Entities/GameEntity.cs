using Vokabeln.AppData.Enums;

namespace Vokabeln.AppData.Entities;

public class GameEntity
{
    public int Id { get; set; }

    public GameType Type { get; set; }

    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }

    // Oyun başlarken seçilen filtre (anlam oyunu)
    public WordType? WordType { get; set; }
    public ArticleFilter Article { get; set; }

    // Çekim oyununda sorulan zaman
    public Tense? Tense { get; set; }

    public int QuestionCount { get; set; }

    // Oyun bitince doldurulur
    public int AnsweredCount { get; set; }
    public int CorrectCount { get; set; }

    public List<GameAnswerEntity> Answers { get; set; } = [];
}
