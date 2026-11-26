using Vokabeln.AppData.Entities;
using Vokabeln.AppData.Enums;

namespace Vokabeln.ViewModels
{
	public class ConjugationFormViewModel
	{
		public int WordId { get; set; }
		public Tense Tense { get; set; }

		// Ekranda gösterim için (formdan gelmez)
		public string Word { get; set; } = "";
		public AuxiliaryVerb? Auxiliary { get; set; }
		public bool Exists { get; set; }

		// ConjugationBase.PersonLabels sırasıyla: ich, du, er/sie/es, wir, ihr, sie/Sie
		public List<string?> Forms { get; set; } = [null, null, null, null, null, null];
	}

	// Detay sayfasındaki "Tüm çekimleri Wiktionary'den getir" sonucu
	public class ConjugationImportResult
	{
		public string? Error { get; set; }

		// Kaynaktaki madde (ör. "anfangen")
		public string? Source { get; set; }

		public List<Tense> Saved { get; set; } = [];

		// Zaten girilmiş olduğu için atlanan (üzerine yazılmadıysa)
		public List<Tense> Skipped { get; set; } = [];

		// Kaynakta bu zamanın çekimi yok
		public List<Tense> Missing { get; set; } = [];
	}

	// Tools -> Çekimleri getir: fiillerin çekim durumu
	public class ConjugationCoverage
	{
		public int VerbCount { get; set; }

		// Beş zamanın hepsi girilmiş fiiller
		public int CompleteCount { get; set; }

		// Zaman -> çekimi girilmiş fiil sayısı
		public Dictionary<Tense, int> ByTense { get; set; } = [];

		public int IncompleteCount => VerbCount - CompleteCount;
	}

	// Tüm fiillerin eksik çekimlerinin Wiktionary'den toplu getirilmesi sonucu
	public class BulkConjugationImportResult
	{
		// İşlem hiç yapılamadıysa (ör. kaynak dosya yok)
		public string? Error { get; set; }

		public string? BackupPath { get; set; }
		public int VerbCount { get; set; }

		// "anfangen (Präsens, Präteritum, ...)"
		public List<string> Filled { get; set; } = [];

		// Zaten tüm zamanları girilmiş olan ya da kaynakta yeni zaman bulunmayan fiiller
		public int UnchangedCount { get; set; }

		// Kaynakta hiç çekimi bulunamayan fiiller
		public List<string> NotFound { get; set; } = [];
	}

	public class WordDetailsViewModel
	{
		public WordEntity Word { get; set; } = new();

		// Yalnızca girilmiş zamanlar bulunur
		public Dictionary<Tense, ConjugationBase> Conjugations { get; set; } = [];
	}
}
