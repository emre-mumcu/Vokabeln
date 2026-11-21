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
}
