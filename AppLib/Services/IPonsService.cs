using Vokabeln.ViewModels;

namespace Vokabeln.AppLib.Services;

public interface IPonsService
{
    // Secret ayarlı mı (ekranda butonun açıklaması için)
    bool IsConfigured { get; }

    // Almanca kelimenin Türkçe karşılıklarını PONS sözlüğünden (detr) getirir; artikel ("der Tisch") yok sayılır
    Task<PonsLookupResult> LookupAsync(string? word, CancellationToken cancellationToken = default);
}
