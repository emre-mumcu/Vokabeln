using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vokabeln.AppLib.Services;

namespace Vokabeln.Controllers
{
	[Authorize(Roles = "Admin")]
	public class AdminController(IGameStatsService gameStatsService) : Controller
	{
		[HttpGet]
		public async Task<IActionResult> Index()
		{
			var stats = await gameStatsService.GetStatsAsync();
			return View(stats);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> ResetGameStats()
		{
			var count = await gameStatsService.ResetAsync();

			TempData["SuccessMessage"] = $"Oyun istatistikleri sıfırlandı ({count} oyun silindi).";
			return RedirectToAction(nameof(Index));
		}
	}
}
