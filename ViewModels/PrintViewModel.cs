using Vokabeln.AppData.Entities;
using Vokabeln.AppData.Enums;
using Vokabeln.AppLib.Extensions;

namespace Vokabeln.ViewModels
{
	public class PrintViewModel
	{
		public WordType? SelectedType { get; set; }
		public bool Der { get; set; }
		public bool Die { get; set; }
		public bool Das { get; set; }

		// Yalnızca tür "Verb" iken: seçilen zamanlar her fiil için çekim tablosu olarak basılır.
		// Hiçbiri seçilmezse eski düzen (kelime / anlam / örnek tablosu) kullanılır.
		public List<Tense> Tenses { get; set; } = [];

		// Çekim hücreleri boş basılır (alıştırma kağıdı)
		public bool BlankForms { get; set; }

		public List<WordEntity> Words { get; set; } = [];

		public bool IsConjugationLayout => SelectedType == WordType.Verb && Tenses.Count > 0;

		// Çekim tablolu düzende: seçilen zamanlardan en az birinin çekimi girilmemiş fiil sayısı (uyarı için)
		public int MissingConjugationCount => IsConjugationLayout
			? Words.Count(w => Tenses.Any(t => w.ConjugationOf(t)?.HasAnyForm != true))
			: 0;
	}
}
