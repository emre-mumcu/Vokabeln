using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vokabeln.AppData;
using Vokabeln.AppData.Entities;
using Vokabeln.AppData.Enums;
using Vokabeln.AppLib.Services;
using Vokabeln.ViewModels;

namespace Vokabeln.Controllers
{
	public class WordsController(AppDbContext dbContext, IWordService wordService) : Controller
	{
        public async Task<IActionResult> Index(int page = 1, int pageSize = 10)
        {
			

			var model = await wordService.GetPagedAsync(page, pageSize);
			return View(model);
		}

        [HttpGet]
        public async Task<IActionResult> Word(int id = 0)
        {
            if (id == 0)
                return View(new WordEntity());

            var entity = await wordService.GetByIdAsync(id);

            if (entity is null)
                return NotFound();

            return View(entity);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Word(WordEntity we)
        {
            try
            {
                if (!ModelState.IsValid) return View(we);
                await wordService.SaveAsync(we);
                return RedirectToAction(nameof(Word));
            }
            catch (Exception ex)
            {
                // TempData["SuccessMessage"] = "Kelime kaydedildi.";
                TempData["ErrorMessage"] = $"{ex.Message}{ex.InnerException.Message}";
                return RedirectToAction(nameof(Word));
			}
        }

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Delete(int id = 0)
		{
			try
			{
				if(id > 0)
                {
					await wordService.DeleteAsync(id);
					TempData["SuccessMessage"] = "Deleted!";
				}

				return RedirectToAction(nameof(Index));
			}
			catch (Exception ex)
			{
				// TempData["SuccessMessage"] = "Kelime kaydedildi.";
				TempData["ErrorMessage"] = $"{ex.Message}{ex.InnerException.Message}";
				return RedirectToAction(nameof(Index));
			}
		}

		private static readonly JsonSerializerOptions ExportOptions = new()
		{
			WriteIndented = true,
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			Converters = { new JsonStringEnumConverter() },
			Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
		};

		[HttpGet]
		public async Task<IActionResult> Export()
		{
			var words = await wordService.GetAllAsync();
						
			var export = words.Select(w => new
			{
				w.Word,
				w.WordType,
				w.Plural,
				w.Meaning,
				w.Sample
			});

			var bytes = JsonSerializer.SerializeToUtf8Bytes(export, ExportOptions);

			var fileName = $"words-{DateTime.Now:yyyyMMdd-HHmm}.json";

			return File(bytes, "application/json", fileName);
		}


		[HttpGet]
		public IActionResult Import() => View();

		// [RequestSizeLimit(100 * 1024 * 1024)] // 100 MB
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Import(IFormFile? file, ImportMode mode = ImportMode.Bulk)
		{
			if (file is null || file.Length == 0)
			{
				ModelState.AddModelError("", "Lütfen bir JSON dosyası seçin.");
				return View();
			}

			if (file.Length > 5 * 1024 * 1024) // 5 MB
			{
				ModelState.AddModelError("", "Dosya çok büyük (en fazla 5 MB).");
				return View();
			}

			List<WordEntity>? words;
			try
			{
				await using var stream = file.OpenReadStream();
				words = await JsonSerializer.DeserializeAsync<List<WordEntity>>(stream, ExportOptions);
			}
			catch (JsonException ex)
			{
				ModelState.AddModelError("", $"Geçersiz JSON: {ex.Message}");
				return View();
			}

			if (words is null || words.Count == 0)
			{
				ModelState.AddModelError("", "Dosyada kelime bulunamadı.");
				return View();
			}

			var result = await wordService.ImportAsync(words, mode);
			return View("ImportResult", result);
		}

		[HttpGet]
		public async Task<IActionResult> Backup()
		{
			var path = await wordService.CreateBackupAsync();

			// DeleteOnClose: indirme bitince geçici dosya otomatik silinir
			var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
				bufferSize: 4096, FileOptions.DeleteOnClose);

			var fileName = $"vokabeln-backup-{DateTime.Now:yyyyMMdd-HHmm}.db";
			return File(stream, "application/vnd.sqlite3", fileName);
		}

		[HttpGet]
		public IActionResult Restore() => View();

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Restore(IFormFile? file, bool confirm)
		{
			if (!confirm)
			{
				ModelState.AddModelError("", "Devam etmek için onay kutusunu işaretleyin.");
				return View();
			}

			if (file is null || file.Length == 0)
			{
				ModelState.AddModelError("", "Lütfen bir yedek dosyası (.db) seçin.");
				return View();
			}

			var tempPath = Path.Combine(Path.GetTempPath(), $"restore-{Guid.NewGuid():N}.db");

			try
			{
				await using (var fs = new FileStream(tempPath, FileMode.Create))
				{
					await file.CopyToAsync(fs);
				}

				await wordService.RestoreAsync(tempPath);

				TempData["Message"] = "Yedek başarıyla geri yüklendi.";
				return RedirectToAction(nameof(Index));
			}
			catch (InvalidOperationException ex)
			{
				ModelState.AddModelError("", ex.Message);
				return View();
			}
			catch (SqliteException ex)
			{
				ModelState.AddModelError("", $"Geri yükleme başarısız: {ex.Message}");
				return View();
			}
			finally
			{
				// Geçici dosyayı sil
				if (System.IO.File.Exists(tempPath))
					System.IO.File.Delete(tempPath);
			}
		}

		[HttpGet]
		public async Task<IActionResult> Stats()
		{
			var stats = await wordService.GetStatsAsync();
			return View(stats);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DeleteSafetyBackups()
		{
			var count = await wordService.DeleteSafetyBackupsAsync();

			TempData["SuccessMessage"] = $"{count} güvenlik yedeği silindi.";
			return RedirectToAction(nameof(Stats));
		}

		[HttpGet]
		public async Task<IActionResult> Print(PrintViewModel filter)
		{
			// Hiçbiri seçili değilse (ilk açılış dahil) hepsi seçili say
			if (!filter.Der && !filter.Die && !filter.Das)
				filter.Der = filter.Die = filter.Das = true;

			// Formdaki checkbox'lar ModelState yerine bu düzeltilmiş değerleri göstersin
			ModelState.Clear();

			filter.Words = await wordService.GetForPrintAsync(
				filter.SelectedType, filter.Der, filter.Die, filter.Das);

			return View(filter);
		}

		private bool IsLocalRequest()
		{
			var ip = HttpContext.Connection.RemoteIpAddress;
			return ip is null || IPAddress.IsLoopback(ip);
		}

		private async Task<SqlExplorerViewModel> BuildSqlViewAsync(
			string? table, string sql = "", bool allowWrite = false, SqlResult? result = null)
		{
			return new SqlExplorerViewModel
			{
				Tables = await wordService.GetTablesAsync(),
				Detail = string.IsNullOrWhiteSpace(table) ? null : await wordService.GetTableDetailAsync(table),
				Sql = sql,
				AllowWrite = allowWrite,
				Result = result
			};
		}

		[HttpGet]
		public async Task<IActionResult> Sql(string? table)
		{
			if (!IsLocalRequest()) return NotFound();

			return View(await BuildSqlViewAsync(table));
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Sql(string? table, string? sql, bool allowWrite)
		{
			if (!IsLocalRequest()) return NotFound();

			SqlResult? result = null;

			if (string.IsNullOrWhiteSpace(sql))
				ModelState.AddModelError("", "Lütfen bir SQL komutu yazın.");
			else
				result = await wordService.ExecuteSqlAsync(sql, allowWrite);

			// Tablo listesi, komuttan sonra güncel sayılarla oluşturulur
			return View(await BuildSqlViewAsync(table, sql ?? "", allowWrite, result));
		}
	}
}
