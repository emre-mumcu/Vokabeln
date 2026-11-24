using Microsoft.AspNetCore.Authorization;
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
    [Authorize]
	public class WordsController(IWordService wordService, IConjugationService conjugationService) : Controller
	{
        private const int QuickSearchLimit = 15;

        public async Task<IActionResult> Index(WordSearch search, int page = 1, int pageSize = 10)
        {
			ViewData["Search"] = search;

			var model = await wordService.GetPagedAsync(page, pageSize, search);
			return View(model);
		}

        // Detaylı arama formu; sonuçlar Index'te listelenir
        [HttpGet]
        public IActionResult Search(WordSearch search)
        {
            ModelState.Clear();
            return View(search);
        }

        // Navbar'daki arama kutusunun açılır listesi için
        [HttpGet]
        public async Task<IActionResult> QuickSearch(string? q)
            => Json(await wordService.QuickSearchAsync(q, QuickSearchLimit));

        // "View" adı Controller.View() ile çakıştığı için action adı Details
        [HttpGet]
        public async Task<IActionResult> Details(int id, string? returnUrl)
        {
            var entity = await wordService.GetByIdAsync(id);

            if (entity is null)
                return NotFound();

            ViewData["ReturnUrl"] = returnUrl;

            return View(new WordDetailsViewModel
            {
                Word = entity,
                Conjugations = entity.WordType == WordType.Verb
                    ? await conjugationService.GetForWordAsync(entity.Id)
                    : []
            });
        }

        [HttpGet]
        public async Task<IActionResult> Word(int id = 0, string? returnUrl = null)
        {
            // Kaydet/İptal sonrası gelinen sayfaya (liste, arama sonucu, detay) dönmek için
            ViewData["ReturnUrl"] = returnUrl;

            if (id == 0)
                return View(new WordEntity());

            var entity = await wordService.GetByIdAsync(id);

            if (entity is null)
                return NotFound();

            return View(entity);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Word(WordEntity we, string? returnUrl)
        {
            // Form hatayla tekrar gösterilirse dönüş adresi kaybolmasın
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid) return View(we);

            var isEdit = we.Id > 0;

            try
            {
                await wordService.SaveAsync(we);
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: 19 })
            {
                // 19 = SQLITE_CONSTRAINT: (Word, WordType) unique index'i
                TempData["ErrorMessage"] = $"\"{we.Word}\" bu kelime türüyle zaten kayıtlı.";
                return View(we);
            }
            catch (Exception ex)
            {
                // Form, girilen değerlerle tekrar gösterilir; değişiklikler kaybolmaz
                TempData["ErrorMessage"] = ex.InnerException is null ? ex.Message : $"{ex.Message} {ex.InnerException.Message}";
                return View(we);
            }

            TempData["SuccessMessage"] = "Kelime kaydedildi.";

            // Düzenlemeden sonra gelinen sayfaya, yeni kayıttan sonra bir sonraki kelime için boş forma dönülür
            return isEdit ? RedirectToLocal(returnUrl) : RedirectToAction(nameof(Word));
        }

        // Yalnızca site içi adrese dönülür (open redirect'i önler), yoksa liste
        private IActionResult RedirectToLocal(string? returnUrl)
            => !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? LocalRedirect(returnUrl)
                : RedirectToAction(nameof(Index));

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Delete(int id = 0, string? returnUrl = null)
		{
			try
			{
				if(id > 0)
                {
					await wordService.DeleteAsync(id);
					TempData["SuccessMessage"] = "Deleted!";
				}

				return RedirectToLocal(returnUrl);
			}
			catch (Exception ex)
			{
				// TempData["SuccessMessage"] = "Kelime kaydedildi.";
				TempData["ErrorMessage"] = $"{ex.Message}{ex?.InnerException?.Message}";
				return RedirectToLocal(returnUrl);
			}
		}

		private static readonly JsonSerializerOptions ExportOptions = new()
		{
			WriteIndented = true,
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			Converters = { new JsonStringEnumConverter() },
			Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, // Boş alanlar dosyaya yazılmaz
			PropertyNameCaseInsensitive = true
		};

		// Export seçim ekranı: kelime türü ve dosyaya yazılacak çekim tabloları (varsayılan: tümü)
		[HttpGet]
		public IActionResult Export() => View(new ExportSelection { Tenses = [.. Enum.GetValues<Tense>()] });

		// Kelimeler, fiil bilgileri, tarihler ve seçilen çekimlerle (biçim: ViewModels/WordExportModels.cs).
		// Biçim her zaman aynı WordExport dizisidir; eski ve yeni import'larla uyumludur.
		[HttpPost]
		[ValidateAntiForgeryToken]
		[ActionName("Export")] // GET ile aynı ad; URL aynı kalır
		public async Task<IActionResult> ExportPost(ExportSelection selection)
		{
			var export = await wordService.GetForExportAsync(selection);

			if (export.Count == 0)
			{
				ModelState.AddModelError("", "Seçilen türde kelime yok.");
				return View(selection);
			}

			var bytes = JsonSerializer.SerializeToUtf8Bytes(export, ExportOptions);

			var suffix = selection.SelectedType is { } type ? $"-{type.ToString().ToLowerInvariant()}" : "";
			var fileName = $"words{suffix}-{DateTime.Now:yyyyMMdd-HHmm}.json";

			return File(bytes, "application/json", fileName);
		}


		[HttpGet]
		public IActionResult Import() => View(new ImportOptions { Tenses = [.. Enum.GetValues<Tense>()] });

		// [RequestSizeLimit(100 * 1024 * 1024)] // 100 MB
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Import(IFormFile? file, ImportOptions options)
		{
			if (file is null || file.Length == 0)
			{
				ModelState.AddModelError("", "Lütfen bir JSON dosyası seçin.");
				return View(options);
			}

			if (file.Length > 5 * 1024 * 1024) // 5 MB
			{
				ModelState.AddModelError("", "Dosya çok büyük (en fazla 5 MB).");
				return View(options);
			}

			// Eski export dosyaları da aynı biçimdedir (WordExport dizisi; yeni alanlar yoksa boş sayılır)
			List<WordExport>? words;
			try
			{
				await using var stream = file.OpenReadStream();
				words = await JsonSerializer.DeserializeAsync<List<WordExport>>(stream, ExportOptions);
			}
			catch (JsonException ex)
			{
				ModelState.AddModelError("", $"Geçersiz JSON: {ex.Message}");
				return View(options);
			}

			if (words is null || words.Count == 0)
			{
				ModelState.AddModelError("", "Dosyada kelime bulunamadı.");
				return View(options);
			}

			var result = await wordService.ImportAsync(words, options);
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

				TempData["SuccessMessage"] = "Yedek başarıyla geri yüklendi.";
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

		// Veritabanını sıfırla: onay için kutuya "dbreset" yazılması gerekir (sunucuda da kontrol edilir)
		private const string ResetConfirmText = "dbreset";

		[HttpGet]
		public IActionResult Reset() => View();

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Reset(string? confirmText)
		{
			if (!string.Equals(confirmText?.Trim(), ResetConfirmText, StringComparison.Ordinal))
			{
				ModelState.AddModelError("", $"Onay için kutuya \"{ResetConfirmText}\" yazın.");
				return View();
			}

			try
			{
				var backupPath = await wordService.ResetDatabaseAsync();

				TempData["SuccessMessage"] = $"Veritabanı sıfırlandı. Önceki verinin yedeği: Backups/{Path.GetFileName(backupPath)}";
				return RedirectToAction("Index", "Home");
			}
			catch (InvalidOperationException ex)
			{
				ModelState.AddModelError("", ex.Message);
				return View();
			}
			catch (SqliteException ex)
			{
				ModelState.AddModelError("", $"Sıfırlama başarısız: {ex.Message}");
				return View();
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
		public IActionResult Checkpoint() => View(wordService.GetDbFiles());

		[HttpPost]
		[ActionName("Checkpoint")] // GET ile aynı imza olduğu için C# adı farklı, URL aynı
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> CheckpointPost()
		{
			var result = await wordService.CheckpointAsync();

			if (result.Busy)
				TempData["ErrorMessage"] = "Veritabanı meşgul, aktarım tamamlanamadı. Biraz sonra tekrar deneyin.";
			else if (result.WalPages < 0)
				TempData["SuccessMessage"] = "Veritabanı WAL modunda değil, aktarılacak veri yok. Bağlantılar kapatıldı.";
			else
				// TRUNCATE sonrası sayfa sayıları 0 döner, bu yüzden mesajda sayı verilmez
				TempData["SuccessMessage"] = "Tüm veri veritabanı dosyasına aktarıldı, bağlantılar kapatıldı.";

			return RedirectToAction(nameof(Checkpoint));
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

		// Listedeki kelimeleri yazdırır: arama varsa sonuçları, yoksa tümünü (sayfalama olmadan)
		[HttpGet]
		public async Task<IActionResult> PrintList(WordSearch search, string? returnUrl)
		{
			ViewData["Search"] = search;
			ViewData["ReturnUrl"] = returnUrl;

			var words = await wordService.GetForPrintAsync(search);
			return View(words);
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
