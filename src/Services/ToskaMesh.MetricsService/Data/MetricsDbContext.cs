using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;
using System.Linq;
using ToskaMesh.MetricsService.Entities;

namespace ToskaMesh.MetricsService.Data;

public class MetricsDbContext : DbContext
{
    public MetricsDbContext(DbContextOptions<MetricsDbContext> options) : base(options)
    {
    }

    public DbSet<MetricSample> MetricSamples => Set<MetricSample>();
    public DbSet<AlertRule> AlertRules => Set<AlertRule>();
    public DbSet<GrafanaDashboard> GrafanaDashboards => Set<GrafanaDashboard>();
    public DbSet<CustomMetricDefinition> CustomMetrics => Set<CustomMetricDefinition>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var dictionaryConverter = new ValueConverter<Dictionary<string, string>?, string?>(
            v => v == null ? null : JsonSerializer.Serialize(v, JsonSerializerOptions),
            v => string.IsNullOrWhiteSpace(v) ? null : JsonSerializer.Deserialize<Dictionary<string, string>>(v, JsonSerializerOptions));

        var dictionaryComparer = new ValueComparer<Dictionary<string, string>?>(
            (l, r) => DictionaryEquals(l, r),
            v => DictionaryHashCode(v),
            v => DictionarySnapshot(v));

        var stringArrayConverter = new ValueConverter<string[]?, string?>(
            v => v == null ? null : JsonSerializer.Serialize(v, JsonSerializerOptions),
            v => string.IsNullOrWhiteSpace(v) ? null : JsonSerializer.Deserialize<string[]>(v, JsonSerializerOptions));

        var stringArrayComparer = new ValueComparer<string[]?>(
            (l, r) => StringArrayEquals(l, r),
            v => StringArrayHashCode(v),
            v => StringArraySnapshot(v));

        modelBuilder.Entity<MetricSample>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Unit).HasMaxLength(32);
            entity.Property(e => e.TimestampUtc).HasDefaultValueSql("NOW()");
            entity.Property(e => e.Labels)
                .HasConversion(dictionaryConverter)
                .Metadata
                .SetValueComparer(dictionaryComparer);
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.TimestampUtc);
        });

        modelBuilder.Entity<AlertRule>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(150);
            entity.Property(e => e.MetricName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Severity).HasMaxLength(32);
            entity.Property(e => e.NotificationChannel).HasMaxLength(64);
            entity.Property(e => e.LabelFilters)
                .HasConversion(dictionaryConverter)
                .Metadata
                .SetValueComparer(dictionaryComparer);
        });

        modelBuilder.Entity<GrafanaDashboard>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Uid).IsUnique();
            entity.Property(e => e.Uid).HasMaxLength(64);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Folder).HasMaxLength(128);
            entity.Property(e => e.Datasource).HasMaxLength(128);
        });

        modelBuilder.Entity<CustomMetricDefinition>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Unit).HasMaxLength(32);
            entity.Property(e => e.LabelNames)
                .HasConversion(stringArrayConverter)
                .Metadata
                .SetValueComparer(stringArrayComparer);
        });
    }

    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    private static bool DictionaryEquals(Dictionary<string, string>? left, Dictionary<string, string>? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left == null || right == null || left.Count != right.Count)
        {
            return false;
        }

        foreach (var kvp in left)
        {
            if (!right.TryGetValue(kvp.Key, out var value) || !string.Equals(value, kvp.Value, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static int DictionaryHashCode(Dictionary<string, string>? value)
    {
        if (value == null || value.Count == 0)
        {
            return 0;
        }

        var hash = new HashCode();
        foreach (var pair in value.OrderBy(pair => pair.Key))
        {
            hash.Add(pair.Key);
            hash.Add(pair.Value);
        }

        return hash.ToHashCode();
    }

    private static Dictionary<string, string>? DictionarySnapshot(Dictionary<string, string>? value) =>
        value == null ? null : new Dictionary<string, string>(value, StringComparer.Ordinal);

    private static bool StringArrayEquals(string[]? left, string[]? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left == null || right == null)
        {
            return false;
        }

        return left.SequenceEqual(right);
    }

    private static int StringArrayHashCode(string[]? values)
    {
        if (values == null || values.Length == 0)
        {
            return 0;
        }

        var hash = new HashCode();
        foreach (var value in values)
        {
            hash.Add(value);
        }

        return hash.ToHashCode();
    }

    private static string[]? StringArraySnapshot(string[]? value) =>
        value == null ? null : value.ToArray();
}
