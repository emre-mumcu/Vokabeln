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

	public class WordDetailsViewModel
	{
		public WordEntity Word { get; set; } = new();

		// Yalnızca girilmiş zamanlar bulunur
		public Dictionary<Tense, ConjugationBase> Conjugations { get; set; } = [];
	}
}
