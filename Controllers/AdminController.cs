using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vokabeln.AppData.Enums;
using Vokabeln.AppLib.Services;
using Vokabeln.ViewModels;

namespace Vokabeln.Controllers
{
	[Authorize(Roles = "Admin")]
	public class AdminController(IGameStatsService gameStatsService) : Controller
	{
		[HttpGet]
		public async Task<IActionResult> Index()
		{
			// Her oyun türünün istatistikleri ayrı gösterilir
			var stats = new List<GameStatsViewModel>();

			foreach (var type in Enum.GetValues<GameType>())
				stats.Add(await gameStatsService.GetStatsAsync(type));

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
