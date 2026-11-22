namespace Vokabeln.ViewModels
{
	public record ColumnInfo(int Cid, string Name, string Type, bool NotNull, string? Default, int PkOrder);

	public record TableDetail(string Name, List<ColumnInfo> Columns, string? CreateSql);

	public class SqlResult
	{
		public List<string> Columns { get; set; } = [];
		public List<string?[]> Rows { get; set; } = [];
		public bool Truncated { get; set; }
		public int RowsAffected { get; set; }
		public long ElapsedMs { get; set; }
		public string? Error { get; set; }

		public bool HasResultSet => Columns.Count > 0;
	}

	public class SqlExplorerViewModel
	{
		public List<TableStat> Tables { get; init; } = [];
		public TableDetail? Detail { get; init; }
		public string Sql { get; init; } = "";
		public bool AllowWrite { get; init; }
		public SqlResult? Result { get; init; }
	}
}
