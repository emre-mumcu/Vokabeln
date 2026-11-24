using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vokabeln.AppLib.Services;
using Vokabeln.ViewModels;

namespace Vokabeln.Controllers
{
	[Authorize]
	public class WordPlayController(IWordPlayService wordPlayService) : Controller
	{
		[HttpGet]
		public IActionResult Index() => View(new WordPlaySetupViewModel());

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Start(WordPlaySetupViewModel setup)
		{
			var state = await wordPlayService.StartAsync(setup.QuestionCount, setup.SelectedType, setup.Article);
			return await ShowQuestionAsync(state);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Answer(WordPlayState state, string? optionIds, int? selectedId)
		{
			// Şık seçilmeden gönderildiyse aynı soru tekrar gösterilir
			if (selectedId is null)
				return await ShowQuestionAsync(state);

			var model = await wordPlayService.AnswerAsync(state, optionIds, selectedId.Value);

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
			var model = await wordPlayService.GetQuestionAsync(state);

			// Sorular bittiyse sonuç gösterilir
			if (model is null)
				return await FinishAsync(state);

			return View("Question", model);
		}

		private async Task<IActionResult> FinishAsync(WordPlayState state)
			=> View("GameResult", await wordPlayService.FinishAsync(state));
	}
}
