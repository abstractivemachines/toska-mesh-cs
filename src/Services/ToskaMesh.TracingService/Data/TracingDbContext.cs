using Microsoft.EntityFrameworkCore;
using ToskaMesh.TracingService.Entities;

namespace ToskaMesh.TracingService.Data;

public class TracingDbContext : DbContext
{
    public TracingDbContext(DbContextOptions<TracingDbContext> options) : base(options)
    {
    }

    public DbSet<TraceSpan> TraceSpans => Set<TraceSpan>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var span = modelBuilder.Entity<TraceSpan>();
        span.HasKey(x => x.Id);
        span.HasIndex(x => x.TraceId);
        span.HasIndex(x => new { x.TraceId, x.SpanId }).IsUnique();
        span.Property(x => x.TraceId).HasMaxLength(64).IsRequired();
        span.Property(x => x.SpanId).HasMaxLength(64).IsRequired();
        span.Property(x => x.ParentSpanId).HasMaxLength(64);
        span.Property(x => x.ServiceName).HasMaxLength(200).IsRequired();
        span.Property(x => x.OperationName).HasMaxLength(200).IsRequired();
        span.Property(x => x.Status).HasMaxLength(32);
        span.Property(x => x.Kind).HasMaxLength(32);
        span.Property(x => x.CorrelationId).HasMaxLength(128);
        span.Property(x => x.AttributesJson).HasColumnType("jsonb");
        span.Property(x => x.EventsJson).HasColumnType("jsonb");
        span.Property(x => x.ResourceAttributesJson).HasColumnType("jsonb");
    }
}
