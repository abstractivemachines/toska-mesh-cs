using Microsoft.EntityFrameworkCore;
using ToskaMesh.ConfigService.Entities;

namespace ToskaMesh.ConfigService.Data;

public class ConfigDbContext : DbContext
{
    public ConfigDbContext(DbContextOptions<ConfigDbContext> options) : base(options)
    {
    }

    public DbSet<ConfigurationItem> Configurations => Set<ConfigurationItem>();
    public DbSet<ConfigurationVersion> Versions => Set<ConfigurationVersion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ConfigurationItem>(entity =>
        {
            entity.HasIndex(item => new { item.Name, item.Environment }).IsUnique();
            entity.Property(item => item.Name).HasMaxLength(200);
            entity.Property(item => item.Environment).HasMaxLength(100);
        });

        modelBuilder.Entity<ConfigurationVersion>(entity =>
        {
            entity.HasIndex(version => new { version.ConfigurationItemId, version.Version }).IsUnique();
            entity.HasOne(version => version.ConfigurationItem)
                .WithMany(item => item.History)
                .HasForeignKey(version => version.ConfigurationItemId);
        });
    }
}
