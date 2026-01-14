using Moq;
using Services;
using Models;
using Tools;
using FluentAssertions;
using System.Text.Json;

namespace tsql_mcp_server_tests.Tools
{
    public class StoredProcedureToolTests
    {
        private readonly Mock<IQueryService> _queryServiceMock;
        private readonly StoredProcedureTool _tool;

        public StoredProcedureToolTests()
        {
            _queryServiceMock = new Mock<IQueryService>();
            _tool = new StoredProcedureTool(_queryServiceMock.Object);
        }

        [Fact]
        public async Task ExecuteStoredProcedure_WithNoParameters_CallsServiceCorrectly()
        {
            // Arrange
            var expectedResult = new QueryResult
            {
                IsSuccess = true,
                Columns = ["Id", "Name"],
                Rows = [new Dictionary<string, object> { ["Id"] = 1, ["Name"] = "Test" }],
                RowCount = 1
            };

            _queryServiceMock
                .Setup(x => x.ExecuteStoredProcedureAsync(
                    "dbo",
                    "GetUsers",
                    null,
                    null,
                    null,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _tool.ExecuteStoredProcedure("dbo", "GetUsers");

            // Assert
            _queryServiceMock.Verify(x => x.ExecuteStoredProcedureAsync(
                "dbo",
                "GetUsers",
                null,
                null,
                null,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ExecuteStoredProcedure_WithValidJsonParameters_ParsesCorrectly()
        {
            // Arrange
            var expectedResult = new QueryResult
            {
                IsSuccess = true,
                Columns = ["Id", "Name"],
                Rows = [new Dictionary<string, object> { ["Id"] = 1, ["Name"] = "Test" }],
                RowCount = 1
            };

            Dictionary<string, object?>? capturedParams = null;
            _queryServiceMock
                .Setup(x => x.ExecuteStoredProcedureAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, object?>?>(),
                    It.IsAny<int?>(),
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, string, Dictionary<string, object?>?, int?, int?, CancellationToken>(
                    (schema, proc, parms, timeout, rows, ct) => capturedParams = parms)
                .ReturnsAsync(expectedResult);

            var parametersJson = "{\"@userId\": 123, \"@name\": \"John\"}";

            // Act
            var result = await _tool.ExecuteStoredProcedure("dbo", "GetUser", parametersJson);

            // Assert
            capturedParams.Should().NotBeNull();
            capturedParams.Should().ContainKey("@userId");
            capturedParams.Should().ContainKey("@name");
        }

        [Fact]
        public async Task ExecuteStoredProcedure_WithInvalidJson_ReturnsErrorMessage()
        {
            // Arrange
            var invalidJson = "{ invalid json }";

            // Act
            var result = await _tool.ExecuteStoredProcedure("dbo", "GetUser", invalidJson);

            // Assert
            result.Should().StartWith("Error parsing parameters JSON:");
            _queryServiceMock.Verify(x => x.ExecuteStoredProcedureAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object?>?>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteStoredProcedure_WithEmptyParameters_PassesNullToService()
        {
            // Arrange
            var expectedResult = new QueryResult { IsSuccess = true, Message = "Success" };

            _queryServiceMock
                .Setup(x => x.ExecuteStoredProcedureAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, object?>?>(),
                    It.IsAny<int?>(),
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            await _tool.ExecuteStoredProcedure("dbo", "GetUsers", null);
            await _tool.ExecuteStoredProcedure("dbo", "GetUsers", "");
            await _tool.ExecuteStoredProcedure("dbo", "GetUsers", "   ");

            // Assert - all should pass null parameters
            _queryServiceMock.Verify(x => x.ExecuteStoredProcedureAsync(
                "dbo",
                "GetUsers",
                null,
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()), Times.Exactly(3));
        }

        [Fact]
        public async Task ExecuteStoredProcedure_WithTimeout_PassesTimeoutToService()
        {
            // Arrange
            var expectedResult = new QueryResult { IsSuccess = true, Message = "Success" };

            _queryServiceMock
                .Setup(x => x.ExecuteStoredProcedureAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, object?>?>(),
                    60,
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            await _tool.ExecuteStoredProcedure("dbo", "LongProc", null, commandTimeout: 60);

            // Assert
            _queryServiceMock.Verify(x => x.ExecuteStoredProcedureAsync(
                "dbo",
                "LongProc",
                null,
                60,
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ExecuteStoredProcedure_WithMaxRows_PassesMaxRowsToService()
        {
            // Arrange
            var expectedResult = new QueryResult { IsSuccess = true, Message = "Success" };

            _queryServiceMock
                .Setup(x => x.ExecuteStoredProcedureAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, object?>?>(),
                    It.IsAny<int?>(),
                    50,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            await _tool.ExecuteStoredProcedure("dbo", "GetUsers", null, maxRows: 50);

            // Assert
            _queryServiceMock.Verify(x => x.ExecuteStoredProcedureAsync(
                "dbo",
                "GetUsers",
                null,
                It.IsAny<int?>(),
                50,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ExecuteStoredProcedure_WhenServiceReturnsError_ReturnsErrorMessage()
        {
            // Arrange
            var errorResult = new QueryResult
            {
                IsSuccess = false,
                Message = "Procedure not found",
                ErrorCode = 2812
            };

            _queryServiceMock
                .Setup(x => x.ExecuteStoredProcedureAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, object?>?>(),
                    It.IsAny<int?>(),
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(errorResult);

            // Act
            var result = await _tool.ExecuteStoredProcedure("dbo", "NonExistentProc");

            // Assert
            result.Should().Contain("Error executing stored procedure");
            result.Should().Contain("Procedure not found");
            result.Should().Contain("Error code: 2812");
        }

        [Fact]
        public async Task ExecuteStoredProcedure_WhenServiceReturnsErrorWithoutCode_ReturnsErrorWithoutCode()
        {
            // Arrange
            var errorResult = new QueryResult
            {
                IsSuccess = false,
                Message = "Connection failed"
            };

            _queryServiceMock
                .Setup(x => x.ExecuteStoredProcedureAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, object?>?>(),
                    It.IsAny<int?>(),
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(errorResult);

            // Act
            var result = await _tool.ExecuteStoredProcedure("dbo", "SomeProc");

            // Assert
            result.Should().Contain("Connection failed");
            result.Should().NotContain("Error code:");
        }

        [Fact]
        public async Task ExecuteStoredProcedure_WithResults_ReturnsJsonResults()
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
                .Setup(x => x.ExecuteStoredProcedureAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, object?>?>(),
                    It.IsAny<int?>(),
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _tool.ExecuteStoredProcedure("dbo", "GetUsers");

            // Assert
            result.Should().Contain("\"Columns\":");
            result.Should().Contain("\"Rows\":");
            result.Should().Contain("Alice");
            result.Should().Contain("Bob");
        }

        [Fact]
        public async Task ExecuteStoredProcedure_WithCompactFalse_ReturnsPrettyPrintedJson()
        {
            // Arrange
            var expectedResult = new QueryResult
            {
                IsSuccess = true,
                Columns = ["Id"],
                Rows = [new Dictionary<string, object> { ["Id"] = 1 }],
                RowCount = 1
            };

            _queryServiceMock
                .Setup(x => x.ExecuteStoredProcedureAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, object?>?>(),
                    It.IsAny<int?>(),
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var compactResult = await _tool.ExecuteStoredProcedure("dbo", "GetUsers", compact: true);
            var prettyResult = await _tool.ExecuteStoredProcedure("dbo", "GetUsers", compact: false);

            // Assert
            // Pretty-printed JSON should have newlines
            prettyResult.Should().Contain("\n");
            // Compact JSON should be on fewer lines
            compactResult.Split('\n').Length.Should().BeLessThan(prettyResult.Split('\n').Length);
        }

        [Fact]
        public async Task ExecuteStoredProcedure_WithMaxCharsExceeded_ReturnsTruncatedSummary()
        {
            // Arrange
            var largeResult = new QueryResult
            {
                IsSuccess = true,
                Columns = ["Id", "LongText"],
                Rows = Enumerable.Range(1, 100).Select(i => new Dictionary<string, object>
                {
                    ["Id"] = i,
                    ["LongText"] = new string('x', 100)
                }).ToList(),
                RowCount = 100,
                TotalRowCount = 100
            };

            _queryServiceMock
                .Setup(x => x.ExecuteStoredProcedureAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, object?>?>(),
                    It.IsAny<int?>(),
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(largeResult);

            // Act
            var result = await _tool.ExecuteStoredProcedure("dbo", "GetLargeData", maxChars: 500);

            // Assert
            result.Should().Contain("Truncated");
            result.Should().Contain("SampleRows");
            result.Should().Contain("exceeded");
        }

        [Fact]
        public async Task ExecuteStoredProcedure_WithNoResults_ReturnsSuccessMessage()
        {
            // Arrange
            var noResultsResult = new QueryResult
            {
                IsSuccess = true,
                Rows = [],
                Message = "0 rows affected"
            };

            _queryServiceMock
                .Setup(x => x.ExecuteStoredProcedureAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, object?>?>(),
                    It.IsAny<int?>(),
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(noResultsResult);

            // Act
            var result = await _tool.ExecuteStoredProcedure("dbo", "UpdateSomething");

            // Assert
            result.Should().Be("0 rows affected");
        }

        [Fact]
        public async Task ExecuteStoredProcedure_WithNoResultsAndNoMessage_ReturnsDefaultMessage()
        {
            // Arrange
            var noResultsResult = new QueryResult
            {
                IsSuccess = true,
                Rows = []
                // Message defaults to empty string
            };

            _queryServiceMock
                .Setup(x => x.ExecuteStoredProcedureAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, object?>?>(),
                    It.IsAny<int?>(),
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(noResultsResult);

            // Act
            var result = await _tool.ExecuteStoredProcedure("dbo", "DoSomething");

            // Assert
            result.Should().Contain("executed successfully");
        }
    }
}
