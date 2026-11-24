using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vokabeln.AppLib.Services;

namespace Vokabeln.Controllers
{
	// Kelime formundaki "PONS'tan çek" bağlantısı buradan Türkçe karşılıkları alır
	[Authorize]
	public class PonsController(IPonsService ponsService) : Controller
	{
		[HttpGet]
		public async Task<IActionResult> Lookup(string? q, CancellationToken cancellationToken)
			=> Json(await ponsService.LookupAsync(q, cancellationToken));
	}
}
