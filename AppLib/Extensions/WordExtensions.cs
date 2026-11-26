using Vokabeln.AppData.Entities;
using Vokabeln.AppData.Enums;

namespace Vokabeln.AppLib.Extensions;

public static class WordExtensions
{
    // Fiilin bir zamandaki çekimi (navigation property'ler Include ile yüklenmiş olmalı)
    public static ConjugationBase? ConjugationOf(this WordEntity word, Tense tense) => tense switch
    {
        Tense.Praesens => word.Praesens,
        Tense.Praeteritum => word.Praeteritum,
        Tense.Perfekt => word.Perfekt,
        Tense.FuturI => word.FuturI,
        Tense.KonjunktivII => word.KonjunktivII,
        _ => null
    };

    // Artikele göre Bootstrap metin rengi: der mavi, die kırmızı, das yeşil (yazdırma tablosuyla aynı renk mantığı)
    public static string ArticleColorClass(this string word)
    {
        var w = word.TrimStart();

        if (w.StartsWith("der ", StringComparison.OrdinalIgnoreCase)) return "fg-primary";
        if (w.StartsWith("die ", StringComparison.OrdinalIgnoreCase)) return "fg-danger";
        if (w.StartsWith("das ", StringComparison.OrdinalIgnoreCase)) return "fg-success";
        return "";
    }
}
