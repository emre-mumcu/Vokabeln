using System.Globalization;
using System.Text.RegularExpressions;
using Vokabeln.AppData;
using Vokabeln.AppData.Entities;
using Vokabeln.AppData.Enums;
using Vokabeln.AppLib.Extensions;
using Vokabeln.AppLib.Filters;
using Vokabeln.ViewModels;

namespace Vokabeln.AppLib.Services;

// Çekim oyunu: seçilen zamanda fiilin çekimleri (ich ... sie/Sie) elle yazılır.
// Bir soru = bir fiil; puan, tüm kişileri doğru yazılan fiil sayısıdır.
public class ConjugationPlayService(AppDbContext dbContext, IGameStatsService gameStatsService, IConjugationService conjugationService)
    : GameServiceBase(dbContext, gameStatsService), IConjugationPlayService
{
    private const char AnswerSeparator = '|';

    private static readonly CultureInfo German = new("de-DE");

    // Cevabın başına yazılan zamir yok sayılır ("ich gehe" -> "gehe"); PersonLabels sırasıyla
    private static readonly string[][] Pronouns =
    [
        ["ich"], ["du"], ["er", "sie", "es"], ["wir"], ["ihr"], ["sie"]
    ];

    public async Task<List<TenseOption>> GetTenseOptionsAsync()
    {
        var result = new List<TenseOption>();

        foreach (var tense in Enum.GetValues<Tense>())
            result.Add(new TenseOption(tense, (await conjugationService.GetVerbIdsWithAsync(tense)).Count));

        return result;
    }

    public async Task<WordPlayState> StartAsync(int questionCount, Tense tense)
    {
        var ids = await conjugationService.GetVerbIdsWithAsync(tense);

        if (ids.Count == 0)
            throw new AppException($"{tense.GetDescription()} çekimi girilmiş fiil bulunamadı.");

        return await CreateGameAsync(new GameEntity { Type = GameType.Conjugation, WordType = WordType.Verb, Tense = tense }, ids, questionCount);
    }

    public async Task<ConjugationQuestionViewModel?> GetQuestionAsync(WordPlayState state)
    {
        var game = await GetGameAsync(state.GameId);
        var (word, total) = await GetCurrentWordAsync(state);

        if (word is null)
            return null;

        return new ConjugationQuestionViewModel
        {
            State = state,
            Total = total,
            Score = await ScoreAsync(game.Id),
            Word = word,
            Tense = TenseOf(game),
            Rows = await BuildRowsAsync(word.Id, TenseOf(game))
        };
    }

    public async Task<ConjugationQuestionViewModel?> AnswerAsync(WordPlayState state, IReadOnlyList<string?> forms)
    {
        var game = await GetGameAsync(state.GameId);
        var (word, total) = await GetCurrentWordAsync(state);

        if (word is null)
            return null;

        var tense = TenseOf(game);
        var rows = await BuildRowsAsync(word.Id, tense);

        // Kayıtta: 6 kişinin cevabı "|" ile birleştirilir (sorulmayan kişiler boş kalır)
        var given = Enumerable.Range(0, ConjugationBase.PersonLabels.Length)
            .Select(i => (i < forms.Count ? forms[i] : null)?.Replace(AnswerSeparator, ' ').Trim() ?? "")
            .ToArray();

        Check(rows, given);

        var answer = await SaveAnswerAsync(game, new GameAnswerEntity
        {
            WordId = word.Id,
            Answer = string.Join(AnswerSeparator, given),
            IsCorrect = rows.Count > 0 && rows.All(x => x.IsCorrect)
        });

        // Soru daha önce cevaplandıysa ilk cevap gösterilir
        if (answer?.Answer is { } saved && saved != string.Join(AnswerSeparator, given))
            Check(rows, saved.Split(AnswerSeparator));

        return new ConjugationQuestionViewModel
        {
            State = state,
            Total = total,
            Score = await ScoreAsync(game.Id),
            Word = word,
            Tense = tense,
            Rows = rows,
            Answered = true
        };
    }

    private static Tense TenseOf(GameEntity game) => game.Tense ?? Tense.Praesens;

    private async Task<List<ConjugationRow>> BuildRowsAsync(int wordId, Tense tense)
    {
        var conjugation = await conjugationService.FindAsync(wordId, tense)
            ?? throw new KeyNotFoundException("Fiilin bu zamandaki çekimi bulunamadı (oyun sırasında silinmiş olabilir).");

        var forms = conjugation.GetForms();

        return Enumerable.Range(0, forms.Length)
            .Where(i => !string.IsNullOrWhiteSpace(forms[i]))
            .Select(i => new ConjugationRow { Index = i, Label = ConjugationBase.PersonLabels[i], Expected = forms[i]! })
            .ToList();
    }

    private static void Check(List<ConjugationRow> rows, IReadOnlyList<string> given)
    {
        foreach (var row in rows)
        {
            row.Given = row.Index < given.Count ? given[row.Index] : "";
            row.IsCorrect = IsCorrect(row.Given, row.Expected, row.Index);
        }
    }

    // Esnek kontrol: büyük/küçük harf ve fazla boşluk önemsiz, baştaki zamir yok sayılır.
    // Beklenen birden fazla form varsa ("fragte / frug") herhangi biri ya da birkaçı yazılabilir; yazılanların hepsi doğru olmalı.
    public static bool IsCorrect(string? given, string expected, int personIndex)
    {
        var expectedForms = SplitForms(expected).Select(x => Normalize(x, personIndex)).ToHashSet();
        var givenForms = SplitForms(given).Select(x => Normalize(x, personIndex)).Where(x => x.Length > 0).ToList();

        return givenForms.Count > 0 && givenForms.All(expectedForms.Contains);
    }

    private static IEnumerable<string> SplitForms(string? value)
        => (value ?? "").Split(ConjugationBase.FormSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string Normalize(string value, int personIndex)
    {
        var text = Regex.Replace(value.Trim(), @"\s+", " ").ToLower(German);

        foreach (var pronoun in Pronouns[personIndex])
        {
            if (text.StartsWith(pronoun + " ", StringComparison.Ordinal))
                return text[(pronoun.Length + 1)..];
        }

        return text;
    }
}
