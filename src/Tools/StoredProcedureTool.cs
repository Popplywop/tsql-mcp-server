using ModelContextProtocol.Server;
using Services;
using System.ComponentModel;
using System.Text.Json;

namespace Tools;

[McpServerToolType]
public class StoredProcedureTool(IQueryService queryService)
{
    private readonly IQueryService _queryService = queryService;
    private static readonly JsonSerializerOptions _compactJsonOptions = new() { WriteIndented = false };
    private static readonly JsonSerializerOptions _prettyJsonOptions = new() { WriteIndented = true };

    [McpServerTool, Description("Executes a stored procedure with optional parameters and returns the results.")]
    public async Task<string> ExecuteStoredProcedure(
        [Description("The schema name (e.g., 'dbo')")] string schema,
        [Description("The stored procedure name")] string procedureName,
        [Description("JSON object of parameters (e.g., {\"@param1\": \"value\", \"@param2\": 123}). Parameter names can optionally include the @ prefix.")] string? parametersJson = null,
        [Description("Optional command timeout in seconds")] int? commandTimeout = null,
        [Description("Optional maximum number of rows to return")] int? maxRows = null,
        [Description("Optional maximum characters to return - truncates results if exceeded")] int? maxChars = null,
        [Description("Use compact JSON output to reduce token usage (default: true)")] bool compact = true,
        CancellationToken cancellationToken = default)
    {
        Dictionary<string, object?>? parameters = null;

        if (!string.IsNullOrWhiteSpace(parametersJson))
        {
            try
            {
                parameters = JsonSerializer.Deserialize<Dictionary<string, object?>>(parametersJson);
            }
            catch (JsonException ex)
            {
                return $"Error parsing parameters JSON: {ex.Message}";
            }
        }

        var result = await _queryService.ExecuteStoredProcedureAsync(
            schema,
            procedureName,
            parameters,
            commandTimeout,
            maxRows,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return $"Error executing stored procedure: {result.Message}" +
                   (result.ErrorCode.HasValue ? $" (Error code: {result.ErrorCode})" : "");
        }

        if (result.Rows.Count > 0)
        {
            var jsonOptions = compact ? _compactJsonOptions : _prettyJsonOptions;
            var json = JsonSerializer.Serialize(result, jsonOptions);

            // Apply character limit if specified
            if (maxChars.HasValue && json.Length > maxChars.Value)
            {
                var summary = new
                {
                    result.Columns,
                    result.RowCount,
                    result.TotalRowCount,
                    Truncated = true,
                    TruncatedAt = maxChars.Value,
                    Message = $"Results exceeded {maxChars.Value} characters. Showing summary only. Use smaller maxRows.",
                    SampleRows = result.Rows.Take(3)
                };
                return JsonSerializer.Serialize(summary, jsonOptions);
            }

            return json;
        }
        else
        {
            return string.IsNullOrEmpty(result.Message)
                ? "Stored procedure executed successfully with no results."
                : result.Message;
        }
    }
}
