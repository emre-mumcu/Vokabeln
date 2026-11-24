using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Vokabeln.AppData.Enums;
using Vokabeln.AppLib.Extensions;
using Vokabeln.ViewModels;

namespace Vokabeln.AppLib.Services;

// Proje kökündeki Wiktionary kaynak dosyasından (wiktionary-de.json) kelime bilgisi okur.
// Dosya ~280 MB olduğu için belleğe alınmaz: ilk aramada satırların dosyadaki yerleri (offset) indekslenir,
// aramada yalnızca ilgili satırlar okunur. Dosya değişirse (elle düzeltme) indeks yeniden kurulur.
// Dosya biçimi: her kelime ({"word":...}) ve her çekimli form ({"form":...}) ayrı bir satırdadır.
public class WiktionaryService(IConfiguration configuration, IWebHostEnvironment environment, ILogger<WiktionaryService> logger) : IWiktionaryService
{
    private const int MaxResults = 20;
    private const int PrefixLength = 1024; // indeks için satırın yalnızca başı okunur ("word"/"lemma" ya da "form")

    private static readonly Regex ArticleRegex = new(@"^(der|die|das)\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly string[] PersonKeys = ["ich", "du", "erSieEs", "wir", "ihr", "sieSie"];

    private readonly SemaphoreSlim buildLock = new(1, 1);
    private WiktionaryIndex? index;

    private string FilePath
    {
        get
        {
            var path = configuration["Wiktionary:Path"] ?? "wiktionary-de.json";
            return Path.IsPathRooted(path) ? path : Path.Combine(environment.ContentRootPath, path);
        }
    }

    public async Task<WiktionaryLookupResult> LookupAsync(string? query, WordType? preferredType)
    {
        var path = FilePath;
        if (!File.Exists(path))
            return new() { Message = $"Wiktionary kaynak dosyası bulunamadı: {path}" };

        var term = Normalize(query);
        if (term.Length == 0)
            return new() { Available = true, Message = "Aranacak kelimeyi yazın." };

        var current = await GetIndexAsync(path);

        // 1) Sözlük maddesi (lemma), 2) çekimli form / eski yazım -> maddesi
        var hits = new List<(long Offset, string? Via, string? ViaType)>();
        var seen = new HashSet<long>();

        foreach (var offset in current.Find(current.Words, term))
            if (seen.Add(offset)) hits.Add((offset, null, null));

        foreach (var formOffset in current.Find(current.Forms, term))
        {
            using var form = await ReadLineAsync(path, formOffset);
            if (form is null) continue;

            var root = form.RootElement;
            var lemma = Str(root, "lemma");
            if (lemma is null) continue;

            var tags = root.TryGetProperty("tags", out var t) ? string.Join("; ", t.EnumerateArray().Select(x => x.GetString())) : null;
            var via = string.IsNullOrEmpty(tags) ? Str(root, "form") : $"{Str(root, "form")}: {tags}";

            foreach (var offset in current.Find(current.Words, lemma))
                if (seen.Add(offset)) hits.Add((offset, via, Str(root, "wordType")));
        }

        var entries = new List<WiktionaryEntry>();
        foreach (var (offset, via, viaType) in hits.Take(MaxResults * 2))
        {
            using var doc = await ReadLineAsync(path, offset);
            if (doc is null) continue;

            var entry = ToEntry(doc.RootElement);

            // Formun ait olduğu türle eşleşmeyen maddeler alınmaz (ör. "ging" yalnızca fiil gehen'e gider)
            if (viaType is not null && entry.WordType?.ToString() != viaType) continue;

            entry.Via = via;
            entries.Add(entry);
        }

        // Artikelle arandıysa ("der Tisch") yalnızca isimler; tür seçiliyse ve o türde sonuç varsa yalnızca o tür
        var onlyType = HasArticle(query) ? WordType.Noun : preferredType;
        if (onlyType is not null && entries.Any(e => e.WordType == onlyType))
            entries = entries.Where(e => e.WordType == onlyType).ToList();

        // Doğrudan eşleşmeler (çekimli formdan bulunanlardan) önce
        entries = entries
            .OrderBy(e => e.Via is null ? 0 : 1)
            .ThenBy(e => e.Lemma == term ? 0 : 1)
            .Take(MaxResults)
            .ToList();

        return new()
        {
            Available = true,
            Entries = entries,
            Message = entries.Count == 0 ? $"\"{term}\" Wiktionary kaynağında bulunamadı." : null
        };
    }

    private static bool HasArticle(string? query) => ArticleRegex.IsMatch(query?.Trim() ?? "");

    // "  der   Tisch " -> "Tisch"
    private static string Normalize(string? query)
    {
        var text = Regex.Replace(query?.Trim() ?? "", @"\s+", " ");
        return ArticleRegex.Replace(text, "");
    }

    // ---------- indeks

    private async Task<WiktionaryIndex> GetIndexAsync(string path)
    {
        var file = new FileInfo(path);

        if (index is { } ready && ready.IsCurrent(file))
            return ready;

        await buildLock.WaitAsync();
        try
        {
            file.Refresh();
            if (index is { } built && built.IsCurrent(file))
                return built;

            var started = DateTime.UtcNow;
            index = await Task.Run(() => WiktionaryIndex.Build(file));
            logger.LogInformation("Wiktionary indeksi kuruldu: {Words} kelime, {Forms} form, {Seconds:N1} sn",
                index.Words.Count, index.Forms.Count, (DateTime.UtcNow - started).TotalSeconds);
            return index;
        }
        finally
        {
            buildLock.Release();
        }
    }

    private sealed class WiktionaryIndex
    {
        public required DateTime LastWriteUtc { get; init; }
        public required long Length { get; init; }

        // Büyük/küçük harf duyarsız; tam harf eşleşmesi Find'da öne alınır
        public Dictionary<string, List<(string Key, long Offset)>> Words { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, List<(string Key, long Offset)>> Forms { get; } = new(StringComparer.OrdinalIgnoreCase);

        public bool IsCurrent(FileInfo file) => file.Exists && file.LastWriteTimeUtc == LastWriteUtc && file.Length == Length;

        public IEnumerable<long> Find(Dictionary<string, List<(string Key, long Offset)>> map, string key)
            => map.TryGetValue(key, out var list)
                ? list.OrderBy(x => x.Key == key ? 0 : 1).Select(x => x.Offset)
                : [];

        public static WiktionaryIndex Build(FileInfo file)
        {
            var result = new WiktionaryIndex { LastWriteUtc = file.LastWriteTimeUtc, Length = file.Length };

            using var fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 20, FileOptions.SequentialScan);

            var buffer = new byte[1 << 20];
            var prefix = new byte[PrefixLength];
            int prefixLength = 0, read;
            long bufferStart = 0, lineStart = 0;

            while ((read = fs.Read(buffer, 0, buffer.Length)) > 0)
            {
                var i = 0;
                while (i < read)
                {
                    var newLine = Array.IndexOf(buffer, (byte)'\n', i, read - i);
                    var end = newLine < 0 ? read : newLine;

                    var copy = Math.Min(end - i, prefix.Length - prefixLength);
                    if (copy > 0)
                    {
                        Buffer.BlockCopy(buffer, i, prefix, prefixLength, copy);
                        prefixLength += copy;
                    }

                    if (newLine < 0) break;

                    result.AddLine(prefix.AsSpan(0, prefixLength), lineStart);
                    prefixLength = 0;
                    lineStart = bufferStart + newLine + 1;
                    i = newLine + 1;
                }
                bufferStart += read;
            }

            if (prefixLength > 0)
                result.AddLine(prefix.AsSpan(0, prefixLength), lineStart);

            return result;
        }

        // Kelime satırında anahtar "lemma", form satırında "form" alanıdır
        private void AddLine(ReadOnlySpan<byte> line, long offset)
        {
            line = line.TrimStart(" \t﻿"u8);

            Dictionary<string, List<(string, long)>> map;
            string keyName;
            if (line.StartsWith("{\"word\""u8)) { map = Words; keyName = "lemma"; }
            else if (line.StartsWith("{\"form\""u8)) { map = Forms; keyName = "form"; }
            else return;

            var reader = new Utf8JsonReader(line, isFinalBlock: false, state: default);
            try
            {
                if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject) return;

                while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
                {
                    var isKey = reader.ValueTextEquals(keyName);
                    if (!reader.Read()) return;

                    if (isKey)
                    {
                        if (reader.TokenType != JsonTokenType.String) return;
                        var key = reader.GetString();
                        if (string.IsNullOrWhiteSpace(key)) return;

                        if (!map.TryGetValue(key, out var list)) map[key] = list = [];
                        list.Add((key, offset));
                        return;
                    }

                    if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray && !reader.TrySkip()) return;
                }
            }
            catch (JsonException)
            {
                // Bozuk satır (ör. elle düzeltmede hata): atlanır
            }
        }
    }

    // ---------- satır okuma

    // Satırın sonundaki virgül (dizi elemanı ayırıcısı) atılıp JSON olarak çözülür
    private static async Task<JsonDocument?> ReadLineAsync(string path, long offset)
    {
        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 16, FileOptions.Asynchronous);
        fs.Seek(offset, SeekOrigin.Begin);

        using var line = new MemoryStream();
        var buffer = new byte[1 << 16];
        int read;
        while ((read = await fs.ReadAsync(buffer)) > 0)
        {
            var newLine = Array.IndexOf(buffer, (byte)'\n', 0, read);
            line.Write(buffer, 0, newLine < 0 ? read : newLine);
            if (newLine >= 0) break;
        }

        var text = Encoding.UTF8.GetString(line.GetBuffer(), 0, (int)line.Length).Trim().TrimEnd(',').Trim();
        try
        {
            return JsonDocument.Parse(text);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // ---------- kaynak satırı -> uygulama alanları

    private static WiktionaryEntry ToEntry(JsonElement e)
    {
        var wordType = Enum.TryParse<WordType>(Str(e, "wordType"), out var type) ? type : (WordType?)null;

        var entry = new WiktionaryEntry
        {
            Word = Str(e, "word") ?? "",
            Lemma = Str(e, "lemma") ?? "",
            WordType = wordType,
            WordTypeText = wordType?.GetDescription() ?? Str(e, "pos"),
            Meaning = Str(e, "meaning"),
            Sample = Str(e, "sample"),
            PartizipII = Str(e, "partizipII"),
            IsSeparable = e.TryGetProperty("isSeparable", out var sep) && sep.ValueKind == JsonValueKind.True,
            Auxiliary = Enum.TryParse<AuxiliaryVerb>(Str(e, "auxiliary"), out var aux) ? aux : null,
            Plural = PluralWithArticle(Str(e, "plural"))
        };

        if (e.TryGetProperty("conjugations", out var conj) && conj.ValueKind == JsonValueKind.Object)
        {
            foreach (var tense in conj.EnumerateObject())
            {
                if (!Enum.TryParse<Tense>(tense.Name, out _)) continue;
                entry.Conjugations[tense.Name] = PersonKeys.Select(k => Str(tense.Value, k)).ToList();
            }
        }

        if (e.TryGetProperty("senses", out var senses))
            entry.Glosses = senses.EnumerateArray()
                .Select(s => (Index: Str(s, "index"), Gloss: Str(s, "gloss")))
                .Where(s => !string.IsNullOrWhiteSpace(s.Gloss))
                .Select(s => s.Index is null ? s.Gloss! : $"[{s.Index}] {s.Gloss}")
                .Take(5)
                .ToList();

        if (e.TryGetProperty("translations", out var tr) && tr.TryGetProperty("en", out var en))
            entry.English = en.EnumerateArray()
                .Select(x => Str(x, "word"))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!)
                .Distinct()
                .Take(8)
                .ToList();

        return entry;
    }

    // Uygulamada çoğul artikelle yazılır: "Tische / Tisch" -> "die Tische / die Tisch"
    private static string? PluralWithArticle(string? plural)
    {
        if (string.IsNullOrWhiteSpace(plural)) return null;

        var parts = plural.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(p => ArticleRegex.IsMatch(p) ? p : $"die {p}");
        return string.Join(" / ", parts);
    }

    private static string? Str(JsonElement e, string name)
        => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
