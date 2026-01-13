using CommandLine;

namespace tsql_mcp_server;

public class Options
{
    [Option('d', "dsn", Required = false, HelpText = "SQL Server connection string (e.g., \"Server=myserver;Database=mydb;User Id=sa;Password=mypassword;TrustServerCertificate=True;\")")]
    public string? Dsn { get; set; }

    [Option('e', "env-var", Required = false, HelpText = "Environment variable name containing the connection string")]
    public string? EnvVar { get; set; }

    [Option('r', "read-only", Required = false, Default = false, HelpText = "Run in read-only mode (blocks INSERT, UPDATE, DELETE, etc.)")]
    public bool ReadOnly { get; set; }
}
