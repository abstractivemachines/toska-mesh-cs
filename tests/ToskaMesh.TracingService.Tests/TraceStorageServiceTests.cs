using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ToskaMesh.TracingService.Data;
using ToskaMesh.TracingService.Models;
using ToskaMesh.TracingService.Services;
using Xunit;

namespace ToskaMesh.TracingService.Tests;

public class TraceStorageServiceTests
{
    [Fact]
    public async Task IngestAndQuery_RoundTripsTraces()
    {
        await using var context = CreateDbContext();
        var service = new TraceStorageService(context);

        var traceId = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        var request = new TraceIngestRequest
        {
            CorrelationId = "order-123",
            Spans = new[]
            {
                new TraceSpanDto
                {
                    TraceId = traceId,
                    SpanId = "root-span",
                    ServiceName = "gateway",
                    OperationName = "HTTP GET /orders",
                    StartTime = now.AddMilliseconds(-150),
                    EndTime = now.AddMilliseconds(-50),
                    Status = "Ok",
                    Kind = "Server",
                    CorrelationId = "order-123",
                    Attributes = new Dictionary<string, string?> { { "http.status_code", "200" } }
                },
                new TraceSpanDto
                {
                    TraceId = traceId,
                    SpanId = "child-span",
                    ParentSpanId = "root-span",
                    ServiceName = "orders-service",
                    OperationName = "GET /db/orders",
                    StartTime = now.AddMilliseconds(-120),
                    EndTime = now.AddMilliseconds(-60),
                    Status = "Ok",
                    Kind = "Client",
                    CorrelationId = "order-123"
                }
            }
        };

        await service.IngestAsync(request, CancellationToken.None);

        var response = await service.QueryAsync(new TraceQueryParameters(), CancellationToken.None);
        response.Total.Should().Be(1);
        response.Items.Single().TraceId.Should().Be(traceId);

        var detail = await service.GetTraceAsync(traceId, CancellationToken.None);
        detail.Should().NotBeNull();
        detail!.Spans.Should().HaveCount(2);
        detail.Summary.CorrelationId.Should().Be("order-123");
    }

    private static TracingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<TracingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TracingDbContext(options);
    }
}
