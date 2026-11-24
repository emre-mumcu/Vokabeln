namespace Vokabeln.AppData.Entities;

// Bir zamanın 6 kişi için çekimi. Ortak alanlar burada; her zaman ayrı bir tabloya yazılır.
// Bu sınıf EF modelinde yer almaz (DbSet'i yok), bu yüzden kalıtım tablosu oluşmaz.
// Bir kişi için birden fazla doğru form varsa "/" ile ayrılır: "fragte / frug"
public abstract class ConjugationBase
{
    public const string FormSeparator = "/";

    // Ekranlarda alt alta gösterim sırası
    public static readonly string[] PersonLabels = ["ich", "du", "er/sie/es", "wir", "ihr", "sie/Sie"];

    public int Id { get; set; }

    public int WordId { get; set; }
    public WordEntity? Word { get; set; }

    public string? Ich { get; set; }
    public string? Du { get; set; }
    public string? ErSieEs { get; set; }
    public string? Wir { get; set; }
    public string? Ihr { get; set; }
    public string? SieSie { get; set; }

    // PersonLabels sırasıyla
    public string?[] GetForms() => [Ich, Du, ErSieEs, Wir, Ihr, SieSie];

    public void SetForms(IReadOnlyList<string?> forms)
    {
        string? At(int i) => i < forms.Count ? forms[i] : null;

        Ich = At(0);
        Du = At(1);
        ErSieEs = At(2);
        Wir = At(3);
        Ihr = At(4);
        SieSie = At(5);
    }

    public bool HasAnyForm => GetForms().Any(x => !string.IsNullOrWhiteSpace(x));
}

public class PraesensEntity : ConjugationBase { }

public class PraeteritumEntity : ConjugationBase { }

public class PerfektEntity : ConjugationBase { }

public class FuturIEntity : ConjugationBase { }

public class KonjunktivIIEntity : ConjugationBase { }
