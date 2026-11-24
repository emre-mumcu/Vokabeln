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
			[nameof(Mode)] = Mode.ToString()
		};
	}

	public record QuickSearchItem(int Id, string Word, string? Meaning);
}
