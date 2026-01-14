namespace Models;

public class QueryResult
{
    /// <summary>
    /// Column names in the result set
    /// </summary>
    public List<string> Columns { get; set; } = [];

    /// <summary>
    /// Rows in the result set
    /// </summary>
    public List<Dictionary<string, object>> Rows { get; set; } = [];

    /// <summary>
    /// Number of rows returned in this result
    /// </summary>
    public int RowCount { get; set; }

    /// <summary>
    /// Total number of rows in the full result set (before limiting)
    /// Only set when HasMoreRows is true
    /// </summary>
    public int? TotalRowCount { get; set; }

    /// <summary>
    /// Number of rows affected by the query (for non-SELECT queries)
    /// </summary>
    public int RowsAffected { get; set; }

    /// <summary>
    /// Whether the query was successful
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Message describing the result or error
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// SQL error code (if applicable)
    /// </summary>
    public int? ErrorCode { get; set; }

    /// <summary>
    /// Indicates if there are more rows available beyond the max rows limit
    /// </summary>
    public bool HasMoreRows { get; set; }
}
