using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Vokabeln.AppData;
using Vokabeln.AppData.Entities;
using Vokabeln.AppData.Enums;
using Vokabeln.ViewModels;

namespace Vokabeln.AppLib.Services;

public class WordService(AppDbContext dbContext) : IWordService
{
	public async Task<WordEntity?> GetByIdAsync(int id)
	{
		return await dbContext.Words.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
	}

	public async Task<WordEntity> SaveAsync(WordEntity entity)
	{
		WordEntity target;

		if (entity.Id == 0)
		{
			target = entity;
			dbContext.Words.Add(target);
		}
		else
		{
			target = await dbContext.Words.FindAsync(entity.Id)
				 ?? throw new KeyNotFoundException($"Id: {entity.Id} NOT found!");

			target.Word = entity.Word;
			target.WordType = entity.WordType;
			target.Plural = entity.Plural;
			target.Meaning = entity.Meaning;
			target.Sample = entity.Sample;
		}

		await dbContext.SaveChangesAsync();
		return target;
	}

	public Task<PagedResult<WordEntity>> GetPagedAsync(int page, int pageSize)
	{
		return dbContext.Words
			.AsNoTracking()
			.OrderBy(x => x.Word)
			.ToPagedResultAsync(page, pageSize);
	}

	public async Task DeleteAsync(int id)
	{
		var entity = await dbContext.Words.FindAsync(id)
					 ?? throw new KeyNotFoundException($"Id: {id} NOT found!");

		dbContext.Words.Remove(entity);
		await dbContext.SaveChangesAsync();
	}

	public async Task<List<WordEntity>> GetAllAsync()
	{
		return await dbContext.Words
			.AsNoTracking()
			.OrderBy(x => x.Word)
			.ToListAsync();
	}

	public async Task<ImportResult> ImportAsync(IEnumerable<WordEntity> words, ImportMode mode)
	{
		var existing = (await dbContext.Words
				.AsNoTracking()
				.Select(x => x.Word)
				.ToListAsync())
			.ToHashSet(StringComparer.OrdinalIgnoreCase);

		var inFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		var candidates = new List<WordEntity>();
		var added = new List<string>();
		var skipped = new List<SkippedWord>();
		var failed = new List<FailedWord>();

		// 1) Ayıklama
		foreach (var w in words)
		{
			var word = w.Word?.Trim();

			if (string.IsNullOrEmpty(word))
			{
				skipped.Add(new SkippedWord("(boş)", "Kelime alanı boş"));
				continue;
			}

			if (existing.Contains(word))
			{
				skipped.Add(new SkippedWord(word, "Veritabanında zaten var"));
				continue;
			}

			if (!inFile.Add(word))
			{
				skipped.Add(new SkippedWord(word, "Dosyada birden fazla kez geçiyor"));
				continue;
			}

			candidates.Add(new WordEntity
			{
				Id = 0,
				Word = word,
				WordType = w.WordType,
				Plural = w.Plural?.Trim(),
				Meaning = w.Meaning?.Trim(),
				Sample = w.Sample?.Trim()
			});
		}

		// 2) Kaydetme
		if (mode == ImportMode.Bulk)
		{
			if (candidates.Count > 0)
			{
				dbContext.Words.AddRange(candidates);

				try
				{
					await dbContext.SaveChangesAsync();
					added.AddRange(candidates.Select(x => x.Word));
				}
				catch (DbUpdateException ex)
				{
					// Toplu kayıt tek parça geri alınır: hiçbir kelime eklenmedi
					foreach (var entity in candidates)
						dbContext.Entry(entity).State = EntityState.Detached;

					var error = ex.InnerException?.Message ?? ex.Message;
					failed.AddRange(candidates.Select(x => new FailedWord(x.Word, error)));
				}
			}
		}
		else
		{
			foreach (var entity in candidates)
			{
				dbContext.Words.Add(entity);

				try
				{
					await dbContext.SaveChangesAsync();
					added.Add(entity.Word);
				}
				catch (DbUpdateException ex)
				{
					dbContext.Entry(entity).State = EntityState.Detached;
					failed.Add(new FailedWord(entity.Word, ex.InnerException?.Message ?? ex.Message));
				}
			}
		}

		return new ImportResult(added, skipped, failed);
	}

	public async Task<string> CreateBackupAsync()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), $"vokabeln-{Guid.NewGuid():N}.db");
		await BackupToAsync(tempPath);
		return tempPath;
	}

	private async Task BackupToAsync(string destinationPath)
	{
		var source = (SqliteConnection)dbContext.Database.GetDbConnection();
		await dbContext.Database.OpenConnectionAsync();

		try
		{
			using var destination = new SqliteConnection($"Data Source={destinationPath};Pooling=False");
			source.BackupDatabase(destination);
		}
		finally
		{
			await dbContext.Database.CloseConnectionAsync();
		}
	}

	public async Task<string> CreateBackupAsyncEski()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), $"vokabeln-{Guid.NewGuid():N}.db");

		var source = (SqliteConnection)dbContext.Database.GetDbConnection();
		await dbContext.Database.OpenConnectionAsync();

		try
		{
			// Pooling=False: kopya bağlantısı kapanınca dosya kilidi hemen bırakılsın
			using var destination = new SqliteConnection($"Data Source={tempPath};Pooling=False");
			source.BackupDatabase(destination);
		}
		finally
		{
			await dbContext.Database.CloseConnectionAsync();
		}

		return tempPath;
	}

	public async Task RestoreAsync(string uploadedDbPath)
	{
		var uploadedConnectionString = $"Data Source={uploadedDbPath};Mode=ReadOnly;Pooling=False";

		// 1) Yüklenen dosyayı doğrula
		using (var check = new SqliteConnection(uploadedConnectionString))
		{
			try
			{
				await check.OpenAsync();

				using var integrity = check.CreateCommand();
				integrity.CommandText = "PRAGMA integrity_check;";
				var result = (string?)await integrity.ExecuteScalarAsync();

				if (result != "ok")
					throw new InvalidOperationException("Dosya bozuk görünüyor (integrity check başarısız).");

				using var tableCheck = check.CreateCommand();
				tableCheck.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Words';";
				var hasWords = Convert.ToInt32(await tableCheck.ExecuteScalarAsync()) > 0;

				if (!hasWords)
					throw new InvalidOperationException("Bu dosya bir Vokabeln yedeği değil ('Words' tablosu bulunamadı).");
			}
			catch (SqliteException)
			{
				throw new InvalidOperationException("Dosya geçerli bir SQLite veritabanı değil.");
			}
		}

		// 2) Geri yüklemeden önce mevcut verinin güvenlik yedeğini al
		var backupFolder = Path.Combine(AppContext.BaseDirectory, "Backups");
		Directory.CreateDirectory(backupFolder);
		await BackupToAsync(Path.Combine(backupFolder, $"before-restore-{DateTime.Now:yyyyMMdd-HHmmss}.db"));

		// 3) Yüklenen dosyayı çalışan veritabanının üzerine yaz
		var live = (SqliteConnection)dbContext.Database.GetDbConnection();
		await dbContext.Database.OpenConnectionAsync();

		try
		{
			using var source = new SqliteConnection(uploadedConnectionString);
			await source.OpenAsync();
			source.BackupDatabase(live);
		}
		finally
		{
			await dbContext.Database.CloseConnectionAsync();
		}

		// Bellekteki eski varlıkları at
		dbContext.ChangeTracker.Clear();
	}

	public async Task<DbStatsViewModel> GetStatsAsync()
	{
		var conn = (SqliteConnection)dbContext.Database.GetDbConnection();
		await dbContext.Database.OpenConnectionAsync();

		var path = "";
		long pageSize, pageCount, freePages;
		string journalMode, sqliteVersion;
		var tables = new List<TableStat>();

		try
		{
			// Ana veritabanı dosyasının mutlak yolu
			using (var cmd = conn.CreateCommand())
			{
				cmd.CommandText = "PRAGMA database_list;";
				using var reader = await cmd.ExecuteReaderAsync();
				while (await reader.ReadAsync())
				{
					if (reader.GetString(1) == "main")
						path = reader.IsDBNull(2) ? "" : reader.GetString(2);
				}
			}

			pageSize = await ScalarAsync<long>(conn, "PRAGMA page_size;");
			pageCount = await ScalarAsync<long>(conn, "PRAGMA page_count;");
			freePages = await ScalarAsync<long>(conn, "PRAGMA freelist_count;");
			journalMode = await ScalarAsync<string>(conn, "PRAGMA journal_mode;");
			sqliteVersion = await ScalarAsync<string>(conn, "SELECT sqlite_version();");

			// Tablo adları
			var tableNames = new List<string>();
			using (var cmd = conn.CreateCommand())
			{
				cmd.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name;";
				using var reader = await cmd.ExecuteReaderAsync();
				while (await reader.ReadAsync())
					tableNames.Add(reader.GetString(0));
			}

			// Her tablonun kayıt sayısı (ad sqlite_master'dan geldiği için güvenli, yine de tırnaklıyoruz)
			foreach (var name in tableNames)
			{
				var quoted = "\"" + name.Replace("\"", "\"\"") + "\"";
				var count = await ScalarAsync<long>(conn, $"SELECT COUNT(*) FROM {quoted};");
				tables.Add(new TableStat(name, count));
			}
		}
		finally
		{
			await dbContext.Database.CloseConnectionAsync();
		}

		// Kelime istatistikleri
		var words = dbContext.Words.AsNoTracking();

		var total = await words.CountAsync();

		var byTypeRaw = await words
			.GroupBy(x => x.WordType)
			.Select(g => new { Type = g.Key, Count = g.Count() })
			.ToListAsync();

		var byType = byTypeRaw
			.Select(x => new WordTypeStat(x.Type.ToString(), x.Count))
			.OrderByDescending(x => x.Count)
			.ToList();

		var der = await words.CountAsync(x => x.Word.StartsWith("der "));
		var die = await words.CountAsync(x => x.Word.StartsWith("die "));
		var das = await words.CountAsync(x => x.Word.StartsWith("das "));

		var missingMeaning = await words.CountAsync(x => x.Meaning == null || x.Meaning == "");
		var missingSample = await words.CountAsync(x => x.Sample == null || x.Sample == "");

		var nounsMissingPlural = await words.CountAsync(x =>
			(x.Word.StartsWith("der ") || x.Word.StartsWith("die ") || x.Word.StartsWith("das ")) &&
			(x.Plural == null || x.Plural == ""));

		var avgLength = await words.AverageAsync(x => (double?)x.Word.Length) ?? 0;

		var longest = await words
			.OrderByDescending(x => x.Word.Length)
			.Select(x => x.Word)
			.FirstOrDefaultAsync();

		var duplicateGroups = await words
			.GroupBy(x => x.Word.ToLower())
			.Where(g => g.Count() > 1)
			.CountAsync();

		// Güvenlik yedekleri
		var backupFolder = Path.Combine(AppContext.BaseDirectory, "Backups");
		var backupFiles = Directory.Exists(backupFolder)
			? new DirectoryInfo(backupFolder).GetFiles("before-restore-*.db")
			: [];

		return new DbStatsViewModel
		{
			FilePath = path,
			FileSizeBytes = FileSize(path),
			WalSizeBytes = FileSize(path + "-wal"),
			ShmSizeBytes = FileSize(path + "-shm"),
			PageSize = pageSize,
			PageCount = pageCount,
			FreePages = freePages,
			JournalMode = journalMode,
			SqliteVersion = sqliteVersion,
			DotNetVersion = RuntimeInformation.FrameworkDescription,
			Tables = tables,

			TotalWords = total,
			ByType = byType,
			DerCount = der,
			DieCount = die,
			DasCount = das,
			MissingMeaning = missingMeaning,
			MissingSample = missingSample,
			NounsMissingPlural = nounsMissingPlural,
			AverageWordLength = avgLength,
			LongestWord = longest,
			DuplicateGroups = duplicateGroups,

			BackupCount = backupFiles.Length,
			BackupBytes = backupFiles.Sum(f => f.Length),
			LastBackup = backupFiles.Length > 0 ? backupFiles.Max(f => f.LastWriteTime) : null
		};
	}

	private static async Task<T> ScalarAsync<T>(SqliteConnection connection, string sql)
	{
		using var cmd = connection.CreateCommand();
		cmd.CommandText = sql;
		var result = await cmd.ExecuteScalarAsync();
		return (T)Convert.ChangeType(result!, typeof(T), CultureInfo.InvariantCulture);
	}

	private static long FileSize(string path)
		=> !string.IsNullOrEmpty(path) && File.Exists(path) ? new FileInfo(path).Length : 0;

	public Task<int> DeleteSafetyBackupsAsync()
	{
		var folder = Path.Combine(AppContext.BaseDirectory, "Backups");

		if (!Directory.Exists(folder))
			return Task.FromResult(0);

		var deleted = 0;

		foreach (var file in Directory.GetFiles(folder, "before-restore-*.db"))
		{
			try
			{
				File.Delete(file);
				deleted++;
			}
			catch (IOException)
			{
				// Dosya kullanımdaysa atla
			}
		}

		return Task.FromResult(deleted);
	}

	public async Task<List<WordEntity>> GetForPrintAsync(WordType? type, bool der, bool die, bool das)
	{
		var query = dbContext.Words.AsNoTracking().AsQueryable();

		if (type is not null)
			query = query.Where(x => x.WordType == type.Value);

		// Artikel filtresi yalnızca isimlerde ve hepsi seçili değilse uygulanır
		if (type == WordType.Noun && !(der && die && das))
		{
			query = query.Where(x =>
				(der && x.Word.StartsWith("der ")) ||
				(die && x.Word.StartsWith("die ")) ||
				(das && x.Word.StartsWith("das ")));
		}

		var list = await query.ToListAsync();

		// Sıralama: artikeli yok sayarak (der Tisch -> "Tisch"), Almanca alfabeye göre
		var comparer = StringComparer.Create(new CultureInfo("de-DE"), ignoreCase: true);

		return list
			.OrderBy(x => SortKey(x.Word), comparer)
			.ThenBy(x => x.Word, comparer)
			.ToList();
	}

	private static string SortKey(string word)
	{
		var w = word.Trim();

		foreach (var article in new[] { "der ", "die ", "das " })
		{
			if (w.StartsWith(article, StringComparison.OrdinalIgnoreCase))
				return w[article.Length..];
		}

		return w;
	}

	private const int MaxResultRows = 1000;

	private static string Quote(string name) => "\"" + name.Replace("\"", "\"\"") + "\"";

	public async Task<List<TableStat>> GetTablesAsync()
	{
		var conn = (SqliteConnection)dbContext.Database.GetDbConnection();
		await dbContext.Database.OpenConnectionAsync();

		try
		{
			var names = new List<string>();

			using (var cmd = conn.CreateCommand())
			{
				cmd.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name;";
				using var reader = await cmd.ExecuteReaderAsync();
				while (await reader.ReadAsync())
					names.Add(reader.GetString(0));
			}

			var result = new List<TableStat>();

			foreach (var name in names)
			{
				var count = await ScalarAsync<long>(conn, $"SELECT COUNT(*) FROM {Quote(name)};");
				result.Add(new TableStat(name, count));
			}

			return result;
		}
		finally
		{
			await dbContext.Database.CloseConnectionAsync();
		}
	}

	public async Task<TableDetail?> GetTableDetailAsync(string table)
	{
		var conn = (SqliteConnection)dbContext.Database.GetDbConnection();
		await dbContext.Database.OpenConnectionAsync();

		try
		{
			// Tablo gerçekten var mı? (ad parametre olarak veriliyor, SQL'e birleştirilmiyor)
			string? createSql;

			using (var cmd = conn.CreateCommand())
			{
				cmd.CommandText = "SELECT sql FROM sqlite_master WHERE type = 'table' AND name = $name;";
				cmd.Parameters.AddWithValue("$name", table);

				var value = await cmd.ExecuteScalarAsync();
				if (value is null) return null;

				createSql = value as string;
			}

			var columns = new List<ColumnInfo>();

			using (var cmd = conn.CreateCommand())
			{
				cmd.CommandText = "SELECT cid, name, type, \"notnull\", dflt_value, pk FROM pragma_table_info($name);";
				cmd.Parameters.AddWithValue("$name", table);

				using var reader = await cmd.ExecuteReaderAsync();
				while (await reader.ReadAsync())
				{
					columns.Add(new ColumnInfo(
						reader.GetInt32(0),
						reader.GetString(1),
						reader.GetString(2),
						reader.GetInt32(3) != 0,
						reader.IsDBNull(4) ? null : reader.GetString(4),
						reader.GetInt32(5)));
				}
			}

			return new TableDetail(table, columns, createSql);
		}
		finally
		{
			await dbContext.Database.CloseConnectionAsync();
		}
	}

	public async Task<SqlResult> ExecuteSqlAsync(string sql, bool allowWrite)
	{
		var result = new SqlResult();
		var path = await GetDbPathAsync();

		// Havuzsuz, ayrı bir bağlantı: query_only ayarı EF'in bağlantı havuzuna sızmaz
		using var conn = new SqliteConnection($"Data Source={path};Pooling=False");
		await conn.OpenAsync();

		if (!allowWrite)
		{
			using var pragma = conn.CreateCommand();
			pragma.CommandText = "PRAGMA query_only = ON;";
			await pragma.ExecuteNonQueryAsync();
		}

		SqliteTransaction? tx = allowWrite ? (SqliteTransaction)await conn.BeginTransactionAsync() : null;
		var sw = Stopwatch.StartNew();

		try
		{
			using var cmd = conn.CreateCommand();
			cmd.CommandText = sql;
			cmd.CommandTimeout = 30;
			cmd.Transaction = tx;

			using (var reader = await cmd.ExecuteReaderAsync())
			{
				// Birden fazla komut varsa sonuç kümesi olan sonuncusu gösterilir
				do
				{
					if (reader.FieldCount > 0)
					{
						result.Columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList();
						result.Rows = [];
						result.Truncated = false;

						while (await reader.ReadAsync())
						{
							if (result.Rows.Count >= MaxResultRows)
							{
								result.Truncated = true;
								break;
							}

							var row = new string?[reader.FieldCount];
							for (var i = 0; i < row.Length; i++)
								row[i] = FormatCell(reader, i);

							result.Rows.Add(row);
						}
					}
				} while (await reader.NextResultAsync());

				result.RowsAffected = reader.RecordsAffected;
			}

			if (tx is not null)
				await tx.CommitAsync();
		}
		catch (SqliteException ex)
		{
			result.Error = ex.Message; // tx dispose edilince geri alınır
		}
		finally
		{
			if (tx is not null)
				await tx.DisposeAsync();

			sw.Stop();
			result.ElapsedMs = sw.ElapsedMilliseconds;
		}

		// SQL ile veri değiştiyse EF'in önbelleğindeki varlıklar eskimiş olabilir
		if (allowWrite && result.Error is null)
			dbContext.ChangeTracker.Clear();

		return result;
	}

	private async Task<string> GetDbPathAsync()
	{
		var conn = (SqliteConnection)dbContext.Database.GetDbConnection();
		await dbContext.Database.OpenConnectionAsync();

		try
		{
			using var cmd = conn.CreateCommand();
			cmd.CommandText = "PRAGMA database_list;";
			using var reader = await cmd.ExecuteReaderAsync();

			while (await reader.ReadAsync())
			{
				if (reader.GetString(1) == "main" && !reader.IsDBNull(2) && reader.GetString(2).Length > 0)
					return reader.GetString(2);
			}
		}
		finally
		{
			await dbContext.Database.CloseConnectionAsync();
		}

		throw new InvalidOperationException("Veritabanı dosya yolu bulunamadı (bellek içi veritabanı olabilir).");
	}

	private static string? FormatCell(SqliteDataReader reader, int index)
	{
		if (reader.IsDBNull(index)) return null;

		var value = reader.GetValue(index);

		return value switch
		{
			byte[] bytes => $"[BLOB {bytes.Length} bayt]",
			IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
			_ => value.ToString()
		};
	}
}
