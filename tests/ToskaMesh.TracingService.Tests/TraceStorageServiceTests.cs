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

    [Fact]
    public async Task QueryAsync_FiltersByServiceName()
    {
        await using var context = CreateDbContext();
        var service = new TraceStorageService(context);

        await IngestTrace(service, "trace-1", "gateway", "HTTP GET /api");
        await IngestTrace(service, "trace-2", "orders-service", "ProcessOrder");
        await IngestTrace(service, "trace-3", "gateway", "HTTP POST /api");

        var response = await service.QueryAsync(new TraceQueryParameters
        {
            ServiceName = "gateway"
        }, CancellationToken.None);

        response.Total.Should().Be(2);
        response.Items.Should().AllSatisfy(t => t.ServiceName.Should().Be("gateway"));
    }

    [Fact]
    public async Task QueryAsync_FiltersByOperationName()
    {
        await using var context = CreateDbContext();
        var service = new TraceStorageService(context);

        await IngestTrace(service, "trace-1", "api", "CreateUser");
        await IngestTrace(service, "trace-2", "api", "DeleteUser");
        await IngestTrace(service, "trace-3", "api", "CreateUser");

        var response = await service.QueryAsync(new TraceQueryParameters
        {
            OperationName = "CreateUser"
        }, CancellationToken.None);

        response.Total.Should().Be(2);
    }

    [Fact]
    public async Task QueryAsync_FiltersByStatus()
    {
        await using var context = CreateDbContext();
        var service = new TraceStorageService(context);

        await IngestTrace(service, "trace-1", "api", "Op1", status: "Ok");
        await IngestTrace(service, "trace-2", "api", "Op2", status: "Error");
        await IngestTrace(service, "trace-3", "api", "Op3", status: "Ok");

        var response = await service.QueryAsync(new TraceQueryParameters
        {
            Status = "Error"
        }, CancellationToken.None);

        response.Total.Should().Be(1);
        response.Items.Single().Status.Should().Be("Error");
    }

    [Fact]
    public async Task QueryAsync_FiltersByCorrelationId()
    {
        await using var context = CreateDbContext();
        var service = new TraceStorageService(context);

        await IngestTrace(service, "trace-1", "api", "Op1", correlationId: "req-123");
        await IngestTrace(service, "trace-2", "api", "Op2", correlationId: "req-456");
        await IngestTrace(service, "trace-3", "api", "Op3", correlationId: "req-123");

        var response = await service.QueryAsync(new TraceQueryParameters
        {
            CorrelationId = "req-123"
        }, CancellationToken.None);

        response.Total.Should().Be(2);
        response.Items.Should().AllSatisfy(t => t.CorrelationId.Should().Be("req-123"));
    }

    [Fact]
    public async Task QueryAsync_FiltersByDurationRange()
    {
        await using var context = CreateDbContext();
        var service = new TraceStorageService(context);

        await IngestTrace(service, "trace-fast", "api", "FastOp", durationMs: 50);
        await IngestTrace(service, "trace-medium", "api", "MediumOp", durationMs: 200);
        await IngestTrace(service, "trace-slow", "api", "SlowOp", durationMs: 500);

        var response = await service.QueryAsync(new TraceQueryParameters
        {
            MinDurationMs = 100,
            MaxDurationMs = 300
        }, CancellationToken.None);

        response.Total.Should().Be(1);
        response.Items.Single().TraceId.Should().Be("trace-medium");
    }

    [Fact]
    public async Task QueryAsync_SupportsPagination()
    {
        await using var context = CreateDbContext();
        var service = new TraceStorageService(context);

        for (int i = 0; i < 15; i++)
        {
            await IngestTrace(service, $"trace-{i:D2}", "api", "Operation");
        }

        var page1 = await service.QueryAsync(new TraceQueryParameters { Page = 1, PageSize = 5 }, CancellationToken.None);
        var page2 = await service.QueryAsync(new TraceQueryParameters { Page = 2, PageSize = 5 }, CancellationToken.None);
        var page3 = await service.QueryAsync(new TraceQueryParameters { Page = 3, PageSize = 5 }, CancellationToken.None);

        page1.Total.Should().Be(15);
        page1.Items.Should().HaveCount(5);
        page2.Items.Should().HaveCount(5);
        page3.Items.Should().HaveCount(5);

        // Ensure no overlap between pages
        var allTraceIds = page1.Items.Concat(page2.Items).Concat(page3.Items)
            .Select(t => t.TraceId).ToList();
        allTraceIds.Distinct().Should().HaveCount(15);
    }

    [Fact]
    public async Task GetTraceAsync_ReturnsNullForNonExistent()
    {
        await using var context = CreateDbContext();
        var service = new TraceStorageService(context);

        var result = await service.GetTraceAsync("nonexistent-trace-id", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetTraceAsync_CalculatesCorrectDuration()
    {
        await using var context = CreateDbContext();
        var service = new TraceStorageService(context);

        var traceId = "duration-test";
        var now = DateTimeOffset.UtcNow;

        await service.IngestAsync(new TraceIngestRequest
        {
            Spans = new[]
            {
                new TraceSpanDto
                {
                    TraceId = traceId,
                    SpanId = "span-1",
                    ServiceName = "api",
                    OperationName = "TestOp",
                    StartTime = now.AddMilliseconds(-500),
                    EndTime = now,
                    Status = "Ok"
                }
            }
        }, CancellationToken.None);

        var detail = await service.GetTraceAsync(traceId, CancellationToken.None);

        detail.Should().NotBeNull();
        detail!.Summary.DurationMs.Should().BeApproximately(500, 10);
    }

    [Fact]
    public async Task IngestAsync_HandlesEmptySpans()
    {
        await using var context = CreateDbContext();
        var service = new TraceStorageService(context);

        await service.IngestAsync(new TraceIngestRequest
        {
            Spans = Array.Empty<TraceSpanDto>()
        }, CancellationToken.None);

        var response = await service.QueryAsync(new TraceQueryParameters(), CancellationToken.None);
        response.Total.Should().Be(0);
    }

    [Fact]
    public async Task QueryAsync_CombinesMultipleFilters()
    {
        await using var context = CreateDbContext();
        var service = new TraceStorageService(context);

        await IngestTrace(service, "trace-1", "gateway", "CreateOrder", status: "Ok");
        await IngestTrace(service, "trace-2", "gateway", "CreateOrder", status: "Error");
        await IngestTrace(service, "trace-3", "orders", "CreateOrder", status: "Ok");
        await IngestTrace(service, "trace-4", "gateway", "DeleteOrder", status: "Ok");

        var response = await service.QueryAsync(new TraceQueryParameters
        {
            ServiceName = "gateway",
            OperationName = "CreateOrder",
            Status = "Ok"
        }, CancellationToken.None);

        response.Total.Should().Be(1);
        response.Items.Single().TraceId.Should().Be("trace-1");
    }

    private static async Task IngestTrace(
        TraceStorageService service,
        string traceId,
        string serviceName,
        string operationName,
        string status = "Ok",
        string? correlationId = null,
        double durationMs = 100)
    {
        var now = DateTimeOffset.UtcNow;
        await service.IngestAsync(new TraceIngestRequest
        {
            CorrelationId = correlationId,
            Spans = new[]
            {
                new TraceSpanDto
                {
                    TraceId = traceId,
                    SpanId = Guid.NewGuid().ToString("N"),
                    ServiceName = serviceName,
                    OperationName = operationName,
                    StartTime = now.AddMilliseconds(-durationMs),
                    EndTime = now,
                    Status = status,
                    CorrelationId = correlationId
                }
            }
        }, CancellationToken.None);
    }

    private static TracingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<TracingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TracingDbContext(options);
    }
}
