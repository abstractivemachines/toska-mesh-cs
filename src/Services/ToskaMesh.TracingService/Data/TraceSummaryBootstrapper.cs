using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ToskaMesh.TracingService.Data;

public static class TraceSummaryBootstrapper
{
    private const string TraceSummariesViewName = "\"TraceSummaries\"";
    private const string TraceSpansTableName = "\"TraceSpans\"";
    private const string DropTraceSummariesSql = $@"
DROP MATERIALIZED VIEW IF EXISTS {TraceSummariesViewName};
DROP TYPE IF EXISTS {TraceSummariesViewName};";

    private const string CreateMaterializedViewSql = $@"
CREATE MATERIALIZED VIEW IF NOT EXISTS {TraceSummariesViewName} AS
WITH ordered AS (
    SELECT
        ""TraceId"",
        ""ServiceName"",
        COALESCE(""ResourceAttributesJson"", '{{{{}}}}'::jsonb)->>'service.namespace' AS ""ServiceNamespace"",
        ""OperationName"",
        ""Status"",
        ""StartTimeUtc"",
        ""EndTimeUtc"",
        ""CorrelationId"",
        row_number() OVER (PARTITION BY ""TraceId"" ORDER BY ""StartTimeUtc"") AS ""RowNumber""
    FROM {TraceSpansTableName}
),
aggregated AS (
    SELECT
        ""TraceId"",
        MIN(""StartTimeUtc"") AS ""StartTimeUtc"",
        MAX(""EndTimeUtc"") AS ""EndTimeUtc"",
        EXTRACT(EPOCH FROM (MAX(""EndTimeUtc"") - MIN(""StartTimeUtc""))) * 1000 AS ""DurationMs"",
        COUNT(*)::int AS ""SpanCount"",
        MAX(""CorrelationId"") AS ""CorrelationId""
    FROM {TraceSpansTableName}
    GROUP BY ""TraceId""
)
SELECT
    aggregated.""TraceId"",
    ordered.""ServiceName"",
    ordered.""ServiceNamespace"",
    ordered.""OperationName"",
    ordered.""Status"",
    aggregated.""StartTimeUtc"",
    aggregated.""EndTimeUtc"",
    aggregated.""DurationMs"",
    aggregated.""SpanCount"",
    aggregated.""CorrelationId""
FROM aggregated
JOIN ordered ON ordered.""TraceId"" = aggregated.""TraceId"" AND ordered.""RowNumber"" = 1
WITH DATA;";

    private static readonly string[] TraceSummaryIndexSql =
    [
        $"CREATE UNIQUE INDEX IF NOT EXISTS \"IX_TraceSummaries_TraceId\" ON {TraceSummariesViewName} (\"TraceId\");",
        $"CREATE INDEX IF NOT EXISTS \"IX_TraceSummaries_EndTimeUtc\" ON {TraceSummariesViewName} (\"EndTimeUtc\" DESC);",
        $"CREATE INDEX IF NOT EXISTS \"IX_TraceSummaries_EndTimeUtc_StartTimeUtc\" ON {TraceSummariesViewName} (\"EndTimeUtc\" DESC, \"StartTimeUtc\");",
        $"CREATE INDEX IF NOT EXISTS \"IX_TraceSummaries_StartTimeUtc\" ON {TraceSummariesViewName} (\"StartTimeUtc\");",
        $"CREATE INDEX IF NOT EXISTS \"IX_TraceSummaries_ServiceName\" ON {TraceSummariesViewName} (\"ServiceName\");",
        $"CREATE INDEX IF NOT EXISTS \"IX_TraceSummaries_ServiceNamespace\" ON {TraceSummariesViewName} (\"ServiceNamespace\");",
        $"CREATE INDEX IF NOT EXISTS \"IX_TraceSummaries_OperationName\" ON {TraceSummariesViewName} (\"OperationName\");",
        $"CREATE INDEX IF NOT EXISTS \"IX_TraceSummaries_Status\" ON {TraceSummariesViewName} (\"Status\");",
        $"CREATE INDEX IF NOT EXISTS \"IX_TraceSummaries_CorrelationId\" ON {TraceSummariesViewName} (\"CorrelationId\");",
        $"CREATE INDEX IF NOT EXISTS \"IX_TraceSummaries_DurationMs\" ON {TraceSummariesViewName} (\"DurationMs\");",
        $"CREATE INDEX IF NOT EXISTS \"IX_TraceSummaries_Service_EndTimeUtc\" ON {TraceSummariesViewName} (\"ServiceName\", \"EndTimeUtc\" DESC);",
        $"CREATE INDEX IF NOT EXISTS \"IX_TraceSummaries_ServiceNamespace_EndTimeUtc\" ON {TraceSummariesViewName} (\"ServiceNamespace\", \"EndTimeUtc\" DESC);"
    ];

    private static readonly string[] TraceSpanIndexSql =
    [
        $"CREATE INDEX IF NOT EXISTS \"IX_TraceSpans_StartTimeUtc\" ON {TraceSpansTableName} (\"StartTimeUtc\");",
        $"CREATE INDEX IF NOT EXISTS \"IX_TraceSpans_EndTimeUtc\" ON {TraceSpansTableName} (\"EndTimeUtc\");",
        $"CREATE INDEX IF NOT EXISTS \"IX_TraceSpans_ServiceName\" ON {TraceSpansTableName} (\"ServiceName\");",
        $"CREATE INDEX IF NOT EXISTS \"IX_TraceSpans_OperationName\" ON {TraceSpansTableName} (\"OperationName\");",
        $"CREATE INDEX IF NOT EXISTS \"IX_TraceSpans_Status\" ON {TraceSpansTableName} (\"Status\");",
        $"CREATE INDEX IF NOT EXISTS \"IX_TraceSpans_CorrelationId\" ON {TraceSpansTableName} (\"CorrelationId\");",
        $"CREATE INDEX IF NOT EXISTS \"IX_TraceSpans_DurationMs\" ON {TraceSpansTableName} (\"DurationMs\");",
        $"CREATE INDEX IF NOT EXISTS \"IX_TraceSpans_Service_EndTimeUtc\" ON {TraceSpansTableName} (\"ServiceName\", \"EndTimeUtc\" DESC);"
    ];

    private static bool IsDuplicateObject(PostgresException ex)
        => ex.SqlState is "23505" or "42P07" or "42710";

    public static async Task EnsureTraceSummariesAsync(
        this IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TracingDbContext>();
        var originalTimeout = dbContext.Database.GetCommandTimeout();
        dbContext.Database.SetCommandTimeout(300);

        try
        {
            var requiresRefresh = !await MaterializedViewHasColumnAsync(
                dbContext,
                "TraceSummaries",
                "ServiceNamespace",
                cancellationToken);
            if (requiresRefresh)
            {
                await dbContext.Database.ExecuteSqlRawAsync(DropTraceSummariesSql, cancellationToken);
            }

            try
            {
                await dbContext.Database.ExecuteSqlRawAsync(CreateMaterializedViewSql, cancellationToken);
            }
            catch (PostgresException ex) when (IsDuplicateObject(ex))
            {
                logger.LogWarning(ex, "Trace summary view creation hit a duplicate; recreating materialized view.");
                await dbContext.Database.ExecuteSqlRawAsync(DropTraceSummariesSql, cancellationToken);
                await dbContext.Database.ExecuteSqlRawAsync(CreateMaterializedViewSql, cancellationToken);
            }

            foreach (var sql in TraceSummaryIndexSql)
            {
                await ExecuteIndexSqlAsync(dbContext, logger, sql, cancellationToken);
            }

            foreach (var sql in TraceSpanIndexSql)
            {
                await ExecuteIndexSqlAsync(dbContext, logger, sql, cancellationToken);
            }

            logger.LogInformation("Trace summary materialized view and indexes are ready.");
        }
        finally
        {
            dbContext.Database.SetCommandTimeout(originalTimeout);
        }
    }

    private static async Task ExecuteIndexSqlAsync(
        TracingDbContext dbContext,
        ILogger logger,
        string sql,
        CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        }
        catch (PostgresException ex) when (IsDuplicateObject(ex))
        {
            logger.LogWarning(ex, "Index creation hit a duplicate entry; skipping. SQL: {Sql}", sql);
        }
    }

    private static async Task<bool> MaterializedViewHasColumnAsync(
        TracingDbContext dbContext,
        string viewName,
        string columnName,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
SELECT 1
FROM information_schema.columns
WHERE lower(table_name) = lower(@viewName)
  AND lower(column_name) = lower(@columnName)
LIMIT 1;
""";
            var viewParam = command.CreateParameter();
            viewParam.ParameterName = "viewName";
            viewParam.Value = viewName;
            command.Parameters.Add(viewParam);

            var columnParam = command.CreateParameter();
            columnParam.ParameterName = "columnName";
            columnParam.Value = columnName;
            command.Parameters.Add(columnParam);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result != null && result != DBNull.Value;
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }
}
