namespace Vokabeln.ViewModels
{
	// Kelime formundaki "PONS'tan çek" sonucu (JSON olarak döner)
	public class PonsLookupResult
	{
		// Secret ayarlı değilse ya da istek başarısızsa false; Message nedenini söyler
		public bool Success { get; set; }
		public string? Message { get; set; }

		// Aranan kelime (artikelsiz)
		public string Query { get; set; } = "";

		public List<PonsEntry> Entries { get; set; } = [];
	}

	// PONS'taki bir madde (ör. "Tisch, der · Substantiv") ve anlam grupları
	public class PonsEntry
	{
		public string Headword { get; set; } = "";

		// Dilbilgisi bilgisi düz metin olarak: "Tisch <-(e)s, -e> SUBST m"
		public string? HeadwordFull { get; set; }
		public string? WordClass { get; set; }

		public List<PonsSense> Senses { get; set; } = [];
	}

	public class PonsSense
	{
		// Anlam başlığı (ör. "1. Tisch (Möbelstück):"), olmayabilir
		public string? Header { get; set; }

		public List<PonsTranslation> Translations { get; set; } = [];
	}

	// Almanca ifade ve Türkçe karşılığı (HTML temizlenmiş)
	public record PonsTranslation(string Source, string Target);
}
