using Vokabeln.AppData.Enums;

namespace Vokabeln.ViewModels
{
	// Export/Import JSON biçimi (mobil uygulamada da kullanılacak).
	// Eski export dosyaları (yalnızca word, wordType, plural, meaning, sample) da içe aktarılabilir.
	public class WordExport
	{
		public string Word { get; set; } = "";
		public WordType WordType { get; set; }
		public string? Plural { get; set; }
		public string? Meaning { get; set; }
		public string? Sample { get; set; }

		// Yalnızca fiiller
		public AuxiliaryVerb? Auxiliary { get; set; }
		public string? PartizipII { get; set; }
		public bool IsSeparable { get; set; }

		// UTC
		public DateTime? CreatedAt { get; set; }
		public DateTime? UpdatedAt { get; set; }

		// Yalnızca girilmiş zamanlar: { "Praesens": { "ich": "gehe", ... }, "Perfekt": { ... } }
		public Dictionary<Tense, ConjugationExport>? Conjugations { get; set; }
	}

	// Export ekranındaki seçimler. Dosya biçimi değişmez (WordExport dizisi); seçimler yalnızca içeriği belirler.
	public class ExportSelection
	{
		// Kelime türü filtresi (null = tümü)
		public WordType? SelectedType { get; set; }

		// Dosyaya yazılacak çekim tabloları (boş = çekim yok)
		// Varsayılan boş: hiç işaretlenmezse formdan değer gelmez (ekran açılışında hepsi işaretli gelir)
		public List<Tense> Tenses { get; set; } = [];
	}

	// Import ekranındaki seçimler
	public class ImportOptions
	{
		public ImportMode Mode { get; set; } = ImportMode.Bulk;

		// Veritabanında zaten olan kelimeler
		public ExistingWordMode Existing { get; set; } = ExistingWordMode.Skip;

		// Dosyadan alınacak çekim tabloları (boş = çekimler alınmaz)
		// Varsayılan boş: hiç işaretlenmezse formdan değer gelmez (ekran açılışında hepsi işaretli gelir)
		public List<Tense> Tenses { get; set; } = [];
	}

	// Bir kişi için birden fazla doğru form "/" ile ayrılır: "fragte / frug"
	public class ConjugationExport
	{
		public string? Ich { get; set; }
		public string? Du { get; set; }
		public string? ErSieEs { get; set; }
		public string? Wir { get; set; }
		public string? Ihr { get; set; }
		public string? SieSie { get; set; }
	}
}
