using System.Text.Json.Serialization;

namespace Models;

public class ProcedureInfo(string schemaName, string procedureName)
{
    public string SchemaName { get; set; } = schemaName;
    public string ProcedureName { get; set; } = procedureName;
    public List<ParameterInfo>? Parameters { get; set; } = null;

    /// <summary>
    /// Procedure definition SQL. Null by default to reduce token usage.
    /// Access via sqlserver://schemas/{schema}/procedures/{procedure}/definition
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Definition { get; set; } = null;
}
