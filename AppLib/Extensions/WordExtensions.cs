namespace Vokabeln.AppLib.Extensions;

public static class WordExtensions
{
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
