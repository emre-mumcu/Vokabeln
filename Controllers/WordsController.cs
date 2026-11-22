using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vokabeln.AppData;
using Vokabeln.AppData.Entities;
using Vokabeln.AppData.Enums;
using Vokabeln.AppLib.Services;

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
	}
}
