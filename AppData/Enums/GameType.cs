using System.ComponentModel;

namespace Vokabeln.AppData.Enums;

public enum GameType
{
    [Description("Anlam oyunu")]
    Meaning,

    [Description("Artikel oyunu")]
    Article,

    [Description("Çekim oyunu")]
    Conjugation
}
