using Vokabeln.AppData.Enums;
using Vokabeln.ViewModels;

namespace Vokabeln.AppLib.Services;

public interface IWiktionaryService
{
    // Kelimeyi (artikelli/artikelsiz) ya da çekimli halini ("ging") arar; tür seçiliyse ve o türde sonuç varsa yalnızca o tür döner
    Task<WiktionaryLookupResult> LookupAsync(string? query, WordType? preferredType);
}
