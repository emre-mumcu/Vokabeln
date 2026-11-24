using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Vokabeln.ViewModels;

namespace Vokabeln.AppLib.Services;

// PONS Dictionary API (https://api.pons.com/v1/dictionary) ile Almanca -> Türkçe karşılık arama.
// Secret "Pons:Secret" ayarından okunur; git'e girmemesi için user-secrets ya da ortam değişkeninde tutulur:
//   dotnet user-secrets set "Pons:Secret" "<secret>"     (geliştirme)
//   Pons__Secret=<secret>                                (publish edilen uygulama, ortam değişkeni)
// Ücretsiz kota ayda 1000 sorgudur: aynı kelimenin sonucu bellekte önbelleğe alınır.
public partial class PonsService(HttpClient httpClient, IConfiguration configuration, IMemoryCache cache, ILogger<PonsService> logger) : IPonsService
{
    private const string Dictionary = "detr"; // German » Turkish
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(12);

    private string? Secret => configuration["Pons:Secret"] is { Length: > 0 } secret ? secret.Trim() : null;

    public bool IsConfigured => Secret is not null;

    public async Task<PonsLookupResult> LookupAsync(string? word, CancellationToken cancellationToken = default)
    {
        var query = ArticleRegex().Replace(WhitespaceRegex().Replace(word?.Trim() ?? "", " "), "");

        if (query.Length == 0)
            return new() { Message = "Önce kelimeyi yazın." };

        if (Secret is not { } secret)
            return new() { Query = query, Message = "PONS secret ayarlı değil. Proje klasöründe: dotnet user-secrets set \"Pons:Secret\" \"<secret>\"" };

        // Başarılı sonuçlar (bulunamadı dahil) önbellekten; hatalar önbelleğe alınmaz
        var cacheKey = $"pons:{Dictionary}:{query.ToLowerInvariant()}";
        if (cache.TryGetValue(cacheKey, out PonsLookupResult? cached) && cached is not null)
            return cached;

        using var request = new HttpRequestMessage(HttpMethod.Get, $"dictionary?q={Uri.EscapeDataString(query)}&l={Dictionary}");
        request.Headers.Add("X-Secret", secret);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "PONS isteği başarısız: {Query}", query);
            return new() { Query = query, Message = "PONS'a ulaşılamadı (internet bağlantısını kontrol edin)." };
        }

        using (response)
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    break;

                case HttpStatusCode.NoContent:
                    return Remember(cacheKey, new PonsLookupResult { Success = true, Query = query, Message = $"\"{query}\" PONS'ta bulunamadı." });

                case HttpStatusCode.Forbidden:
                case HttpStatusCode.Unauthorized:
                    logger.LogWarning("PONS secret reddedildi ({Status})", (int)response.StatusCode);
                    return new() { Query = query, Message = "PONS secret geçersiz ya da yetkisiz." };

                case HttpStatusCode.ServiceUnavailable:
                case HttpStatusCode.TooManyRequests:
                    logger.LogWarning("PONS kotası doldu ({Status})", (int)response.StatusCode);
                    return new() { Query = query, Message = "PONS aylık sorgu kotası dolmuş olabilir (ücretsiz: 1000 sorgu/ay)." };

                default:
                    logger.LogWarning("PONS beklenmeyen yanıt: {Status} ({Query})", (int)response.StatusCode, query);
                    return new() { Query = query, Message = $"PONS beklenmeyen bir yanıt verdi (HTTP {(int)response.StatusCode})." };
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            // Aranan kelimeyle tam eşleşen maddeler önce (PONS "schön" için "schon", "schonen" de döndürür)
            var entries = Parse(doc.RootElement)
                .OrderBy(e => string.Equals(e.Headword, query, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ToList();
            var result = new PonsLookupResult
            {
                Success = true,
                Query = query,
                Entries = entries,
                Message = entries.Count == 0 ? $"\"{query}\" için Türkçe karşılık bulunamadı." : null
            };

            return Remember(cacheKey, result);
        }
    }

    private PonsLookupResult Remember(string key, PonsLookupResult result)
    {
        cache.Set(key, result, CacheDuration);
        return result;
    }

    // Yanıt: [{ lang, hits: [{ type: "entry", roms: [{ headword, headword_full, wordclass, arabs: [{ header, translations: [{ source, target }] }] }] },
    //                        { type: "translation", source, target }] }]
    // Yalnızca Almanca taraf (lang = "de") alınır; HTML etiketleri temizlenir.
    private static List<PonsEntry> Parse(JsonElement root)
    {
        var entries = new List<PonsEntry>();
        if (root.ValueKind != JsonValueKind.Array) return entries;

        foreach (var language in root.EnumerateArray())
        {
            if (Str(language, "lang") is { } lang && lang != "de") continue;
            if (!language.TryGetProperty("hits", out var hits)) continue;

            foreach (var hit in hits.EnumerateArray())
            {
                if (Str(hit, "type") == "translation")
                {
                    // Tam metin eşleşmesi: tek çeviri
                    var translation = ToTranslation(hit);
                    if (translation is not null)
                        entries.Add(new PonsEntry { Headword = translation.Source, Senses = [new PonsSense { Translations = [translation] }] });
                    continue;
                }

                if (!hit.TryGetProperty("roms", out var roms)) continue;

                foreach (var rom in roms.EnumerateArray())
                {
                    var entry = new PonsEntry
                    {
                        Headword = Clean(Str(rom, "headword")) ?? "",
                        HeadwordFull = Clean(Str(rom, "headword_full")),
                        WordClass = Clean(Str(rom, "wordclass"))
                    };

                    if (rom.TryGetProperty("arabs", out var arabs))
                    {
                        foreach (var arab in arabs.EnumerateArray())
                        {
                            var sense = new PonsSense { Header = Clean(Str(arab, "header")) };

                            if (arab.TryGetProperty("translations", out var translations))
                                sense.Translations = translations.EnumerateArray().Select(ToTranslation).OfType<PonsTranslation>().ToList();

                            if (sense.Translations.Count > 0)
                                entry.Senses.Add(sense);
                        }
                    }

                    if (entry.Senses.Count > 0)
                        entries.Add(entry);
                }
            }
        }

        return entries;
    }

    private static PonsTranslation? ToTranslation(JsonElement e)
    {
        var target = Clean(Str(e, "target"));
        return string.IsNullOrWhiteSpace(target) ? null : new PonsTranslation(Clean(Str(e, "source")) ?? "", target);
    }

    // "<strong class=\"headword\">Tisch</strong> <span ...>m</span>" -> "Tisch m"
    private static string? Clean(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;

        var text = WebUtility.HtmlDecode(TagRegex().Replace(html, " "));
        text = WhitespaceRegex().Replace(text, " ").Trim();
        return text.Length > 0 ? text : null;
    }

    private static string? Str(JsonElement e, string name)
        => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    [GeneratedRegex(@"^(der|die|das)\s+", RegexOptions.IgnoreCase)]
    private static partial Regex ArticleRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagRegex();
}
