using Moq;
using Services;
using Models;
using Tools;
using FluentAssertions;
using System.Text.Json;

namespace tsql_mcp_server_tests.Tools
{
    public class SchemaToolTests
    {
        private readonly Mock<IQueryService> _queryServiceMock;
        private readonly SchemaTool _tool;

        public SchemaToolTests()
        {
            _queryServiceMock = new Mock<IQueryService>();
            _tool = new SchemaTool(_queryServiceMock.Object);
        }

        #region GetTableColumns Tests

        [Fact]
        public async Task GetTableColumns_WithValidInput_ReturnsJsonResult()
        {
            // Arrange
            var expectedResult = new QueryResult
            {
                IsSuccess = true,
                Rows = [
                    new Dictionary<string, object> { ["Name"] = "Id", ["Type"] = "int", ["MaxLength"] = DBNull.Value, ["Nullable"] = "NO" },
                    new Dictionary<string, object> { ["Name"] = "Email", ["Type"] = "varchar", ["MaxLength"] = 255, ["Nullable"] = "YES" }
                ]
            };

            _queryServiceMock
                .Setup(x => x.ExecuteQueryAsync(
                    It.IsAny<string>(),
                    10,
                    500,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _tool.GetTableColumns("dbo", "Users");

            // Assert
            result.Should().Contain("\"Schema\":\"dbo\"");
            result.Should().Contain("\"Table\":\"Users\"");
            result.Should().Contain("\"Columns\":");
        }

        [Fact]
        public async Task GetTableColumns_WhenServiceReturnsError_ReturnsErrorMessage()
        {
            // Arrange
            var errorResult = new QueryResult
            {
                IsSuccess = false,
                Message = "Table not found"
            };

            _queryServiceMock
                .Setup(x => x.ExecuteQueryAsync(
                    It.IsAny<string>(),
                    It.IsAny<int?>(),
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(errorResult);

            // Act
            var result = await _tool.GetTableColumns("dbo", "NonExistent");

            // Assert
            result.Should().StartWith("Error:");
            result.Should().Contain("Table not found");
        }

        [Theory]
        [InlineData("dbo", "Users")]
        [InlineData("Sales", "Orders")]
        [InlineData("HumanResources", "Employees")]
        public async Task GetTableColumns_PassesSchemaAndTableInQuery(string schema, string table)
        {
            // Arrange
            string? capturedQuery = null;
            _queryServiceMock
                .Setup(x => x.ExecuteQueryAsync(
                    It.IsAny<string>(),
                    It.IsAny<int?>(),
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, int?, int?, CancellationToken>((q, t, r, c) => capturedQuery = q)
                .ReturnsAsync(new QueryResult { IsSuccess = true, Rows = [] });

            // Act
            await _tool.GetTableColumns(schema, table);

            // Assert
            capturedQuery.Should().Contain($"TABLE_SCHEMA = '{schema}'");
            capturedQuery.Should().Contain($"TABLE_NAME = '{table}'");
        }

        #endregion

        #region GetTableRowCount Tests

        [Fact]
        public async Task GetTableRowCount_WithValidInput_ReturnsRowCount()
        {
            // Arrange
            var expectedResult = new QueryResult
            {
                IsSuccess = true,
                Rows = [new Dictionary<string, object> { ["Row_Count"] = 1234 }]
            };

            _queryServiceMock
                .Setup(x => x.ExecuteQueryAsync(
                    It.IsAny<string>(),
                    30,
                    1,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _tool.GetTableRowCount("dbo", "Users");

            // Assert
            result.Should().Contain("\"RowCount\":1234");
            result.Should().Contain("\"Schema\":\"dbo\"");
            result.Should().Contain("\"Table\":\"Users\"");
        }

        [Fact]
        public async Task GetTableRowCount_WhenServiceReturnsError_ReturnsErrorMessage()
        {
            // Arrange
            _queryServiceMock
                .Setup(x => x.ExecuteQueryAsync(
                    It.IsAny<string>(),
                    It.IsAny<int?>(),
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new QueryResult { IsSuccess = false, Message = "Permission denied" });

            // Act
            var result = await _tool.GetTableRowCount("dbo", "SecretTable");

            // Assert
            result.Should().StartWith("Error:");
        }

        #endregion

        #region ListTables Tests

        [Fact]
        public async Task ListTables_WithoutRowCounts_ReturnsTableNames()
        {
            // Arrange
            var expectedResult = new QueryResult
            {
                IsSuccess = true,
                Rows = [
                    new Dictionary<string, object> { ["TableName"] = "Users" },
                    new Dictionary<string, object> { ["TableName"] = "Orders" },
                    new Dictionary<string, object> { ["TableName"] = "Products" }
                ]
            };

            string? capturedQuery = null;
            _queryServiceMock
                .Setup(x => x.ExecuteQueryAsync(
                    It.IsAny<string>(),
                    It.IsAny<int?>(),
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, int?, int?, CancellationToken>((q, t, r, c) => capturedQuery = q)
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _tool.ListTables("dbo", includeRowCounts: false);

            // Assert
            result.Should().Contain("\"Schema\":\"dbo\"");
            result.Should().Contain("\"Tables\":");
            result.Should().Contain("Users");
            result.Should().Contain("Orders");
            capturedQuery.Should().NotContain("sys.partitions"); // Row count join not included
        }

        [Fact]
        public async Task ListTables_WithRowCounts_IncludesRowCountsInQuery()
        {
            // Arrange
            string? capturedQuery = null;
            _queryServiceMock
                .Setup(x => x.ExecuteQueryAsync(
                    It.IsAny<string>(),
                    It.IsAny<int?>(),
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, int?, int?, CancellationToken>((q, t, r, c) => capturedQuery = q)
                .ReturnsAsync(new QueryResult { IsSuccess = true, Rows = [] });

            // Act
            await _tool.ListTables("dbo", includeRowCounts: true);

            // Assert
            capturedQuery.Should().Contain("sys.partitions");
            capturedQuery.Should().Contain("Row_Count");
        }

        #endregion

        #region GetPrimaryKey Tests

        [Fact]
        public async Task GetPrimaryKey_WithSingleColumnPK_ReturnsColumnName()
        {
            // Arrange
            var expectedResult = new QueryResult
            {
                IsSuccess = true,
                Rows = [new Dictionary<string, object> { ["ColumnName"] = "Id" }]
            };

            _queryServiceMock
                .Setup(x => x.ExecuteQueryAsync(
                    It.IsAny<string>(),
                    10,
                    50,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _tool.GetPrimaryKey("dbo", "Users");

            // Assert
            result.Should().Contain("\"PrimaryKeyColumns\":[\"Id\"]");
        }

        [Fact]
        public async Task GetPrimaryKey_WithCompositeKey_ReturnsAllColumns()
        {
            // Arrange
            var expectedResult = new QueryResult
            {
                IsSuccess = true,
                Rows = [
                    new Dictionary<string, object> { ["ColumnName"] = "OrderId" },
                    new Dictionary<string, object> { ["ColumnName"] = "ProductId" }
                ]
            };

            _queryServiceMock
                .Setup(x => x.ExecuteQueryAsync(
                    It.IsAny<string>(),
                    It.IsAny<int?>(),
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _tool.GetPrimaryKey("dbo", "OrderDetails");

            // Assert
            result.Should().Contain("OrderId");
            result.Should().Contain("ProductId");
        }

        [Fact]
        public async Task GetPrimaryKey_WhenNoPK_ReturnsEmptyArray()
        {
            // Arrange
            var expectedResult = new QueryResult
            {
                IsSuccess = true,
                Rows = []
            };

            _queryServiceMock
                .Setup(x => x.ExecuteQueryAsync(
                    It.IsAny<string>(),
                    It.IsAny<int?>(),
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _tool.GetPrimaryKey("dbo", "HeapTable");

            // Assert
            result.Should().Contain("\"PrimaryKeyColumns\":[]");
        }

        #endregion

        #region GetSampleData Tests

        [Fact]
        public async Task GetSampleData_WithDefaultSize_Returns5Rows()
        {
            // Arrange
            string? capturedQuery = null;
            _queryServiceMock
                .Setup(x => x.ExecuteQueryAsync(
                    It.IsAny<string>(),
                    10,
                    5,
                    It.IsAny<CancellationToken>()))
                .Callback<string, int?, int?, CancellationToken>((q, t, r, c) => capturedQuery = q)
                .ReturnsAsync(new QueryResult { IsSuccess = true, Rows = [], Columns = [] });

            // Act
            await _tool.GetSampleData("dbo", "Users");

            // Assert
            capturedQuery.Should().Contain("TOP 5");
        }

        [Fact]
        public async Task GetSampleData_WithCustomSize_UsesSpecifiedSize()
        {
            // Arrange
            string? capturedQuery = null;
            _queryServiceMock
                .Setup(x => x.ExecuteQueryAsync(
                    It.IsAny<string>(),
                    10,
                    10,
                    It.IsAny<CancellationToken>()))
                .Callback<string, int?, int?, CancellationToken>((q, t, r, c) => capturedQuery = q)
                .ReturnsAsync(new QueryResult { IsSuccess = true, Rows = [], Columns = [] });

            // Act
            await _tool.GetSampleData("dbo", "Users", sampleSize: 10);

            // Assert
            capturedQuery.Should().Contain("TOP 10");
        }

        [Theory]
        [InlineData(0, 1)]   // Less than min clamped to 1
        [InlineData(-5, 1)]  // Negative clamped to 1
        [InlineData(25, 20)] // More than max clamped to 20
        [InlineData(100, 20)] // Large value clamped to 20
        public async Task GetSampleData_ClampsSize_BetweenOneAndTwenty(int requestedSize, int expectedSize)
        {
            // Arrange
            string? capturedQuery = null;
            _queryServiceMock
                .Setup(x => x.ExecuteQueryAsync(
                    It.IsAny<string>(),
                    It.IsAny<int?>(),
                    expectedSize,
                    It.IsAny<CancellationToken>()))
                .Callback<string, int?, int?, CancellationToken>((q, t, r, c) => capturedQuery = q)
                .ReturnsAsync(new QueryResult { IsSuccess = true, Rows = [], Columns = [] });

            // Act
            await _tool.GetSampleData("dbo", "Users", sampleSize: requestedSize);

            // Assert
            capturedQuery.Should().Contain($"TOP {expectedSize}");
        }

        [Fact]
        public async Task GetSampleData_ReturnsColumnsAndRows()
        {
            // Arrange
            var expectedResult = new QueryResult
            {
                IsSuccess = true,
                Columns = ["Id", "Name", "Email"],
                Rows = [
                    new Dictionary<string, object> { ["Id"] = 1, ["Name"] = "Alice", ["Email"] = "alice@test.com" },
                    new Dictionary<string, object> { ["Id"] = 2, ["Name"] = "Bob", ["Email"] = "bob@test.com" }
                ],
                RowCount = 2
            };

            _queryServiceMock
                .Setup(x => x.ExecuteQueryAsync(
                    It.IsAny<string>(),
                    It.IsAny<int?>(),
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _tool.GetSampleData("dbo", "Users");

            // Assert
            result.Should().Contain("\"Columns\":");
            result.Should().Contain("\"Rows\":");
            result.Should().Contain("\"SampleSize\":2");
        }

        #endregion

        #region SQL Escaping Tests (via Query Verification)

        [Theory]
        [InlineData("normal_schema", "normal_table")]
        [InlineData("Schema123", "Table456")]
        public async Task AllMethods_WithNormalInput_GeneratesValidQueries(string schema, string table)
        {
            // Arrange
            _queryServiceMock
                .Setup(x => x.ExecuteQueryAsync(
                    It.IsAny<string>(),
                    It.IsAny<int?>(),
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new QueryResult { IsSuccess = true, Rows = [] });

            // Act & Assert - Should not throw
            await _tool.GetTableColumns(schema, table);
            await _tool.GetTableRowCount(schema, table);
            await _tool.GetPrimaryKey(schema, table);
            await _tool.GetSampleData(schema, table);
            await _tool.ListTables(schema);

            // Verify service was called for each method
            _queryServiceMock.Verify(x => x.ExecuteQueryAsync(
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()), Times.Exactly(5));
        }

        [Theory]
        [InlineData("schema'with'quotes", "schema''with''quotes")]
        [InlineData("schema[with]brackets", "schemawithbrackets")]
        [InlineData("schema;with;semicolons", "schemawithsemicolons")]
        public async Task GetTableColumns_EscapesDangerousCharacters(string input, string expectedEscaped)
        {
            // Arrange
            string? capturedQuery = null;
            _queryServiceMock
                .Setup(x => x.ExecuteQueryAsync(
                    It.IsAny<string>(),
                    It.IsAny<int?>(),
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, int?, int?, CancellationToken>((q, t, r, c) => capturedQuery = q)
                .ReturnsAsync(new QueryResult { IsSuccess = true, Rows = [] });

            // Act
            await _tool.GetTableColumns(input, "TestTable");

            // Assert - The escaped version should appear in the query
            capturedQuery.Should().Contain($"TABLE_SCHEMA = '{expectedEscaped}'");
        }

        #endregion

        #region Compact JSON Output Tests

        [Fact]
        public async Task GetTableColumns_ReturnsCompactJson()
        {
            // Arrange
            var result = new QueryResult
            {
                IsSuccess = true,
                Rows = [new Dictionary<string, object> { ["Name"] = "Id", ["Type"] = "int" }]
            };

            _queryServiceMock
                .Setup(x => x.ExecuteQueryAsync(
                    It.Is<string>(q => q.Contains("INFORMATION_SCHEMA.COLUMNS")),
                    It.IsAny<int?>(),
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(result);

            // Act
            var jsonResult = await _tool.GetTableColumns("dbo", "Users");

            // Assert - Compact JSON should not have newlines
            jsonResult.Should().NotContain("\n");
        }

        [Fact]
        public async Task GetTableRowCount_ReturnsCompactJson()
        {
            // Arrange
            var result = new QueryResult
            {
                IsSuccess = true,
                Rows = [new Dictionary<string, object> { ["Row_Count"] = 100 }]
            };

            _queryServiceMock
                .Setup(x => x.ExecuteQueryAsync(
                    It.Is<string>(q => q.Contains("COUNT(*)")),
                    It.IsAny<int?>(),
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(result);

            // Act
            var jsonResult = await _tool.GetTableRowCount("dbo", "Users");

            // Assert
            jsonResult.Should().NotContain("\n");
        }

        [Fact]
        public async Task GetPrimaryKey_ReturnsCompactJson()
        {
            // Arrange
            var result = new QueryResult
            {
                IsSuccess = true,
                Rows = [new Dictionary<string, object> { ["ColumnName"] = "Id" }]
            };

            _queryServiceMock
                .Setup(x => x.ExecuteQueryAsync(
                    It.Is<string>(q => q.Contains("IsPrimaryKey")),
                    It.IsAny<int?>(),
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(result);

            // Act
            var jsonResult = await _tool.GetPrimaryKey("dbo", "Users");

            // Assert
            jsonResult.Should().NotContain("\n");
        }

        [Fact]
        public async Task GetSampleData_ReturnsCompactJson()
        {
            // Arrange
            var result = new QueryResult
            {
                IsSuccess = true,
                Columns = ["Id"],
                Rows = [new Dictionary<string, object> { ["Id"] = 1 }],
                RowCount = 1
            };

            _queryServiceMock
                .Setup(x => x.ExecuteQueryAsync(
                    It.Is<string>(q => q.Contains("TOP")),
                    It.IsAny<int?>(),
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(result);

            // Act
            var jsonResult = await _tool.GetSampleData("dbo", "Users");

            // Assert
            jsonResult.Should().NotContain("\n");
        }

        [Fact]
        public async Task ListTables_ReturnsCompactJson()
        {
            // Arrange
            var result = new QueryResult
            {
                IsSuccess = true,
                Rows = [new Dictionary<string, object> { ["TableName"] = "Users" }]
            };

            _queryServiceMock
                .Setup(x => x.ExecuteQueryAsync(
                    It.Is<string>(q => q.Contains("INFORMATION_SCHEMA.TABLES")),
                    It.IsAny<int?>(),
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(result);

            // Act
            var jsonResult = await _tool.ListTables("dbo");

            // Assert
            jsonResult.Should().NotContain("\n");
        }

        #endregion
    }
}
