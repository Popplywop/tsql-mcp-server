using ModelContextProtocol.Server;
using Services;
using System.ComponentModel;
using System.Text.Json;

namespace Tools;

[McpServerToolType]
public class SchemaTool(IQueryService queryService)
{
    private readonly IQueryService _queryService = queryService;
    private static readonly JsonSerializerOptions _compactJsonOptions = new() { WriteIndented = false };

    [McpServerTool, Description("Get column names and types for a table without fetching any data. Useful for understanding table structure before querying.")]
    public async Task<string> GetTableColumns(
        [Description("The schema name (e.g., 'dbo')")] string schema,
        [Description("The table name")] string tableName,
        CancellationToken cancellationToken = default)
    {
        var result = await _queryService.ExecuteQueryAsync(
            $"SELECT COLUMN_NAME as Name, DATA_TYPE as Type, CHARACTER_MAXIMUM_LENGTH as MaxLength, IS_NULLABLE as Nullable FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = '{EscapeSqlString(schema)}' AND TABLE_NAME = '{EscapeSqlString(tableName)}' ORDER BY ORDINAL_POSITION",
            commandTimeout: 10,
            maxRows: 500,
            cancellationToken: cancellationToken);

        if (!result.IsSuccess)
        {
            return $"Error: {result.Message}";
        }

        return JsonSerializer.Serialize(new
        {
            Schema = schema,
            Table = tableName,
            Columns = result.Rows
        }, _compactJsonOptions);
    }

    [McpServerTool, Description("Get row count for a table without fetching data. Useful for understanding data volume before querying.")]
    public async Task<string> GetTableRowCount(
        [Description("The schema name (e.g., 'dbo')")] string schema,
        [Description("The table name")] string tableName,
        CancellationToken cancellationToken = default)
    {
        var result = await _queryService.ExecuteQueryAsync(
            $"SELECT COUNT(*) as Row_Count FROM [{EscapeSqlString(schema)}].[{EscapeSqlString(tableName)}]",
            commandTimeout: 30,
            maxRows: 1,
            cancellationToken: cancellationToken);

        if (!result.IsSuccess)
        {
            return $"Error: {result.Message}";
        }

        var count = result.Rows.FirstOrDefault()?["Row_Count"] ?? 0;
        return JsonSerializer.Serialize(new
        {
            Schema = schema,
            Table = tableName,
            RowCount = count
        }, _compactJsonOptions);
    }

    [McpServerTool, Description("List all tables in a schema with their row counts. Useful for exploring database structure.")]
    public async Task<string> ListTables(
        [Description("The schema name (e.g., 'dbo')")] string schema,
        [Description("Include row counts (slower but more informative)")] bool includeRowCounts = false,
        CancellationToken cancellationToken = default)
    {
        string query;
        if (includeRowCounts)
        {
            query = $@"
                SELECT
                    t.TABLE_NAME as TableName,
                    p.rows as Row_Count 
                FROM INFORMATION_SCHEMA.TABLES t
                LEFT JOIN sys.partitions p ON p.object_id = OBJECT_ID(t.TABLE_SCHEMA + '.' + t.TABLE_NAME) AND p.index_id IN (0, 1)
                WHERE t.TABLE_SCHEMA = '{EscapeSqlString(schema)}' AND t.TABLE_TYPE = 'BASE TABLE'
                ORDER BY t.TABLE_NAME";
        }
        else
        {
            query = $@"
                SELECT TABLE_NAME as TableName
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_SCHEMA = '{EscapeSqlString(schema)}' AND TABLE_TYPE = 'BASE TABLE'
                ORDER BY TABLE_NAME";
        }

        var result = await _queryService.ExecuteQueryAsync(query, commandTimeout: 30, maxRows: 1000, cancellationToken: cancellationToken);

        if (!result.IsSuccess)
        {
            return $"Error: {result.Message}";
        }

        return JsonSerializer.Serialize(new
        {
            Schema = schema,
            Tables = result.Rows
        }, _compactJsonOptions);
    }

    [McpServerTool, Description("Get primary key columns for a table.")]
    public async Task<string> GetPrimaryKey(
        [Description("The schema name (e.g., 'dbo')")] string schema,
        [Description("The table name")] string tableName,
        CancellationToken cancellationToken = default)
    {
        var query = $@"
            SELECT COLUMN_NAME as ColumnName
            FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
            WHERE OBJECTPROPERTY(OBJECT_ID(CONSTRAINT_SCHEMA + '.' + CONSTRAINT_NAME), 'IsPrimaryKey') = 1
            AND TABLE_SCHEMA = '{EscapeSqlString(schema)}'
            AND TABLE_NAME = '{EscapeSqlString(tableName)}'
            ORDER BY ORDINAL_POSITION";

        var result = await _queryService.ExecuteQueryAsync(query, commandTimeout: 10, maxRows: 50, cancellationToken: cancellationToken);

        if (!result.IsSuccess)
        {
            return $"Error: {result.Message}";
        }

        return JsonSerializer.Serialize(new
        {
            Schema = schema,
            Table = tableName,
            PrimaryKeyColumns = result.Rows.Select(r => r["ColumnName"]).ToList()
        }, _compactJsonOptions);
    }

    [McpServerTool, Description("Get a sample of data from a table (first N rows). Useful for understanding data format.")]
    public async Task<string> GetSampleData(
        [Description("The schema name (e.g., 'dbo')")] string schema,
        [Description("The table name")] string tableName,
        [Description("Number of sample rows to return (default: 5, max: 20)")] int sampleSize = 5,
        CancellationToken cancellationToken = default)
    {
        sampleSize = Math.Min(Math.Max(1, sampleSize), 20); // Clamp between 1 and 20

        var result = await _queryService.ExecuteQueryAsync(
            $"SELECT TOP {sampleSize} * FROM [{EscapeSqlString(schema)}].[{EscapeSqlString(tableName)}]",
            commandTimeout: 10,
            maxRows: sampleSize,
            cancellationToken: cancellationToken);

        if (!result.IsSuccess)
        {
            return $"Error: {result.Message}";
        }

        return JsonSerializer.Serialize(new
        {
            Schema = schema,
            Table = tableName,
            SampleSize = result.RowCount,
            Columns = result.Columns,
            Rows = result.Rows
        }, _compactJsonOptions);
    }

    private static string EscapeSqlString(string input)
    {
        // Basic SQL injection prevention for identifier names
        return input.Replace("'", "''").Replace("[", "").Replace("]", "").Replace(";", "");
    }
}
