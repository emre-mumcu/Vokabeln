namespace Vokabeln.AppData.Entities;

public class GameAnswerEntity
{
    public int Id { get; set; }

    public int GameId { get; set; }
    public GameEntity? Game { get; set; }

    // Sorulan kelime
    public int WordId { get; set; }
    public WordEntity? Word { get; set; }

    // Anlam oyunu: seçilen şıkkın ait olduğu kelime (o kelime sonradan silinebilir, bu yüzden ilişki yok)
    public int? SelectedWordId { get; set; }

    // Artikel oyunu: seçilen artikel ("der"); çekim oyunu: yazılan çekimler ("gehe|gehst|geht|...")
    public string? Answer { get; set; }

    public bool IsCorrect { get; set; }
    public DateTime AnsweredAt { get; set; }
}
