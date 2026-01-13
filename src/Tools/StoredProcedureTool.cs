using ModelContextProtocol.Server;
using Services;
using System.ComponentModel;
using System.Text.Json;

namespace Tools;

[McpServerToolType]
public class StoredProcedureTool(QueryService queryService)
{
    private readonly QueryService _queryService = queryService;
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    [McpServerTool, Description("Executes a stored procedure with optional parameters and returns the results.")]
    public async Task<string> ExecuteStoredProcedure(
        [Description("The schema name (e.g., 'dbo')")] string schema,
        [Description("The stored procedure name")] string procedureName,
        [Description("JSON object of parameters (e.g., {\"@param1\": \"value\", \"@param2\": 123}). Parameter names can optionally include the @ prefix.")] string? parametersJson = null,
        [Description("Optional command timeout in seconds")] int? commandTimeout = null,
        [Description("Optional maximum number of rows to return")] int? maxRows = null,
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
            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        else
        {
            return result.Message ?? "Stored procedure executed successfully with no results.";
        }
    }
}
