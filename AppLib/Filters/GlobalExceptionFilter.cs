using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

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

		public GlobalExceptionFilter(
			ILogger<GlobalExceptionFilter> logger,
			ITempDataDictionaryFactory tempDataFactory)
		{
			_logger = logger;
			_tempDataFactory = tempDataFactory;
		}

		public Task OnExceptionAsync(ExceptionContext context)
		{
			var ex = context.Exception;
			_logger.LogError(ex, "İşlenmeyen hata: {Path}", context.HttpContext.Request.Path);

			var message = ex is AppException or KeyNotFoundException
				? ex.Message
				: GenericMessage;

			var request = context.HttpContext.Request;

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
			return Task.CompletedTask;
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
