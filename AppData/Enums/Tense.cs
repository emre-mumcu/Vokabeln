using System.ComponentModel;

namespace Vokabeln.AppData.Enums;

// Her zamanın çekimleri kendi tablosunda tutulur (Praesens, Praeteritum, Perfekt, FuturI, KonjunktivII)
public enum Tense
{
    [Description("Präsens")]
    Praesens,

    [Description("Präteritum")]
    Praeteritum,

    [Description("Perfekt")]
    Perfekt,

    [Description("Futur I")]
    FuturI,

    [Description("Konjunktiv II")]
    KonjunktivII
}
