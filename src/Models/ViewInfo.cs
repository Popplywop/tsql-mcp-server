using System.Text.Json.Serialization;

namespace Models;

public class ViewInfo(string schemaName, string viewName)
{
    public string SchemaName { get; set; } = schemaName;
    public string ViewName { get; set; } = viewName;
    public List<ColumnInfo>? Columns { get; set; } = null;

    /// <summary>
    /// View definition SQL. Null by default to reduce token usage.
    /// Access via sqlserver://schemas/{schema}/views/{view}/definition
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Definition { get; set; } = null;
}
