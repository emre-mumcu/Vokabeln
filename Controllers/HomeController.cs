using Microsoft.AspNetCore.Mvc;

namespace Vokabeln.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }
    }
}
