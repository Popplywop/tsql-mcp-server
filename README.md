# SQL Server MCP Server

A Model Context Protocol (MCP) server that provides tools for interacting with SQL Server databases. This server allows Large Language Models (LLMs) to query and inspect SQL Server databases through a standardized protocol.

## Features

- **Database Querying**: Execute SQL queries and retrieve results
- **Schema Inspection**: List tables, views, stored procedures, and examine table schemas
- **SQL Injection Prevention**: Built-in validation to prevent SQL injection attacks
- **Resource Caching**: Metadata caching for improved performance

## Getting Started

### Prerequisites

- .NET 10.0 SDK or later (or Docker)
- SQL Server instance (local or remote)

### Installation

#### Option 1: Docker (Recommended)

Run the server using Docker without installing .NET:

```bash
# Build the image
docker build -t tsql-mcp-server .

# Run with connection string
docker run -i tsql-mcp-server --dsn "Server=host.docker.internal;Database=mydb;User Id=sa;Password=mypassword;TrustServerCertificate=True;"

# Run in read-only mode
docker run -i tsql-mcp-server --dsn "..." --read-only

# Run with environment variable
docker run -i -e SQL_CONNECTION="Server=..." tsql-mcp-server --env-var SQL_CONNECTION
```

**Notes:**
- The `-i` flag is required for stdio transport
- Use `host.docker.internal` to connect to SQL Server on your host machine
- For WSL2 users: if `host.docker.internal` doesn't resolve, use `--network host` and `localhost` instead

#### Option 2: Install as a .NET Tool

Install globally as a .NET tool:

```bash
dotnet tool install --global tsql-mcp-server
```

To update to the latest version:

```bash
dotnet tool update --global tsql-mcp-server
```

#### Option 2: Build from Source

```bash
# Clone the repository
git clone https://github.com/Popplywop/tsql-mcp-server
cd tsql-mcp-server

# Build the project
dotnet build
```

### Running the Server

#### Using the .NET Tool

If installed as a .NET global tool:

```bash
# Using a direct connection string
tsql-mcp-server --dsn "Server=your-server;Database=your-database;User Id=your-username;Password=your-password;TrustServerCertificate=True;"

# Using an environment variable
tsql-mcp-server --env-var "SQL_CONNECTION_STRING"
```

#### Using Source Code

If building from source:

```bash
# Using a direct connection string
dotnet run --dsn "Server=your-server;Database=your-database;User Id=your-username;Password=your-password;TrustServerCertificate=True;"

# Using an environment variable
dotnet run --env-var "SQL_CONNECTION_STRING"
```

### MCP Server Configuration

To use this server with Claude or other LLMs that support the Model Context Protocol, you'll need to configure it in your MCP configuration file.

#### If Installed as a .NET Tool

```json
{
  "servers": [
    {
      "name": "SqlServerMcp",
      "command": "tsql-mcp-server",
      "args": [
        "--dsn",
        "Server=your-server;Database=your-database;User Id=your-username;Password=your-password;TrustServerCertificate=True;"
      ]
    }
  ]
}
```

#### If Built from Source

```json
{
  "servers": [
    {
      "name": "SqlServerMcp",
      "command": "path/to/tsql-mcp-server.exe",
      "args": [
        "--dsn",
        "Server=your-server;Database=your-database;User Id=your-username;Password=your-password;TrustServerCertificate=True;"
      ]
    }
  ]
}
```

Replace the connection string with your actual values. This configuration can be used with Claude's MCP integration or other LLM platforms that support the Model Context Protocol.

#### Using Docker

```json
{
  "mcpServers": {
    "sqlserver": {
      "command": "docker",
      "args": [
        "run", "-i", "--rm", "tsql-mcp-server",
        "--dsn", "Server=host.docker.internal;Database=your-database;User Id=your-username;Password=your-password;TrustServerCertificate=True;"
      ]
    }
  }
}
```

For read-only mode, add `"--read-only"` to the args array.

#### Using Environment Variables for Sensitive Information

For better security, you can use environment variables for your connection string:

```json
{
  "servers": [
    {
      "name": "SqlServerMcp",
      "command": "tsql-mcp-server",
      "args": [
        "--env-var",
        "SQL_CONNECTION_STRING"
      ]
    }
  ]
}
```

## Command Line Options

| Option | Short | Description |
|--------|-------|-------------|
| `--dsn` | `-d` | SQL Server connection string |
| `--env-var` | `-e` | Environment variable name containing the connection string |
| `--read-only` | `-r` | Run in read-only mode (blocks INSERT, UPDATE, DELETE, etc.)

## Available MCP Tools

### Query Tools

- **ExecuteQuery**: Executes a SQL query against the database and returns the results
  - Parameters:
    - `query`: The SQL query to execute
    - `commandTimeout`: Optional command timeout in seconds
    - `maxRows`: Optional maximum number of rows to return

- **ExecuteStoredProcedure**: Executes a stored procedure with optional parameters
  - Parameters:
    - `schema`: The schema name (e.g., 'dbo')
    - `procedureName`: The stored procedure name
    - `parametersJson`: Optional JSON object of parameters (e.g., `{"@param1": "value"}`)
    - `commandTimeout`: Optional command timeout in seconds
    - `maxRows`: Optional maximum number of rows to return

### Schema Tools (Token-Optimized)

Lightweight tools for exploring database structure without fetching data:

- **GetTableColumns**: Get column names and types for a table
- **GetTableRowCount**: Get row count for a table
- **ListTables**: List all tables in a schema (with optional row counts)
- **GetPrimaryKey**: Get primary key columns for a table
- **GetSampleData**: Get a small sample of data (max 20 rows)

### Database Resources

The server provides database schema information through a resource-based approach. Resources are accessed via URIs and are lazy-loaded with caching for improved performance.

| Resource URI | Description | Loading Behavior |
|--------------|-------------|------------------|
| `sqlserver://schemas/{schema_name}` | Information about a specific schema | Loaded on first request, cached for 10 minutes |
| `sqlserver://schemas/{schema_name}/tables` | List of tables in a schema | Loaded on first request, cached for 10 minutes |
| `sqlserver://schemas/{schema_name}/views` | List of views in a schema | Loaded on first request, cached for 10 minutes |
| `sqlserver://schemas/{schema_name}/procedures` | List of stored procedures in a schema | Loaded on first request, cached for 10 minutes |
| `sqlserver://schemas/{schema_name}/tables/{table_name}` | Detailed information about a specific table | Loaded on first request, cached for 10 minutes |
| `sqlserver://schemas/{schema_name}/views/{view_name}` | View columns (definition excluded) | Loaded on first request, cached for 10 minutes |
| `sqlserver://schemas/{schema_name}/views/{view_name}/definition` | View SQL definition | Loaded on request |
| `sqlserver://schemas/{schema_name}/procedures/{procedure_name}` | Procedure parameters (definition excluded) | Loaded on first request, cached for 10 minutes |
| `sqlserver://schemas/{schema_name}/procedures/{procedure_name}/definition` | Procedure SQL definition | Loaded on request |

**Note:** View and procedure definitions are excluded by default to reduce token usage. Use the `/definition` endpoint when you need the SQL source code.

Resources are automatically discovered by the MCP client and can be accessed directly without requiring specific tool calls.

## Security Considerations

- Use a SQL Server account with appropriate permissions (principle of least privilege)
- Store connection strings securely (not in source control)
- Consider using environment variables for connection strings
- Enable TLS/SSL for database connections
- SQL injection validation prevents dangerous operations and protects against common attack vectors

## Example Usage

### Querying Data

```
ExecuteQuery:
  query: "SELECT TOP 10 * FROM MyTable"
  maxRows: 100
```

### Accessing Database Resources

Resources can be accessed directly via their URIs:

```
# List all schemas
GET sqlserver://schemas

# Get information about a specific schema
GET sqlserver://schemas/dbo

# List all tables in a schema
GET sqlserver://schemas/dbo/tables

# Get detailed information about a specific table
GET sqlserver://schemas/dbo/tables/Customers
```

The resource-based approach provides a RESTful way to explore and interact with the database schema.

## License

This project is licensed under the [MIT License](LICENSE.md) - see the [LICENSE.md](LICENSE.md) file for details.

The MIT License is a permissive license that allows for reuse with minimal restrictions. It permits anyone to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the software, subject to the condition that the original copyright notice and permission notice appear in all copies.
