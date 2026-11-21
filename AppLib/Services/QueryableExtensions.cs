using Vokabeln.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Vokabeln.AppLib.Services
{
	public static class QueryableExtensions
	{
		public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
			this IQueryable<T> query, int page, int pageSize, int minPageSize = 10, int maxPageSize = 50)
		{
			pageSize = Math.Clamp(pageSize, minPageSize, maxPageSize);

			var totalCount = await query.CountAsync();
			var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
			page = Math.Clamp(page, 1, totalPages);

			var items = await query
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
				.ToListAsync();

			return new PagedResult<T>
			{
				Items = items,
				Page = page,
				PageSize = pageSize,
				TotalCount = totalCount
			};
		}
	}
}
