using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Vokabeln.AppData;
using Vokabeln.AppData.Entities;
using Vokabeln.AppData.Enums;
using Vokabeln.AppLib.Extensions;
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
            target.Auxiliary = entity.Auxiliary;
            target.PartizipII = entity.PartizipII;
            target.IsSeparable = entity.IsSeparable;
        }

        // Fiil bilgileri yalnızca fiillerde tutulur
        if (target.WordType != WordType.Verb)
        {
            target.Auxiliary = null;
            target.PartizipII = null;
            target.IsSeparable = false;
        }
        else
        {
            target.PartizipII = string.IsNullOrWhiteSpace(target.PartizipII) ? null : target.PartizipII.Trim();
        }

        // CreatedAt/UpdatedAt AppDbContext tarafından doldurulur
        await dbContext.SaveChangesAsync();
        return target;
    }

    public Task<PagedResult<WordEntity>> GetPagedAsync(int page, int pageSize)
        => GetPagedAsync(page, pageSize, null);

    public Task<PagedResult<WordEntity>> GetPagedAsync(int page, int pageSize, WordSearch? search)
    {
        var query = dbContext.Words.AsNoTracking();

        if (search is { IsActive: true })
            query = ApplySearch(query, search);

        return query
            .OrderBy(x => x.Word)
            .ToPagedResultAsync(page, pageSize);
    }

    public async Task<List<QuickSearchItem>> QuickSearchAsync(string? q, int take)
    {
        q = q?.Trim();

        if (string.IsNullOrEmpty(q) || q.Length < MinQuickSearchLength)
            return [];

        var text = EscapeLike(q);
        var contains = $"%{text}%";
        var starts = $"{text}%";
        var ends = $"%{text}";
        var startsAfterArticle = $"___ {text}%"; // "der Tisch", "die Lampe" gibi: "Tisch" ile başlayan isim sayılır

        // Sıralama: kelime ile başlayanlar, kelime ile bitenler, anlamı ile başlayanlar, diğerleri
        return await dbContext.Words
            .AsNoTracking()
            .Where(x => EF.Functions.Like(x.Word, contains, LikeEscape)
                     || EF.Functions.Like(x.Meaning!, contains, LikeEscape))
            .OrderBy(x => EF.Functions.Like(x.Word, starts, LikeEscape) || EF.Functions.Like(x.Word, startsAfterArticle, LikeEscape) ? 0
                        : EF.Functions.Like(x.Word, ends, LikeEscape) ? 1
                        : EF.Functions.Like(x.Meaning!, starts, LikeEscape) ? 2
                        : 3)
            .ThenBy(x => x.Word)
            .Take(take)
            .Select(x => new QuickSearchItem(x.Id, x.Word, x.Meaning))
            .ToListAsync();
    }

    private const int MinQuickSearchLength = 2;
    private const string LikeEscape = "\\";

    // Aranan metindeki % ve _ joker karakter olarak değil, harf olarak aransın
    private static string EscapeLike(string text)
        => text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private static IQueryable<WordEntity> ApplySearch(IQueryable<WordEntity> query, WordSearch search)
    {
        if (search.SelectedType is { } type)
            query = query.Where(x => x.WordType == type);

        // Metin girilmediyse yalnızca tür filtresi uygulanır (seçilen türün tüm kelimeleri)
        if (!search.HasText)
            return query;

        var text = EscapeLike(search.Q!.Trim());

        var pattern = search.Mode switch
        {
            SearchMode.StartsWith => $"{text}%",
            SearchMode.EndsWith => $"%{text}",
            SearchMode.Exact => text, // Joker karakter yok: büyük/küçük harf duyarsız tam eşleşme
            _ => $"%{text}%"
        };

        var inWord = search.InWord || search.NoFieldSelected;
        var inMeaning = search.InMeaning || search.NoFieldSelected;
        var inPlural = search.InPlural;
        var inSample = search.InSample;
        var ignoreArticle = search.IgnoreArticle;

        // NULL alanlarda LIKE sonucu NULL döner ve kayıt eşleşmemiş sayılır.
        // Artikel yok sayılırsa kelime ve çoğul, "der "/"die "/"das " ile başlıyorsa 5. karakterden itibaren karşılaştırılır
        // (LIKE büyük/küçük harf duyarsız olduğu için "Der Tisch" da yakalanır).
        return query.Where(x =>
            (inWord && EF.Functions.Like(
                ignoreArticle && (EF.Functions.Like(x.Word, "der %") || EF.Functions.Like(x.Word, "die %") || EF.Functions.Like(x.Word, "das %"))
                    ? x.Word.Substring(4)
                    : x.Word,
                pattern, LikeEscape)) ||
            (inMeaning && EF.Functions.Like(x.Meaning!, pattern, LikeEscape)) ||
            (inPlural && EF.Functions.Like(
                ignoreArticle && (EF.Functions.Like(x.Plural!, "der %") || EF.Functions.Like(x.Plural!, "die %") || EF.Functions.Like(x.Plural!, "das %"))
                    ? x.Plural!.Substring(4)
                    : x.Plural!,
                pattern, LikeEscape)) ||
            (inSample && EF.Functions.Like(x.Sample!, pattern, LikeEscape)));
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

    // Eski export dosyaları (yalnızca word, wordType, plural, meaning, sample) da aynı biçimle okunur;
    // dosyada olmayan alanlar boş sayılır ve mevcut kelimelerde hiçbir zaman silinmez.
    public async Task<ImportResult> ImportAsync(IEnumerable<WordExport> words, ImportOptions options)
    {
        var existing = (await dbContext.Words
            .AsNoTracking()
            .Select(x => new { x.Id, x.Word, x.WordType })
            .ToListAsync()
        )
        .ToDictionary(x => (x.Word, x.WordType), x => x.Id, WordKeyComparer.Instance);

        var tenses = options.Tenses.ToHashSet();
        var inFile = new HashSet<(string Word, WordType Type)>(WordKeyComparer.Instance);

        var candidates = new List<WordEntity>();
        var updates = new List<(int Id, string Word, WordExport Source)>();
        var added = new List<string>();
        var updated = new List<string>();
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

            var key = (word, w.WordType);

            if (!inFile.Add(key))
            {
                skipped.Add(new SkippedWord(word, $"Dosyada aynı türle birden fazla kez geçiyor ({w.WordType})"));
                continue;
            }

            if (existing.TryGetValue(key, out var id))
            {
                if (options.Existing == ExistingWordMode.Skip)
                    skipped.Add(new SkippedWord(word, $"Veritabanında zaten var ({w.WordType})"));
                else
                    updates.Add((id, word, w));

                continue;
            }

            candidates.Add(FromExport(w, word, tenses));
        }

        // 2) Kaydetme
        if (options.Mode == ImportMode.Bulk)
        {
            // Tek parça: bir kelime hata verirse hiçbir ekleme/güncelleme kaydedilmez
            var changes = new List<(string Word, string Change)>();

            dbContext.Words.AddRange(candidates);

            foreach (var (id, word, source) in updates)
            {
                var change = await ApplyUpdateAsync(id, source, options.Existing, tenses);

                if (change is null)
                    skipped.Add(new SkippedWord(word, "Veritabanında zaten var; değişecek alan yok"));
                else
                    changes.Add((word, change));
            }

            if (candidates.Count > 0 || changes.Count > 0)
            {
                try
                {
                    await dbContext.SaveChangesAsync();
                    added.AddRange(candidates.Select(x => x.Word));
                    updated.AddRange(changes.Select(x => $"{x.Word} ({x.Change})"));
                }
                catch (DbUpdateException ex)
                {
                    dbContext.ChangeTracker.Clear();

                    var error = ex.InnerException?.Message ?? ex.Message;
                    failed.AddRange(candidates.Select(x => new FailedWord(x.Word, error)));
                    failed.AddRange(changes.Select(x => new FailedWord(x.Word, error)));
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
                    dbContext.ChangeTracker.Clear();
                    failed.Add(new FailedWord(entity.Word, ex.InnerException?.Message ?? ex.Message));
                }
            }

            foreach (var (id, word, source) in updates)
            {
                try
                {
                    var change = await ApplyUpdateAsync(id, source, options.Existing, tenses);

                    if (change is null)
                    {
                        skipped.Add(new SkippedWord(word, "Veritabanında zaten var; değişecek alan yok"));
                        continue;
                    }

                    await dbContext.SaveChangesAsync();
                    updated.Add($"{word} ({change})");
                }
                catch (DbUpdateException ex)
                {
                    dbContext.ChangeTracker.Clear();
                    failed.Add(new FailedWord(word, ex.InnerException?.Message ?? ex.Message));
                }
            }
        }

        return new ImportResult(added, updated, skipped, failed);
    }

    // Veritabanındaki kelimeyi dosyadakiyle günceller (kaydetmez). Değişen alanların adlarını, değişiklik yoksa null döndürür.
    // FillEmpty: yalnızca boş alanlar ve girilmemiş zamanlar. Overwrite: dosyada dolu olan her alan ve seçilen zamanlar.
    // Dosyada boş olan alan hiçbir zaman silinmez (eski export'larda fiil bilgisi ve çekim yoktur).
    private async Task<string?> ApplyUpdateAsync(int id, WordExport source, ExistingWordMode mode, HashSet<Tense> tenses)
    {
        var entity = await dbContext.Words
            .Include(x => x.Praesens)
            .Include(x => x.Praeteritum)
            .Include(x => x.Perfekt)
            .Include(x => x.FuturI)
            .Include(x => x.KonjunktivII)
            .FirstAsync(x => x.Id == id);

        var overwrite = mode == ExistingWordMode.Overwrite;
        var changes = new List<string>();

        string? Merge(string name, string? current, string? value)
        {
            value = string.IsNullOrWhiteSpace(value) ? null : value.Trim();

            if (value is null || value == current || (!overwrite && !string.IsNullOrWhiteSpace(current)))
                return current;

            changes.Add(name);
            return value;
        }

        entity.Plural = Merge("Çoğul", entity.Plural, source.Plural);
        entity.Meaning = Merge("Anlam", entity.Meaning, source.Meaning);
        entity.Sample = Merge("Örnek", entity.Sample, source.Sample);

        if (entity.WordType == WordType.Verb)
        {
            entity.PartizipII = Merge("Partizip II", entity.PartizipII, source.PartizipII);

            if (source.Auxiliary is { } aux && aux != entity.Auxiliary && (overwrite || entity.Auxiliary is null))
            {
                entity.Auxiliary = aux;
                changes.Add("Hilfsverb");
            }

            // Eski dosyalarda alan yok (false okunur): yalnızca işaretlenir, kaldırılmaz
            if (source.IsSeparable && !entity.IsSeparable)
            {
                entity.IsSeparable = true;
                changes.Add("trennbar");
            }

            var conjugationChanged = false;
            foreach (var (tense, c) in source.Conjugations ?? [])
            {
                if (!tenses.Contains(tense))
                    continue;

                var forms = FormsOf(c);
                if (forms.All(x => x is null))
                    continue;

                var target = ConjugationsOf(entity)[tense];

                if (target is { HasAnyForm: true } && (!overwrite || target.GetForms().SequenceEqual(forms)))
                    continue;

                target ??= CreateConjugation(entity, tense);
                target.SetForms(forms);
                changes.Add(tense.GetDescription());
                conjugationChanged = true;
            }

            // Yalnızca çekim değiştiyse kelime değişmiş sayılmaz; tarih elle işaretlenir (mobil aktarım için)
            if (conjugationChanged)
                dbContext.Entry(entity).Property(x => x.UpdatedAt).IsModified = true;
        }

        return changes.Count > 0 ? string.Join(", ", changes) : null;
    }

    // Export: seçilen türdeki kelimeler, fiil bilgileri, tarihler ve seçilen zamanların çekimleriyle
    public async Task<List<WordExport>> GetForExportAsync(ExportSelection selection)
    {
        var query = dbContext.Words.AsNoTracking();

        if (selection.SelectedType is { } type)
            query = query.Where(x => x.WordType == type);

        var words = await query
            .Include(x => x.Praesens)
            .Include(x => x.Praeteritum)
            .Include(x => x.Perfekt)
            .Include(x => x.FuturI)
            .Include(x => x.KonjunktivII)
            .OrderBy(x => x.Word)
            .ToListAsync();

        var tenses = selection.Tenses.ToHashSet();
        return words.Select(x => ToExport(x, tenses)).ToList();
    }

    private static WordExport ToExport(WordEntity w, HashSet<Tense> tenses)
    {
        var conjugations = ConjugationsOf(w)
            .Where(x => tenses.Contains(x.Key) && x.Value is { HasAnyForm: true })
            .ToDictionary(x => x.Key, x => new ConjugationExport
            {
                Ich = x.Value!.Ich,
                Du = x.Value.Du,
                ErSieEs = x.Value.ErSieEs,
                Wir = x.Value.Wir,
                Ihr = x.Value.Ihr,
                SieSie = x.Value.SieSie
            });

        return new WordExport
        {
            Word = w.Word,
            WordType = w.WordType,
            Plural = w.Plural,
            Meaning = w.Meaning,
            Sample = w.Sample,
            Auxiliary = w.Auxiliary,
            PartizipII = w.PartizipII,
            IsSeparable = w.IsSeparable,
            CreatedAt = w.CreatedAt,
            UpdatedAt = w.UpdatedAt,
            Conjugations = conjugations.Count > 0 ? conjugations : null
        };
    }

    private static Dictionary<Tense, ConjugationBase?> ConjugationsOf(WordEntity w) => new()
    {
        [Tense.Praesens] = w.Praesens,
        [Tense.Praeteritum] = w.Praeteritum,
        [Tense.Perfekt] = w.Perfekt,
        [Tense.FuturI] = w.FuturI,
        [Tense.KonjunktivII] = w.KonjunktivII
    };

    // Import: dosyadaki kelimeden yeni kayıt; fiil bilgileri ve çekimler yalnızca fiillerde alınır.
    // Dosyadaki tarihler korunur, yoksa AppDbContext kayıt anını yazar.
    private static WordEntity FromExport(WordExport w, string word, HashSet<Tense> tenses)
    {
        var isVerb = w.WordType == WordType.Verb;

        var entity = new WordEntity
        {
            Id = 0,
            Word = word,
            WordType = w.WordType,
            Plural = w.Plural?.Trim(),
            Meaning = w.Meaning?.Trim(),
            Sample = w.Sample?.Trim(),
            Auxiliary = isVerb ? w.Auxiliary : null,
            PartizipII = isVerb && !string.IsNullOrWhiteSpace(w.PartizipII) ? w.PartizipII.Trim() : null,
            IsSeparable = isVerb && w.IsSeparable,
            CreatedAt = w.CreatedAt?.ToUniversalTime() ?? default,
            UpdatedAt = w.UpdatedAt?.ToUniversalTime() ?? default
        };

        if (!isVerb || w.Conjugations is null)
            return entity;

        foreach (var (tense, c) in w.Conjugations)
        {
            // Import ekranında seçilmeyen zamanlar alınmaz
            if (!tenses.Contains(tense))
                continue;

            var forms = FormsOf(c);

            if (forms.All(x => x is null))
                continue;

            var target = CreateConjugation(entity, tense);

            target.SetForms(forms);
        }

        return entity;
    }

    // Dosyadaki çekim -> ConjugationBase.PersonLabels sırasıyla normalize edilmiş 6 form
    private static List<string?> FormsOf(ConjugationExport c)
        => new[] { c.Ich, c.Du, c.ErSieEs, c.Wir, c.Ihr, c.SieSie }
            .Select(ConjugationService.NormalizeForm)
            .ToList();

    // Kelimeye yeni (boş) çekim bağlar; kelimeyle birlikte kaydedilir
    private static ConjugationBase CreateConjugation(WordEntity entity, Tense tense) => tense switch
    {
        Tense.Praesens => entity.Praesens = new PraesensEntity(),
        Tense.Praeteritum => entity.Praeteritum = new PraeteritumEntity(),
        Tense.Perfekt => entity.Perfekt = new PerfektEntity(),
        Tense.FuturI => entity.FuturI = new FuturIEntity(),
        Tense.KonjunktivII => entity.KonjunktivII = new KonjunktivIIEntity(),
        _ => throw new ArgumentOutOfRangeException(nameof(tense))
    };

    public async Task<string> CreateBackupAsync()
    {
        // Önce WAL'daki veri ana dosyaya aktarılır ve bağlantılar kapatılır.
        // Meşgul olsa bile yedek alınır: BackupDatabase WAL'daki veriyi de kopyalar.
        await CheckpointAsync();

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

        // 2) Veritabanını kapat: WAL'daki veri ana dosyaya aktarılır, havuzdaki eski bağlantılar kapatılır
        await CheckpointAsync();

        // 3) Geri yüklemeden önce mevcut verinin güvenlik yedeğini al
        var backupFolder = Path.Combine(AppContext.BaseDirectory, "Backups");
        Directory.CreateDirectory(backupFolder);
        await BackupToAsync(Path.Combine(backupFolder, $"before-restore-{DateTime.Now:yyyyMMdd-HHmmss}.db"));

        // 4) Yüklenen dosyayı çalışan veritabanının üzerine yaz
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
            .GroupBy(x => new { Word = x.Word.ToLower(), x.WordType })
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

    public DbFilesViewModel GetDbFiles()
    {
        // Yol bağlantı açılmadan, bağlantı cümlesinden bulunur; açılsaydı -wal/-shm yeniden oluşurdu.
        // SQLite göreli yolu çalışma dizinine göre çözer, GetFullPath de aynısını yapar.
        var builder = new SqliteConnectionStringBuilder(dbContext.Database.GetConnectionString());
        var path = Path.GetFullPath(builder.DataSource);

        return new DbFilesViewModel
        {
            Database = FileState(path),
            Wal = FileState(path + "-wal"),
            Shm = FileState(path + "-shm")
        };
    }

    private static DbFileState FileState(string path)
        => new(path, File.Exists(path), FileSize(path));

    public async Task<CheckpointResult> CheckpointAsync()
    {
        var conn = (SqliteConnection)dbContext.Database.GetDbConnection();
        await dbContext.Database.OpenConnectionAsync();

        CheckpointResult result;

        try
        {
            // TRUNCATE: WAL'daki tüm sayfalar ana dosyaya yazılır ve -wal dosyası sıfırlanır
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";

            using var reader = await cmd.ExecuteReaderAsync();
            await reader.ReadAsync();

            result = new CheckpointResult(
                Busy: reader.GetInt32(0) != 0,
                WalPages: reader.GetInt32(1),
                CheckpointedPages: reader.GetInt32(2));
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }

        // Havuzda bekleyen bağlantılar kapatılır. Son bağlantı kapanınca SQLite -wal ve -shm dosyalarını siler.
        // Uygulama veritabanını bir sonraki istekte yeniden açar; dosyalar o zaman tekrar oluşur.
        SqliteConnection.ClearAllPools();

        return result;
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

        return SortForPrint(await query.ToListAsync());
    }

    // Listedeki kelimelerin (arama sonucu ya da tümü) sayfalama olmadan yazdırılması için
    public async Task<List<WordEntity>> GetForPrintAsync(WordSearch? search)
    {
        var query = dbContext.Words.AsNoTracking();

        if (search is { IsActive: true })
            query = ApplySearch(query, search);

        return SortForPrint(await query.ToListAsync());
    }

    // Sıralama: artikeli yok sayarak (der Tisch -> "Tisch"), Almanca alfabeye göre
    private static List<WordEntity> SortForPrint(List<WordEntity> list)
    {
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

    private sealed class WordKeyComparer : IEqualityComparer<(string Word, WordType Type)>
    {
        public static readonly WordKeyComparer Instance = new();

        public bool Equals((string Word, WordType Type) x, (string Word, WordType Type) y) =>
            x.Type == y.Type && StringComparer.OrdinalIgnoreCase.Equals(x.Word, y.Word);

        public int GetHashCode((string Word, WordType Type) obj) =>
            HashCode.Combine(obj.Type, StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Word));
    }
}
