# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/claude-code) when working with this repository.

## Project Overview

This is a Model Context Protocol (MCP) server for SQL Server databases. It allows LLMs to query and inspect SQL Server databases through a standardized protocol using stdio transport.

## Tech Stack

- **.NET 10.0** - Target framework
- **ModelContextProtocol SDK** (0.5.0-preview.1) - MCP server implementation
- **CommandLineParser** - CLI argument parsing
- **Microsoft.Data.SqlClient** - SQL Server connectivity

## Project Structure

```
src/
├── Program.cs              # Entry point, host configuration
├── Options.cs              # CLI options (--dsn, --env-var, --read-only)
├── Tools/
│   ├── QueryTool.cs        # ExecuteQuery MCP tool
│   └── StoredProcedureTool.cs  # ExecuteStoredProcedure MCP tool
├── Services/
│   ├── QueryService.cs     # Query execution with read-only validation
│   ├── SqlConnectionService.cs
│   ├── SqlConnectionFactory.cs
│   ├── SqlInjectionValidationService.cs
│   └── DatabaseMetadataCache.cs
├── Handlers/
│   └── DatabaseResourceHandler.cs  # MCP resource handlers
└── Models/                 # Data models (TableInfo, ColumnInfo, etc.)
tests/
└── tsql-mcp-server-tests/  # Unit tests (xUnit)
```

## Build & Run Commands

```bash
# Build
dotnet build src/tsql-mcp-server.csproj

# Run with connection string
dotnet run --project src/tsql-mcp-server.csproj -- --dsn "Server=...;Database=...;..."

# Run in read-only mode
dotnet run --project src/tsql-mcp-server.csproj -- --dsn "..." --read-only

# Run tests
dotnet test

# Build Docker image
docker build -t tsql-mcp-server .

# Run Docker container
docker run -i tsql-mcp-server --dsn "Server=host.docker.internal;Database=...;..."
```

## Key Features

1. **Read-Only Mode** (`--read-only`): Blocks INSERT, UPDATE, DELETE, TRUNCATE, DROP, ALTER, CREATE, MERGE operations
2. **SQL Injection Prevention**: Built-in validation in `SqlInjectionValidationService`
3. **Stored Procedure Execution**: Safe execution with parameterized inputs
4. **Resource Caching**: 10-minute TTL cache for schema metadata

## Adding New MCP Tools

1. Create a new class in `src/Tools/` with `[McpServerToolType]` attribute
2. Add methods with `[McpServerTool]` and `[Description]` attributes
3. Tools are auto-discovered via `WithToolsFromAssembly()`

Example:
```csharp
[McpServerToolType]
public class MyTool(QueryService queryService)
{
    [McpServerTool, Description("Description of what this tool does")]
    public async Task<string> MyMethod(
        [Description("Parameter description")] string param,
        CancellationToken cancellationToken = default)
    {
        // Implementation
    }
}
```

## Docker Notes

- Uses multi-stage build with .NET 10.0 SDK/runtime
- Stdio transport requires `-i` flag when running
- Use `host.docker.internal` to connect to host SQL Server
- For WSL2 networking issues, try `--network host` with `localhost`
