using Vokabeln.ViewModels;

namespace Vokabeln.AppLib.Services;

public interface IArticlePlayService
{
    Task<WordPlayState> StartAsync(int questionCount);
    Task<ArticleQuestionViewModel?> GetQuestionAsync(WordPlayState state);
    Task<ArticleQuestionViewModel?> AnswerAsync(WordPlayState state, string article);
    Task<WordPlayResultViewModel> FinishAsync(WordPlayState state);
}
