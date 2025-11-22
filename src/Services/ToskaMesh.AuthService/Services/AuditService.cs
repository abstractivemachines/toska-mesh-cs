using System.Text.Json;
using ToskaMesh.AuthService.Data;
using ToskaMesh.AuthService.Entities;

namespace ToskaMesh.AuthService.Services;

public interface IAuditService
{
    Task RecordAsync(string userId, string action, object? metadata = null, string? ipAddress = null, CancellationToken cancellationToken = default);
}

public class AuditService : IAuditService
{
    private readonly AuthDbContext _dbContext;

    public AuditService(AuthDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task RecordAsync(string userId, string action, object? metadata = null, string? ipAddress = null, CancellationToken cancellationToken = default)
    {
        var payload = metadata == null ? null : JsonSerializer.Serialize(metadata);
        var log = new AuditLog
        {
            UserId = userId,
            Action = action,
            Metadata = payload,
            IpAddress = ipAddress
        };

        _dbContext.AuditLogs.Add(log);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
