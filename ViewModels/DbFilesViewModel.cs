namespace Vokabeln.ViewModels
{
	public record DbFileState(string Path, bool Exists, long Size);

	// PRAGMA wal_checkpoint sonucu. WalPages = -1 ise veritabanı WAL modunda değildir.
	// TRUNCATE modunda sayfa sayıları WAL sıfırlandıktan sonraki halidir (başarılıysa 0).
	public record CheckpointResult(bool Busy, int WalPages, int CheckpointedPages);

	public class DbFilesViewModel
	{
		public DbFileState Database { get; init; } = new("", false, 0);
		public DbFileState Wal { get; init; } = new("", false, 0);
		public DbFileState Shm { get; init; } = new("", false, 0);
	}
}
