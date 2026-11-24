using System;
using Vokabeln.AppData.Enums;

namespace Vokabeln.AppData.Entities;

public class WordEntity
{
    public int Id { get; set; }

    public string Word { get; set; } = string.Empty;

    public WordType WordType { get; set; }

    public string? Plural { get; set; }
    public string? Meaning { get; set; }

    public string? Sample { get; set; }

    // Yalnızca fiiller için
    public AuxiliaryVerb? Auxiliary { get; set; }
    public string? PartizipII { get; set; }
    public bool IsSeparable { get; set; }

    // UTC; AppDbContext kayıt sırasında otomatik doldurur (mobil uygulamaya değişenleri aktarmak için)
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Fiil çekimleri: her zaman kendi tablosunda, kelimeyle bire bir ilişkili
    public PraesensEntity? Praesens { get; set; }
    public PraeteritumEntity? Praeteritum { get; set; }
    public PerfektEntity? Perfekt { get; set; }
    public FuturIEntity? FuturI { get; set; }
    public KonjunktivIIEntity? KonjunktivII { get; set; }
}
