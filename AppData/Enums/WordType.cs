using System.ComponentModel;

namespace Vokabeln.AppData.Enums;

public enum WordType
{
    // [Description("-- Please Select --")] None,

    [Description("das Nomen")]
    Noun,

    [Description("das Verb")]
    Verb,

    [Description("das Adjektiv")]
    Adjective,

    [Description("das Adverb")]
    Adverb,

    [Description("die Präposition")]
    Preposition,

    [Description("die Konjunktion")]
    Conjunction,

    [Description("das Pronomen")]
    Pronoun,

    [Description("der Artikel")]
    Article,

    [Description("die Interjektion")]
    Interjection,

    [Description("die Partikel")]
    Particle,

    [Description("das Numerale")]
    Numeral,

    [Description("der Ausdruck")]
    Expression
}
