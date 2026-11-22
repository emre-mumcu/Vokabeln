namespace Vokabeln.ViewModels
{
	public record TableStat(string Name, long RowCount);
	public record WordTypeStat(string Type, int Count);

	public class DbStatsViewModel
	{
		// Veritabanı
		public string FilePath { get; init; } = "";
		public long FileSizeBytes { get; init; }
		public long WalSizeBytes { get; init; }
		public long ShmSizeBytes { get; init; }
		public long PageSize { get; init; }
		public long PageCount { get; init; }
		public long FreePages { get; init; }
		public string JournalMode { get; init; } = "";
		public string SqliteVersion { get; init; } = "";
		public string DotNetVersion { get; init; } = "";
		public List<TableStat> Tables { get; init; } = [];

		public long LogicalSizeBytes => PageSize * PageCount;
		public long FreeBytes => PageSize * FreePages;

		// Kelimeler
		public int TotalWords { get; init; }
		public List<WordTypeStat> ByType { get; init; } = [];
		public int DerCount { get; init; }
		public int DieCount { get; init; }
		public int DasCount { get; init; }
		public int MissingMeaning { get; init; }
		public int MissingSample { get; init; }
		public int NounsMissingPlural { get; init; }
		public double AverageWordLength { get; init; }
		public string? LongestWord { get; init; }
		public int DuplicateGroups { get; init; }

		// Güvenlik yedekleri (Backups klasörü)
		public int BackupCount { get; init; }
		public long BackupBytes { get; init; }
		public DateTime? LastBackup { get; init; }
	}
}
