using Models;

namespace Services
{
    public interface IQueryService
    {
        Task<QueryResult> ExecuteQueryAsync(
            string query,
            int? commandTimeout = null,
            int? maxRows = null,
            CancellationToken cancellationToken = default);

        Task<QueryResult> ExecuteStoredProcedureAsync(
            string schema,
            string procedureName,
            Dictionary<string, object?>? parameters = null,
            int? commandTimeout = null,
            int? maxRows = null,
            CancellationToken cancellationToken = default);
    }
}
