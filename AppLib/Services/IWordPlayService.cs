using Vokabeln.AppData.Enums;
using Vokabeln.ViewModels;

namespace Vokabeln.AppLib.Services;

public interface IWordPlayService
{
    Task<WordPlayState> StartAsync(int questionCount, WordType? type, ArticleFilter article);
    Task<WordPlayQuestionViewModel?> GetQuestionAsync(WordPlayState state);
    Task<WordPlayQuestionViewModel?> AnswerAsync(WordPlayState state, string? optionIds, int selectedId);
    Task<WordPlayResultViewModel> FinishAsync(WordPlayState state);
}
