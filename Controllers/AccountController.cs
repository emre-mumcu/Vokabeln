using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Vokabeln.Controllers
{
    public class AccountController : Controller
    {
        private readonly IOptionsMonitor<CookieAuthenticationOptions> _cookieOptions;

        public AccountController(IOptionsMonitor<CookieAuthenticationOptions> cookieOptions)
        {
            _cookieOptions = cookieOptions;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ActionName("Login")] 
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LoginPost(string? returnUrl)
        {
            var opts = _cookieOptions.Get(CookieAuthenticationDefaults.AuthenticationScheme);

            var now = DateTimeOffset.UtcNow;

            var claims = new List<Claim>
            {
                new Claim("LoginTimeStamp", now.ToString("o")), // ISO 8601 formatı
                new Claim("ExpireStamp", now.Add(opts.ExpireTimeSpan).ToString("o")), // ISO 8601 formatı
                new Claim(ClaimTypes.NameIdentifier, "dummy-user"),
                new Claim(ClaimTypes.Name, "Dummy User"),
                new Claim(ClaimTypes.Role, "Guest"),
                new Claim(ClaimTypes.Role, "User"),
                new Claim(ClaimTypes.Role, "Admin"),
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
            {
                IsPersistent = false,
            });

            // Sadece site içi adrese dön (open redirect'i önler)
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            return RedirectToAction("Index", "Home");
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> KalanSure()
        {
            var result = await HttpContext.AuthenticateAsync();
            var bitis = result?.Properties?.ExpiresUtc;
            var saniye = bitis is null ? 0 : (int)Math.Max(0, (bitis.Value - DateTimeOffset.UtcNow).TotalSeconds);
            return Json(new { saniye });
        }

    }
}
