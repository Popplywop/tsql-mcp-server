using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Services;
using FluentAssertions;

namespace tsql_mcp_server_tests.Services
{
    public class QueryServiceReadOnlyTests
    {
        private readonly Mock<ILogger<QueryService>> _queryServiceLoggerMock;
        private readonly Mock<ILogger<SqlConnectionFactory>> _connectionFactoryLoggerMock;
        private readonly Mock<ILogger<SqlInjectionValidationService>> _validationLoggerMock;
        private readonly SqlInjectionValidationService _sqlInjectionValidator;

        public QueryServiceReadOnlyTests()
        {
            _queryServiceLoggerMock = new Mock<ILogger<QueryService>>();
            _connectionFactoryLoggerMock = new Mock<ILogger<SqlConnectionFactory>>();
            _validationLoggerMock = new Mock<ILogger<SqlInjectionValidationService>>();
            _sqlInjectionValidator = new SqlInjectionValidationService(_validationLoggerMock.Object);
        }

        private QueryService CreateQueryService(bool readOnlyMode)
        {
            var configBuilder = new ConfigurationBuilder();
            var configData = new Dictionary<string, string?>
            {
                ["ConnectionStrings:SqlServer"] = "Server=fake;Database=fake;User Id=fake;Password=fake;TrustServerCertificate=True;",
                ["Database:ReadOnly"] = readOnlyMode.ToString(),
                ["Database:DefaultCommandTimeout"] = "30",
                ["Database:DefaultMaxRows"] = "1000"
            };
            var configuration = configBuilder.AddInMemoryCollection(configData).Build();

            var connectionService = new SqlConnectionService(configuration);
            var connectionFactory = new SqlConnectionFactory(connectionService, _connectionFactoryLoggerMock.Object);

            return new QueryService(connectionFactory, _queryServiceLoggerMock.Object, _sqlInjectionValidator, configuration);
        }

        [Theory]
        [InlineData("INSERT INTO Users (Name) VALUES ('test')")]
        [InlineData("insert into Users (Name) values ('test')")]
        [InlineData("  INSERT INTO Users (Name) VALUES ('test')")]
        public async Task ExecuteQueryAsync_InReadOnlyMode_BlocksInsertQueries(string query)
        {
            // Arrange
            var service = CreateQueryService(readOnlyMode: true);

            // Act
            var result = await service.ExecuteQueryAsync(query);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Message.Should().Contain("read-only mode");
        }

        [Theory]
        [InlineData("UPDATE Users SET Name = 'test' WHERE Id = 1")]
        [InlineData("update Users set Name = 'test' where Id = 1")]
        [InlineData("  UPDATE Users SET Name = 'test'")]
        public async Task ExecuteQueryAsync_InReadOnlyMode_BlocksUpdateQueries(string query)
        {
            // Arrange
            var service = CreateQueryService(readOnlyMode: true);

            // Act
            var result = await service.ExecuteQueryAsync(query);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Message.Should().Contain("read-only mode");
        }

        [Theory]
        [InlineData("DELETE FROM Users WHERE Id = 1")]
        [InlineData("delete from Users where Id = 1")]
        [InlineData("  DELETE FROM Users")]
        public async Task ExecuteQueryAsync_InReadOnlyMode_BlocksDeleteQueries(string query)
        {
            // Arrange
            var service = CreateQueryService(readOnlyMode: true);

            // Act
            var result = await service.ExecuteQueryAsync(query);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Message.Should().Contain("read-only mode");
        }

        [Theory]
        [InlineData("DROP TABLE Users")]
        [InlineData("drop table Users")]
        [InlineData("  DROP DATABASE TestDb")]
        public async Task ExecuteQueryAsync_InReadOnlyMode_BlocksDropQueries(string query)
        {
            // Arrange
            var service = CreateQueryService(readOnlyMode: true);

            // Act
            var result = await service.ExecuteQueryAsync(query);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Message.Should().Contain("read-only mode");
        }

        [Theory]
        [InlineData("TRUNCATE TABLE Users")]
        [InlineData("truncate table Users")]
        public async Task ExecuteQueryAsync_InReadOnlyMode_BlocksTruncateQueries(string query)
        {
            // Arrange
            var service = CreateQueryService(readOnlyMode: true);

            // Act
            var result = await service.ExecuteQueryAsync(query);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Message.Should().Contain("read-only mode");
        }

        [Theory]
        [InlineData("ALTER TABLE Users ADD Column Email VARCHAR(100)")]
        [InlineData("alter table Users drop column Name")]
        public async Task ExecuteQueryAsync_InReadOnlyMode_BlocksAlterQueries(string query)
        {
            // Arrange
            var service = CreateQueryService(readOnlyMode: true);

            // Act
            var result = await service.ExecuteQueryAsync(query);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Message.Should().Contain("read-only mode");
        }

        [Theory]
        [InlineData("CREATE TABLE NewTable (Id INT)")]
        [InlineData("create table NewTable (Id INT)")]
        public async Task ExecuteQueryAsync_InReadOnlyMode_BlocksCreateQueries(string query)
        {
            // Arrange
            var service = CreateQueryService(readOnlyMode: true);

            // Act
            var result = await service.ExecuteQueryAsync(query);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Message.Should().Contain("read-only mode");
        }

        [Theory]
        [InlineData("MERGE INTO Users USING Source ON ...")]
        [InlineData("merge into Users using Source on ...")]
        public async Task ExecuteQueryAsync_InReadOnlyMode_BlocksMergeQueries(string query)
        {
            // Arrange
            var service = CreateQueryService(readOnlyMode: true);

            // Act
            var result = await service.ExecuteQueryAsync(query);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Message.Should().Contain("read-only mode");
        }

        [Fact]
        public async Task ExecuteQueryAsync_InReadOnlyMode_ErrorMessageIsDescriptive()
        {
            // Arrange
            var service = CreateQueryService(readOnlyMode: true);

            // Act
            var result = await service.ExecuteQueryAsync("INSERT INTO Users (Name) VALUES ('test')");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Message.Should().Be("Write operations (INSERT, UPDATE, DELETE, etc.) are not allowed in read-only mode");
        }

        [Theory]
        [InlineData("dbo")]
        [InlineData("MySchema")]
        [InlineData("schema_name")]
        [InlineData("Schema123")]
        public async Task ExecuteStoredProcedureAsync_WithValidSchemaName_PassesValidation(string schema)
        {
            // Arrange
            var service = CreateQueryService(readOnlyMode: false);

            // Act
            // This will fail at connection level but should pass schema validation
            var result = await service.ExecuteStoredProcedureAsync(schema, "ValidProc", null);

            // Assert
            // If it passed schema validation, the error will be about connection, not schema
            if (!result.IsSuccess)
            {
                result.Message.Should().NotContain("Invalid schema name");
            }
        }

        [Theory]
        [InlineData("dbo.something")]
        [InlineData("schema-name")]
        [InlineData("schema name")]
        [InlineData("schema;drop")]
        [InlineData("schema'")]
        public async Task ExecuteStoredProcedureAsync_WithInvalidSchemaName_ReturnsValidationError(string schema)
        {
            // Arrange
            var service = CreateQueryService(readOnlyMode: false);

            // Act
            var result = await service.ExecuteStoredProcedureAsync(schema, "ValidProc", null);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Message.Should().Contain("Invalid schema name");
        }

        [Theory]
        [InlineData("GetUsers")]
        [InlineData("sp_GetData")]
        [InlineData("usp_ProcessOrder")]
        [InlineData("Proc123")]
        public async Task ExecuteStoredProcedureAsync_WithValidProcedureName_PassesValidation(string procedureName)
        {
            // Arrange
            var service = CreateQueryService(readOnlyMode: false);

            // Act
            var result = await service.ExecuteStoredProcedureAsync("dbo", procedureName, null);

            // Assert
            // If it passed procedure validation, the error will be about connection, not procedure name
            if (!result.IsSuccess)
            {
                result.Message.Should().NotContain("Invalid procedure name");
            }
        }

        [Theory]
        [InlineData("proc.name")]
        [InlineData("proc-name")]
        [InlineData("proc name")]
        [InlineData("proc;drop")]
        [InlineData("proc'")]
        public async Task ExecuteStoredProcedureAsync_WithInvalidProcedureName_ReturnsValidationError(string procedureName)
        {
            // Arrange
            var service = CreateQueryService(readOnlyMode: false);

            // Act
            var result = await service.ExecuteStoredProcedureAsync("dbo", procedureName, null);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Message.Should().Contain("Invalid procedure name");
        }
    }
}
