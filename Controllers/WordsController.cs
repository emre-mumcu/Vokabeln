using Microsoft.AspNetCore.Mvc;
using Vokabeln.AppData;
using Microsoft.EntityFrameworkCore;
using Vokabeln.AppData.Entities;
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
                return RedirectToAction(nameof(Index));
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
	}
}
