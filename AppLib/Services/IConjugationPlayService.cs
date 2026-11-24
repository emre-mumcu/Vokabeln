using Vokabeln.AppData.Enums;
using Vokabeln.ViewModels;

namespace Vokabeln.AppLib.Services;

public interface IConjugationPlayService
{
    Task<List<TenseOption>> GetTenseOptionsAsync();
    Task<WordPlayState> StartAsync(int questionCount, Tense tense);
    Task<ConjugationQuestionViewModel?> GetQuestionAsync(WordPlayState state);
    Task<ConjugationQuestionViewModel?> AnswerAsync(WordPlayState state, IReadOnlyList<string?> forms);
    Task<WordPlayResultViewModel> FinishAsync(WordPlayState state);
}
