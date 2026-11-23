namespace Vokabeln.ViewModels
{
	public record GameSummary(DateTime FinishedAt, string Filter, int Correct, int Answered)
	{
		public int Percent => Answered == 0 ? 0 : (int)Math.Round(100.0 * Correct / Answered);
	}

	public record MissedWord(string Word, int Wrong, int Correct);

	public class GameStatsViewModel
	{
		public int GameCount { get; init; }
		public int AnswerCount { get; init; }
		public int CorrectCount { get; init; }
		public int BestPercent { get; init; }
		public DateTime? LastPlayed { get; init; }

		public List<GameSummary> RecentGames { get; init; } = [];
		public List<MissedWord> MostMissed { get; init; } = [];

		public int AccuracyPercent => AnswerCount == 0 ? 0 : (int)Math.Round(100.0 * CorrectCount / AnswerCount);
	}
}
