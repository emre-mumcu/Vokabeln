namespace Vokabeln.ViewModels
{
	public record SkippedWord(string Word, string Reason);

	public record FailedWord(string Word, string Error);

	public record ImportResult(
		List<string> Added,
		List<SkippedWord> Skipped,
		List<FailedWord> Failed)
	{
		public int AddedCount => Added.Count;
		public int SkippedCount => Skipped.Count;
		public int FailedCount => Failed.Count;
	}
}
