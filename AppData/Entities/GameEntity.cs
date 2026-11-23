using Vokabeln.AppData.Enums;

namespace Vokabeln.AppData.Entities;

public class GameEntity
{
    public int Id { get; set; }

    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }

    // Oyun başlarken seçilen filtre
    public WordType? WordType { get; set; }
    public ArticleFilter Article { get; set; }

    public int QuestionCount { get; set; }

    // Oyun bitince doldurulur
    public int AnsweredCount { get; set; }
    public int CorrectCount { get; set; }

    public List<GameAnswerEntity> Answers { get; set; } = [];
}
