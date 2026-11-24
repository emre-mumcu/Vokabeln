using Vokabeln.AppData.Entities;
using Vokabeln.AppData.Enums;

namespace Vokabeln.ViewModels
{
	public record QuizOption(int Id, string Meaning);

	public class WordPlaySetupViewModel
	{
		public int QuestionCount { get; set; } = 10;
		public WordType? SelectedType { get; set; }
		public ArticleFilter Article { get; set; } = ArticleFilter.All;
	}

	// Oyunun durumu formlarda gizli alanlarla taşınır (session gerekmez).
	// Puan burada değil, veritabanındaki cevaplardan hesaplanır.
	public class WordPlayState
	{
		public int GameId { get; set; }
		public string WordIds { get; set; } = ""; // Sorulacak kelimelerin Id'leri: "12,5,33"
		public int Index { get; set; }
	}

	public class WordPlayQuestionViewModel
	{
		public WordPlayState State { get; set; } = new();
		public int Total { get; set; }
		public int Score { get; set; }
		public WordEntity Word { get; set; } = new();
		public List<QuizOption> Options { get; set; } = [];

		// Cevaplandıktan sonra dolar
		public bool Answered { get; set; }
		public int? SelectedOptionId { get; set; }

		public int Number => State.Index + 1;
		public bool IsLast => Number >= Total;
		public bool IsCorrect => SelectedOptionId == Word.Id; // Doğru şık, kelimenin kendi anlamıdır
	}

	// Üç oyunun ortak sonucu (Views/Shared/GameResult)
	public class WordPlayResultViewModel
	{
		public GameType Type { get; init; }
		public int Score { get; init; }
		public int Answered { get; init; }
		public int QuestionCount { get; init; }

		// Bu oyun dahil, aynı türdeki tüm oyunların istatistikleri
		public GameStatsViewModel Stats { get; init; } = new();

		public int Percent => Answered == 0 ? 0 : (int)Math.Round(100.0 * Score / Answered);
	}
}
