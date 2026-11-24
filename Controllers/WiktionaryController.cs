using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vokabeln.AppData.Enums;
using Vokabeln.AppLib.Services;

namespace Vokabeln.Controllers
{
	// Kelime ve çekim formlarındaki "Wiktionary'den getir" butonu buradan veri alır (yerel kaynak dosyası)
	[Authorize]
	public class WiktionaryController(IWiktionaryService wiktionaryService) : Controller
	{
		[HttpGet]
		public async Task<IActionResult> Lookup(string? q, WordType? type)
			=> Json(await wiktionaryService.LookupAsync(q, type));
	}
}
