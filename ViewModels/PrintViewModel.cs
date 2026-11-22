using Vokabeln.AppData.Entities;
using Vokabeln.AppData.Enums;

namespace Vokabeln.ViewModels
{
	public class PrintViewModel
	{
		public WordType? SelectedType { get; set; }
		public bool Der { get; set; }
		public bool Die { get; set; }
		public bool Das { get; set; }
		public List<WordEntity> Words { get; set; } = [];
	}
}
