using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vokabeln.AppLib.Services;
using Vokabeln.ViewModels;

namespace Vokabeln.Controllers
{
	// Çekim oyunu: seçilen zamanda fiilin çekimleri elle yazılır
	[Authorize]
	public class ConjugationPlayController(IConjugationPlayService conjugationPlayService) : Controller
	{
		[HttpGet]
		public async Task<IActionResult> Index()
		{
			var model = new ConjugationPlaySetupViewModel
			{
				Tenses = await conjugationPlayService.GetTenseOptionsAsync()
			};

			// Varsayılan zaman: çekimi girilmiş fiili olan ilk zaman
			model.Tense = model.Tenses.FirstOrDefault(x => x.VerbCount > 0)?.Tense ?? model.Tense;

			return View(model);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Start(ConjugationPlaySetupViewModel setup)
		{
			var state = await conjugationPlayService.StartAsync(setup.QuestionCount, setup.Tense);
			return await ShowQuestionAsync(state);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Answer(WordPlayState state, List<string?> forms)
		{
			var model = await conjugationPlayService.AnswerAsync(state, forms);

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
			var model = await conjugationPlayService.GetQuestionAsync(state);

			// Sorular bittiyse sonuç gösterilir
			if (model is null)
				return await FinishAsync(state);

			return View("Question", model);
		}

		private async Task<IActionResult> FinishAsync(WordPlayState state)
			=> View("GameResult", await conjugationPlayService.FinishAsync(state));
	}
}
