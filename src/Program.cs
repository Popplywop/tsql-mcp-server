using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Services;
using Handlers;
using CommandLine;

namespace tsql_mcp_server;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var result = Parser.Default.ParseArguments<Options>(args);

        return await result.MapResult(
            async options => await RunAsync(options, args),
            _ => Task.FromResult(1)
        );
    }

    private static async Task<int> RunAsync(Options options, string[] args)
    {
        try
        {
            // Determine the connection string from the provided options
            string? connectionString = null;

            // Check environment variable first if specified
            if (!string.IsNullOrEmpty(options.EnvVar))
            {
                connectionString = Environment.GetEnvironmentVariable(options.EnvVar);
                if (string.IsNullOrEmpty(connectionString))
                {
                    Console.Error.WriteLine($"Error: Environment variable '{options.EnvVar}' not found or empty");
                    return 1;
                }
            }
            // Otherwise use the DSN option
            else if (!string.IsNullOrEmpty(options.Dsn))
            {
                connectionString = options.Dsn;
            }

            // If no connection string was provided, show error and exit
            if (string.IsNullOrEmpty(connectionString))
            {
                Console.Error.WriteLine("Error: No connection string provided. Use --dsn or --env-var option.");
                return 1;
            }

            Console.WriteLine(connectionString);

            // Create the host builder
            var builder = Host.CreateApplicationBuilder(args);

            // Add the connection string to configuration
            builder.Configuration["ConnectionStrings:SqlServer"] = connectionString;

            // Configure logging
            builder.Logging.AddConsole(consoleLogOptions =>
            {
                // Configure all logs to go to stderr
                consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
            });
            builder.Logging.SetMinimumLevel(LogLevel.Information);

            // Configure default database settings
            builder.Configuration["Database:DefaultCommandTimeout"] = "30";
            builder.Configuration["Database:DefaultMaxRows"] = "1000";
            builder.Configuration["Database:ReadOnly"] = options.ReadOnly.ToString();

            // Register services in the correct order of dependency
            builder.Services
                .AddSingleton<SqlConnectionService>()
                .AddSingleton<SqlConnectionFactory>()
                .AddSingleton<SqlInjectionValidationService>()
                .AddSingleton<DatabaseMetadataCache>()
                .AddSingleton<QueryService>()
                .AddSingleton<IQueryService>(sp => sp.GetRequiredService<QueryService>())
                .AddSingleton<DatabaseResourceHandler>();

            // Build the service provider to get resource handler
            var serviceProvider = builder.Services.BuildServiceProvider();
            var resourceHandler = serviceProvider.GetRequiredService<DatabaseResourceHandler>();

            // Configure MCP server with stdio transport
            builder.Services
                .AddMcpServer()
                .WithStdioServerTransport()
                .WithToolsFromAssembly()
                .WithListResourcesHandler(resourceHandler.HandleListResources)
                .WithReadResourceHandler(resourceHandler.HandleReadResources);

            var host = builder.Build();

            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var connectionFactory = host.Services.GetRequiredService<SqlConnectionFactory>();
            var connectionService = connectionFactory.ConnectionService;

            logger.LogInformation("Starting SQL Server MCP Server...");
            if (options.ReadOnly)
            {
                logger.LogInformation("Running in READ-ONLY mode - write operations are disabled");
            }

            try
            {
                // Test database connection
                await connectionService.TestConnectionAsync();

                var dbInfo = connectionService.GetDatabaseInfo();
                logger.LogInformation("Connected to database {Database} on server {Server}",
                    dbInfo.DatabaseName, dbInfo.ServerName);

                await host.RunAsync();
                return 0;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to start SQL Server MCP Server");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unhandled error: {ex.Message}");
            return 1;
        }
    }
}
