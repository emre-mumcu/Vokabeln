using Vokabeln.AppData.Entities;
using Vokabeln.AppData.Enums;

namespace Vokabeln.ViewModels
{
	// ---- Artikel oyunu ----

	public class ArticlePlaySetupViewModel
	{
		public int QuestionCount { get; set; } = 10;
	}

	public class ArticleQuestionViewModel
	{
		public WordPlayState State { get; set; } = new();
		public int Total { get; set; }
		public int Score { get; set; }
		public WordEntity Word { get; set; } = new();

		// "der Tisch" -> Noun: "Tisch", CorrectArticle: "der"
		public string Noun { get; set; } = "";
		public string CorrectArticle { get; set; } = "";

		// Cevaplandıktan sonra dolar
		public bool Answered { get; set; }
		public string? SelectedArticle { get; set; }

		public int Number => State.Index + 1;
		public bool IsLast => Number >= Total;
		public bool IsCorrect => string.Equals(SelectedArticle, CorrectArticle, StringComparison.OrdinalIgnoreCase);
	}

	// ---- Çekim oyunu ----

	public record TenseOption(Tense Tense, int VerbCount);

	public class ConjugationPlaySetupViewModel
	{
		public int QuestionCount { get; set; } = 10;
		public Tense Tense { get; set; } = Tense.Praesens;

		// Her zaman için çekimi girilmiş fiil sayısı (formdan gelmez)
		public List<TenseOption> Tenses { get; set; } = [];
	}

	public class ConjugationRow
	{
		public int Index { get; set; }             // ConjugationBase.PersonLabels sırası
		public string Label { get; set; } = "";    // "er/sie/es"
		public string Expected { get; set; } = ""; // "geht" ya da "fragte / frug"
		public string? Given { get; set; }
		public bool IsCorrect { get; set; }
	}

	public class ConjugationQuestionViewModel
	{
		public WordPlayState State { get; set; } = new();
		public int Total { get; set; }
		public int Score { get; set; }
		public WordEntity Word { get; set; } = new();
		public Tense Tense { get; set; }

		// Yalnızca çekimi girilmiş kişiler sorulur
		public List<ConjugationRow> Rows { get; set; } = [];

		public bool Answered { get; set; }

		public int Number => State.Index + 1;
		public bool IsLast => Number >= Total;
		public bool IsCorrect => Rows.Count > 0 && Rows.All(x => x.IsCorrect);
	}

	// ---- Çoğul oyunu ----

	public class PluralPlaySetupViewModel
	{
		public int QuestionCount { get; set; } = 10;

		// Tekil ismin artikeline göre filtre
		public ArticleFilter Article { get; set; } = ArticleFilter.All;
	}

	public class PluralQuestionViewModel
	{
		public WordPlayState State { get; set; } = new();
		public int Total { get; set; }
		public int Score { get; set; }

		public WordEntity Word { get; set; } = new();

		// Cevaplandıktan sonra dolar
		public bool Answered { get; set; }
		public string? Given { get; set; }
		public bool IsCorrect { get; set; }

		public int Number => State.Index + 1;
		public bool IsLast => Number >= Total;
	}
}
