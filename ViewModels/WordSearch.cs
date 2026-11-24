using Vokabeln.AppData.Enums;

namespace Vokabeln.ViewModels
{
	// Navbar'daki hızlı arama yalnızca Q gönderir; diğer alanlar varsayılan değerleriyle (kelime + anlam, içinde geçen) kalır
	public class WordSearch
	{
		public string? Q { get; set; }

		public bool InWord { get; set; } = true;
		public bool InMeaning { get; set; } = true;
		public bool InPlural { get; set; }
		public bool InSample { get; set; }

		public SearchMode Mode { get; set; } = SearchMode.Contains;

		// Kelime ve çoğul alanlarında baştaki der/die/das yok sayılır ("der Tisch" -> "Tisch")
		public bool IgnoreArticle { get; set; }

		public bool IsActive => !string.IsNullOrWhiteSpace(Q);

		// Hiç alan seçilmediyse kelime ve anlamda aranır
		public bool NoFieldSelected => !InWord && !InMeaning && !InPlural && !InSample;

		// Arama ekranına aynı değerlerle dönmek için
		public Dictionary<string, string> ToRouteValues() => new()
		{
			[nameof(Q)] = Q ?? "",
			[nameof(InWord)] = InWord.ToString(),
			[nameof(InMeaning)] = InMeaning.ToString(),
			[nameof(InPlural)] = InPlural.ToString(),
			[nameof(InSample)] = InSample.ToString(),
			[nameof(Mode)] = Mode.ToString(),
			[nameof(IgnoreArticle)] = IgnoreArticle.ToString()
		};

		// Liste ve yazdırma sayfalarındaki özet: "tisch" · Kelime, Anlam · içinde geçen · artikelsiz
		public string Describe()
		{
			var fields = new List<string>();
			if (InWord || NoFieldSelected) fields.Add("Kelime");
			if (InMeaning || NoFieldSelected) fields.Add("Anlam");
			if (InPlural) fields.Add("Çoğul");
			if (InSample) fields.Add("Örnek");

			var mode = Mode switch
			{
				SearchMode.StartsWith => "ile başlayan",
				SearchMode.EndsWith => "ile biten",
				SearchMode.Exact => "tam eşleşen",
				_ => "içinde geçen"
			};

			return $"\"{Q}\" · {string.Join(", ", fields)} · {mode}" + (IgnoreArticle ? " · artikelsiz" : "");
		}
	}

	public record QuickSearchItem(int Id, string Word, string? Meaning);
}
