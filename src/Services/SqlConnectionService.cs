using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Models;

namespace Services
{
    public class SqlConnectionService
    {
        private readonly string _connectionString;
        private readonly IConfiguration _configuration;
        private readonly DatabaseInfo _databaseInfo;

        public SqlConnectionService(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration["ConnectionStrings:SqlServer"]
                ?? throw new InvalidOperationException("SQL Server connection string not found in configuration");
            
            // Initialize the connection string builder for better parsing and validation
            try
            {
                _connectionBuilder = new SqlConnectionStringBuilder(_connectionString);
                
                // Ensure minimum required properties are set
                if (string.IsNullOrEmpty(_connectionBuilder.DataSource))
                    throw new InvalidOperationException("Database server name (Data Source) is missing from connection string");
                
                if (string.IsNullOrEmpty(_connectionBuilder.InitialCatalog))
                    throw new InvalidOperationException("Database name (Initial Catalog) is missing from connection string");
                
                // Configure connection pooling if not explicitly set
                if (!_connectionString.Contains("Min Pool Size"))
                {
                    _connectionBuilder.MinPoolSize = 5;
                }
                
                if (!_connectionString.Contains("Max Pool Size"))
                {
                    _connectionBuilder.MaxPoolSize = 100;
                }
                
                if (!_connectionString.Contains("Connection Timeout"))
                {
                    _connectionBuilder.ConnectTimeout = 30;
                }
                
                if (!_connectionString.Contains("Connection Lifetime"))
                {
                    _connectionBuilder.LoadBalanceTimeout = 60;
                }
            }
            catch (ArgumentException ex)
            {
                throw new InvalidOperationException("Invalid database connection string format", ex);
            }
            
            _databaseInfo = new DatabaseInfo
            {
                // Use the optimized connection string with pool settings
                ConnectionString = _connectionBuilder.ConnectionString,
                // Extract information directly from the connection builder
                ServerName = _connectionBuilder.DataSource,
                DatabaseName = _connectionBuilder.InitialCatalog,
                UserName = _connectionBuilder.UserID,
                IsConnected = false
            };
        }

        private readonly SqlConnectionStringBuilder _connectionBuilder;

        /// <summary>
        /// Creates a new SQL connection using the configured connection string
        /// </summary>
        /// <param name="commandTimeout">Optional command timeout in seconds</param>
        /// <returns>A new SqlConnection instance</returns>
        public SqlConnection CreateConnection(int? commandTimeout = null)
        {
            try
            {
                // Create a new connection using the cached connection builder
                // This ensures all connection parameters are properly parsed and validated
                var connection = new SqlConnection(_connectionBuilder.ConnectionString);
                
                // Set the default command timeout if specified
                if (commandTimeout.HasValue)
                {
                    connection.StatisticsEnabled = true;
                    
                    // We need to set the default command timeout via the connection string
                    // since SqlConnection doesn't expose a property for it
                    var builder = new SqlConnectionStringBuilder(connection.ConnectionString)
                    {
                        CommandTimeout = commandTimeout.Value
                    };
                    connection.ConnectionString = builder.ConnectionString;
                }
                
                return connection;
            }
            catch (ArgumentException ex)
            {
                // Handle invalid connection string parameters
                throw new InvalidOperationException("Invalid database connection parameters", ex);
            }
        }

        public async Task TestConnectionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var connection = CreateConnection();
                await connection.OpenAsync(cancellationToken);
                _databaseInfo.IsConnected = true;
                _databaseInfo.LastConnected = DateTime.Now;
            }
            catch (Exception ex)
            {
                _databaseInfo.IsConnected = false;
                throw new InvalidOperationException($"Failed to connect to database. The application cannot start.", ex);
            }
        }

        public DatabaseInfo GetDatabaseInfo()
        {
            return _databaseInfo;
        }
    }
}