using Vokabeln.AppData.Entities;
using Vokabeln.AppData.Enums;
using Vokabeln.ViewModels;

namespace Vokabeln.AppLib.Services;

public interface IConjugationService
{
    Task<Dictionary<Tense, ConjugationBase>> GetForWordAsync(int wordId);
    Task<ConjugationBase?> FindAsync(int wordId, Tense tense);
    Task<ConjugationFormViewModel?> GetFormAsync(int wordId, Tense tense);
    Task SaveAsync(ConjugationFormViewModel form);
    Task DeleteAsync(int wordId, Tense tense);
    Task<List<int>> GetVerbIdsWithAsync(Tense tense);
}
