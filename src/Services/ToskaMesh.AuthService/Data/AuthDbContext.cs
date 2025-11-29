using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ToskaMesh.AuthService.Entities;

namespace ToskaMesh.AuthService.Data;

public class AuthDbContext : IdentityDbContext<MeshUser>
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options)
    {
    }

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<MeshUser>(entity =>
        {
            entity.Property(user => user.FullName).HasMaxLength(256);
        });

        builder.Entity<RefreshToken>(entity =>
        {
            entity.HasIndex(token => token.TokenHash).IsUnique();
            entity.HasOne(token => token.User)
                .WithMany(user => user.RefreshTokens)
                .HasForeignKey(token => token.UserId);
        });

        builder.Entity<AuditLog>(entity =>
        {
            entity.HasOne(log => log.User)
                .WithMany(user => user.AuditLogs)
                .HasForeignKey(log => log.UserId);
        });
    }
}
