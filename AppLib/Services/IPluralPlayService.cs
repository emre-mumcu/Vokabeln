using Vokabeln.AppData.Enums;
using Vokabeln.ViewModels;

namespace Vokabeln.AppLib.Services;

public interface IPluralPlayService
{
    Task<int> CountPlayableAsync();
    Task<WordPlayState> StartAsync(int questionCount, ArticleFilter article);
    Task<PluralQuestionViewModel?> GetQuestionAsync(WordPlayState state);
    Task<PluralQuestionViewModel?> AnswerAsync(WordPlayState state, string given);
    Task<WordPlayResultViewModel> FinishAsync(WordPlayState state);
}
