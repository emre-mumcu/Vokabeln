namespace Vokabeln.ViewModels
{
	// _Pager partial'ı her türlü sayfalı listeyle kullanılabilsin diye
	public interface IPagedResult
	{
		int Page { get; }
		int PageSize { get; }
		int TotalCount { get; }
		int TotalPages { get; }
		bool HasPrevious { get; }
		bool HasNext { get; }
	}

	public class PagedResult<T> : IPagedResult
	{
		public List<T> Items { get; set; } = new();
		public int Page { get; set; }
		public int PageSize { get; set; }
		public int TotalCount { get; set; }

		public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
		public bool HasPrevious => Page > 1;
		public bool HasNext => Page < TotalPages;
	}
}
