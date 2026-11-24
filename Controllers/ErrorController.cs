using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Vokabeln.AppLib.Logging;
using Vokabeln.ViewModels;

namespace Vokabeln.Controllers
{
	// Merkezi hata sayfaları (Program.cs):
	// - UseExceptionHandler("/Error"): MVC filtresine düşmeyen hatalar (görünüm oluşturma, middleware) -> 500
	// - UseStatusCodePagesWithReExecute("/Error/{0}"): gövdesiz hata durumları (404, 400, 403...) -> ilgili sayfa
	// Hatalar ve 404'ler Hata Kayıtları'na yazılır. Orijinal istek hangi metotla geldiyse (GET/POST) o metotla çalışır.
	[AllowAnonymous]
	[ApiExplorerSettings(IgnoreApi = true)]
	public class ErrorController(ILogStore logStore, ILogger<ErrorController> logger) : Controller
	{
		// Tarayıcıların kendiliğinden istediği dosyalar 404 olarak kaydedilmez
		private static readonly string[] IgnoredPaths = ["/favicon.ico", "/apple-touch-icon", "/.well-known/", "/robots.txt"];

		[Route("Error")]
		[Route("Error/{code:int}")]
		public async Task<IActionResult> Index(int? code)
		{
			var exceptionFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
			var statusFeature = HttpContext.Features.Get<IStatusCodeReExecuteFeature>();

			var statusCode = exceptionFeature is not null ? StatusCodes.Status500InternalServerError : code ?? StatusCodes.Status500InternalServerError;
			var path = exceptionFeature?.Path ?? statusFeature?.OriginalPath ?? HttpContext.Request.Path.Value;
			var query = statusFeature?.OriginalQueryString;

			long? errorId = null;
			var shouldRecord = exceptionFeature is not null
				|| (statusFeature is not null && !IgnoredPaths.Any(p => path?.StartsWith(p, StringComparison.OrdinalIgnoreCase) == true));

			if (shouldRecord)
			{
				if (exceptionFeature is not null)
					logger.LogError(exceptionFeature.Error, "İşlenmeyen hata: {Path}", path);
				else
					logger.LogWarning("HTTP {StatusCode}: {Path}{Query}", statusCode, path, query);

				try
				{
					errorId = await logStore.AddErrorAsync(ErrorLogEntry.From(HttpContext, statusCode, exceptionFeature?.Error, path, query));
				}
				catch (Exception ex)
				{
					logger.LogWarning(ex, "Hata kaydı yazılamadı");
				}
			}

			Response.StatusCode = statusCode;

			var model = new ErrorPageViewModel
			{
				StatusCode = statusCode,
				ErrorId = errorId,
				Path = path + query
			};

			// fetch ile yapılan isteklere sayfa yerine JSON
			if (Request.Headers.Accept.ToString().Contains("application/json") || Request.Headers.XRequestedWith == "XMLHttpRequest")
				return Json(new { success = false, message = model.Title, errorId });

			return View(statusCode == StatusCodes.Status404NotFound ? "NotFound" : "Index", model);
		}
	}
}
