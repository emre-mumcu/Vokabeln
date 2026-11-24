using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Vokabeln.AppLib.Logging;

namespace Vokabeln.AppLib.Filters
{
	public class AppException : Exception
	{
		public AppException(string message) : base(message) { }
	}

	public class GlobalExceptionFilter : IAsyncExceptionFilter
	{
		private const string GenericMessage = "Unknown Exception";

		private readonly ILogger<GlobalExceptionFilter> _logger;
		private readonly ITempDataDictionaryFactory _tempDataFactory;
		private readonly ILogStore _logStore;

		public GlobalExceptionFilter(
			ILogger<GlobalExceptionFilter> logger,
			ITempDataDictionaryFactory tempDataFactory,
			ILogStore logStore)
		{
			_logger = logger;
			_tempDataFactory = tempDataFactory;
			_logStore = logStore;
		}

		// Controller/action hataları burada yakalanır (görünüm oluşturma ve middleware hataları ErrorController'a düşer).
		// AppException kullanıcıya gösterilen iş kuralı mesajıdır: yalnızca uyarı olarak loglanır, Hata Kayıtları'na yazılmaz.
		public async Task OnExceptionAsync(ExceptionContext context)
		{
			var ex = context.Exception;
			var request = context.HttpContext.Request;
			string message;

			if (ex is AppException)
			{
				_logger.LogWarning("İş kuralı hatası: {Message} ({Path})", ex.Message, request.Path);
				message = ex.Message;
			}
			else
			{
				_logger.LogError(ex, "İşlenmeyen hata: {Path}", request.Path);

				var errorId = await RecordAsync(context.HttpContext, ex);
				var text = ex is KeyNotFoundException ? ex.Message : GenericMessage;
				message = errorId is null ? text : $"{text} (Hata No: {errorId})";
			}

			if (IsAjax(request))
			{
				context.Result = new JsonResult(new { success = false, message })
				{
					StatusCode = StatusCodes.Status500InternalServerError
				};
			}
			else
			{
				var tempData = _tempDataFactory.GetTempData(context.HttpContext);
				tempData["ErrorMessage"] = message;

				context.Result = new RedirectResult(GetReturnUrl(request));
			}

			context.ExceptionHandled = true;
		}

		// Hata Kayıtları'na yazar; yazılamazsa (ör. Logs.db kilitli) kullanıcı yine de mesajı görür
		private async Task<long?> RecordAsync(HttpContext httpContext, Exception ex)
		{
			try
			{
				return await _logStore.AddErrorAsync(ErrorLogEntry.From(httpContext, StatusCodes.Status500InternalServerError, ex));
			}
			catch (Exception recordError)
			{
				_logger.LogWarning(recordError, "Hata kaydı yazılamadı");
				return null;
			}
		}

		private static bool IsAjax(HttpRequest request) =>
			request.Headers.XRequestedWith == "XMLHttpRequest" ||
			request.Headers.Accept.ToString().Contains("application/json");

		private static string GetReturnUrl(HttpRequest request)
		{
			// Sadece aynı siteden gelen Referer'a dön (open redirect'i önler)
			if (Uri.TryCreate(request.Headers.Referer.ToString(), UriKind.Absolute, out var referer)
				&& string.Equals(referer.Host, request.Host.Host, StringComparison.OrdinalIgnoreCase)
				&& !string.Equals(referer.AbsolutePath, request.Path, StringComparison.OrdinalIgnoreCase))
			{
				return referer.PathAndQuery;
			}

			return "/";
		}
	}
}
