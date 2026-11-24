using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vokabeln.AppLib.Services;
using Vokabeln.ViewModels;

namespace Vokabeln.Controllers
{
	// Artikel oyunu: ismin artikeli der/die/das butonlarından seçilir
	[Authorize]
	public class ArticlePlayController(IArticlePlayService articlePlayService) : Controller
	{
		[HttpGet]
		public IActionResult Index() => View(new ArticlePlaySetupViewModel());

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Start(ArticlePlaySetupViewModel setup)
		{
			var state = await articlePlayService.StartAsync(setup.QuestionCount);
			return await ShowQuestionAsync(state);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Answer(WordPlayState state, string? article)
		{
			// Artikel seçilmeden gönderildiyse aynı soru tekrar gösterilir
			if (string.IsNullOrWhiteSpace(article))
				return await ShowQuestionAsync(state);

			var model = await articlePlayService.AnswerAsync(state, article);

			if (model is null)
				return await FinishAsync(state);

			return View("Question", model);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public Task<IActionResult> Next(WordPlayState state) => ShowQuestionAsync(state);

		// Oyunu yarıda bitirmek için de kullanılır
		[HttpPost]
		[ValidateAntiForgeryToken]
		public Task<IActionResult> Finish(WordPlayState state) => FinishAsync(state);

		private async Task<IActionResult> ShowQuestionAsync(WordPlayState state)
		{
			var model = await articlePlayService.GetQuestionAsync(state);

			// Sorular bittiyse sonuç gösterilir
			if (model is null)
				return await FinishAsync(state);

			return View("Question", model);
		}

		private async Task<IActionResult> FinishAsync(WordPlayState state)
			=> View("GameResult", await articlePlayService.FinishAsync(state));
	}
}
