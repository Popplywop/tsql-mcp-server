using ModelContextProtocol.Server;
using Services;
using System.ComponentModel;
using System.Text.Json;

namespace Tools;

[McpServerToolType]
public class QueryTool(IQueryService queryService)
{
    private readonly IQueryService _queryService = queryService;
    private static readonly JsonSerializerOptions _compactJsonOptions = new() { WriteIndented = false };
    private static readonly JsonSerializerOptions _prettyJsonOptions = new() { WriteIndented = true };

    [McpServerTool, Description("Executes a SQL query against the database and returns the results.")]
    public async Task<string> ExecuteQuery(
        [Description("The SQL query to execute")] string query,
        [Description("Optional command timeout in seconds")] int? commandTimeout = null,
        [Description("Optional maximum number of rows to return (default: 1000)")] int? maxRows = null,
        [Description("Optional maximum characters to return - truncates results if exceeded")] int? maxChars = null,
        [Description("Use compact JSON output to reduce token usage (default: true)")] bool compact = true,
        CancellationToken cancellationToken = default)
    {
        var result = await _queryService.ExecuteQueryAsync(query, commandTimeout, maxRows, cancellationToken);

        if (!result.IsSuccess)
        {
            return $"Error executing query: {result.Message}" + (result.ErrorCode.HasValue ? $" (Error code: {result.ErrorCode})" : "");
        }

        if (result.Rows.Count > 0)
        {
            var jsonOptions = compact ? _compactJsonOptions : _prettyJsonOptions;
            var json = JsonSerializer.Serialize(result, jsonOptions);

            // Apply character limit if specified
            if (maxChars.HasValue && json.Length > maxChars.Value)
            {
                // Return summary instead of truncated JSON
                var summary = new
                {
                    result.Columns,
                    result.RowCount,
                    TotalRowCount = result.TotalRowCount,
                    Truncated = true,
                    TruncatedAt = maxChars.Value,
                    Message = $"Results exceeded {maxChars.Value} characters. Showing summary only. Use smaller maxRows or query specific columns.",
                    SampleRows = result.Rows.Take(3)
                };
                return JsonSerializer.Serialize(summary, jsonOptions);
            }

            return json;
        }
        else
        {
            return result.Message;
        }
    }
}
