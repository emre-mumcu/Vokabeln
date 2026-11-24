using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vokabeln.AppData.Enums;
using Vokabeln.AppLib.Extensions;
using Vokabeln.AppLib.Services;
using Vokabeln.ViewModels;

namespace Vokabeln.Controllers
{
	// Fiil çekimleri: kelimenin detay sayfasından eklenir/düzenlenir, kayıttan sonra oraya dönülür
	[Authorize]
	public class ConjugationsController(IConjugationService conjugationService) : Controller
	{
		[HttpGet]
		public async Task<IActionResult> Edit(int wordId, Tense tense, string? returnUrl)
		{
			var model = await conjugationService.GetFormAsync(wordId, tense);

			if (model is null)
				return NotFound();

			ViewData["ReturnUrl"] = returnUrl;
			return View(model);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Edit(ConjugationFormViewModel form, string? returnUrl)
		{
			await conjugationService.SaveAsync(form);

			TempData["SuccessMessage"] = $"{form.Tense.GetDescription()} çekimi kaydedildi.";
			return RedirectToLocal(returnUrl, form.WordId);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Delete(int wordId, Tense tense, string? returnUrl)
		{
			await conjugationService.DeleteAsync(wordId, tense);

			TempData["SuccessMessage"] = $"{tense.GetDescription()} çekimi silindi.";
			return RedirectToLocal(returnUrl, wordId);
		}

		// Yalnızca site içi adrese dönülür (open redirect'i önler), yoksa kelimenin detay sayfası
		private IActionResult RedirectToLocal(string? returnUrl, int wordId)
			=> !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
				? LocalRedirect(returnUrl)
				: RedirectToAction("Details", "Words", new { id = wordId });
	}
}
