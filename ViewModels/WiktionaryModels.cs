using Vokabeln.AppData.Enums;

namespace Vokabeln.ViewModels
{
	// Kelime formundaki "Wiktionary'den getir" butonunun sonucu (JSON olarak döner)
	public class WiktionaryLookupResult
	{
		// Kaynak dosya bulunamadıysa false; Message nedenini söyler
		public bool Available { get; set; }
		public string? Message { get; set; }

		public List<WiktionaryEntry> Entries { get; set; } = [];
	}

	// Kaynak dosyadaki bir sözlük maddesi, uygulamanın alanlarına çevrilmiş haliyle
	public class WiktionaryEntry
	{
		public string Word { get; set; } = "";
		public string Lemma { get; set; } = "";

		// Kaynakta uygulamada olmayan bir tür varsa null
		public WordType? WordType { get; set; }
		public string? WordTypeText { get; set; }

		// Uygulamadaki biçimde: "die Tische"
		public string? Plural { get; set; }
		public string? Meaning { get; set; }
		public string? Sample { get; set; }

		public AuxiliaryVerb? Auxiliary { get; set; }
		public string? PartizipII { get; set; }
		public bool IsSeparable { get; set; }

		// Tense adı -> ConjugationBase.PersonLabels sırasıyla 6 form (ich, du, er/sie/es, wir, ihr, sie/Sie)
		public Dictionary<string, List<string?>> Conjugations { get; set; } = [];

		// Türkçe anlam yoksa yardımcı olsun diye: Almanca açıklamalar ve İngilizce karşılıklar
		public List<string> Glosses { get; set; } = [];
		public List<string> English { get; set; } = [];

		// Çekimli formdan bulunduysa: "ging: 1. Person Singular Indikativ Präteritum Aktiv"
		public string? Via { get; set; }
	}
}
