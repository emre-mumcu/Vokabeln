namespace Vokabeln.ViewModels
{
	public record SkippedWord(string Word, string Reason);

	public record FailedWord(string Word, string Error);

	// Updated: veritabanında zaten olan ve dosyadan güncellenen kelimeler (değişen alanlarıyla)
	public record ImportResult(
		List<string> Added,
		List<string> Updated,
		List<SkippedWord> Skipped,
		List<FailedWord> Failed)
	{
		public int AddedCount => Added.Count;
		public int UpdatedCount => Updated.Count;
		public int SkippedCount => Skipped.Count;
		public int FailedCount => Failed.Count;
	}
}
