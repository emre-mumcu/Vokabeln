using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vokabeln.AppLib.Services;
using Vokabeln.ViewModels;

namespace Vokabeln.Controllers
{
	// Çoğul oyunu: tekil ismin çoğulu yazılır
	[Authorize]
	public class PluralPlayController(IPluralPlayService pluralPlayService) : Controller
	{
		[HttpGet]
		public async Task<IActionResult> Index()
		{
			ViewData["PlayableCount"] = await pluralPlayService.CountPlayableAsync();
			return View(new PluralPlaySetupViewModel());
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Start(PluralPlaySetupViewModel setup)
		{
			var state = await pluralPlayService.StartAsync(setup.QuestionCount, setup.Article);
			return await ShowQuestionAsync(state);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Answer(WordPlayState state, string? plural)
		{
			// Boş gönderildiyse aynı soru tekrar gösterilir
			if (string.IsNullOrWhiteSpace(plural))
				return await ShowQuestionAsync(state);

			var model = await pluralPlayService.AnswerAsync(state, plural);

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
			var model = await pluralPlayService.GetQuestionAsync(state);

			// Sorular bittiyse sonuç gösterilir
			if (model is null)
				return await FinishAsync(state);

			return View("Question", model);
		}

		private async Task<IActionResult> FinishAsync(WordPlayState state)
			=> View("GameResult", await pluralPlayService.FinishAsync(state));
	}
}
