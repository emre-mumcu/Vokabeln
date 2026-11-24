using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vokabeln.AppLib.Logging;

namespace Vokabeln.Controllers
{
	// Tools -> Hata Kayıtları / Log Kayıtları (Logs.db). Tek tek, seçilenleri ya da tümünü silme.
	[Authorize]
	public class LogsController(ILogStore logStore) : Controller
	{
		// ---------- Hata Kayıtları

		[HttpGet]
		public async Task<IActionResult> Errors(ErrorLogFilter filter, int page = 1, int pageSize = 20)
		{
			ViewData["Filter"] = filter;
			return View(await logStore.GetErrorsAsync(filter, page, pageSize));
		}

		[HttpGet]
		public async Task<IActionResult> ErrorDetails(long id, string? returnUrl)
		{
			var entry = await logStore.GetErrorAsync(id);

			if (entry is null)
				return NotFound();

			ViewData["ReturnUrl"] = returnUrl;
			return View(entry);
		}

		// Tek kayıt (satırdaki sil butonu: id) ya da seçilenler (onay kutuları: ids)
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DeleteErrors(long? id, long[] ids, string? returnUrl)
		{
			var count = await logStore.DeleteErrorsAsync(id is { } single ? [single] : ids);

			SetDeleteMessage(count, "hata kaydı");
			return RedirectToLocal(returnUrl, nameof(Errors));
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DeleteAllErrors()
		{
			var count = await logStore.DeleteAllErrorsAsync();

			TempData["SuccessMessage"] = $"Tüm hata kayıtları silindi ({count}).";
			return RedirectToAction(nameof(Errors));
		}

		// ---------- Log Kayıtları

		[HttpGet]
		public async Task<IActionResult> Index(AppLogFilter filter, int page = 1, int pageSize = 20)
		{
			ViewData["Filter"] = filter;
			return View(await logStore.GetLogsAsync(filter, page, pageSize));
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DeleteLogs(long? id, long[] ids, string? returnUrl)
		{
			var count = await logStore.DeleteLogsAsync(id is { } single ? [single] : ids);

			SetDeleteMessage(count, "log kaydı");
			return RedirectToLocal(returnUrl, nameof(Index));
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DeleteAllLogs()
		{
			var count = await logStore.DeleteAllLogsAsync();

			TempData["SuccessMessage"] = $"Tüm log kayıtları silindi ({count}).";
			return RedirectToAction(nameof(Index));
		}

		// ---------- ortak

		private void SetDeleteMessage(int count, string what)
		{
			if (count == 0)
				TempData["ErrorMessage"] = "Silinecek kayıt seçilmedi.";
			else
				TempData["SuccessMessage"] = $"{count} {what} silindi.";
		}

		// Yalnızca site içi adrese dönülür (open redirect'i önler)
		private IActionResult RedirectToLocal(string? returnUrl, string fallbackAction)
			=> !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
				? LocalRedirect(returnUrl)
				: RedirectToAction(fallbackAction);
	}
}
