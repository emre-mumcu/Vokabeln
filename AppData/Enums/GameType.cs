using System.ComponentModel;

namespace Vokabeln.AppData.Enums;

public enum GameType
{
    [Description("Anlam oyunu")]
    Meaning,

    [Description("Artikel oyunu")]
    Article,

    [Description("Çekim oyunu")]
    Conjugation,

    // Veritabanında sayı olarak tutulur (3); yeni değer sona eklendiği için migration gerekmez
    [Description("Çoğul oyunu")]
    Plural
}
