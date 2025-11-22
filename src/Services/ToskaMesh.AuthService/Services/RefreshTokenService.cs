using Microsoft.EntityFrameworkCore;
using ToskaMesh.AuthService.Data;
using ToskaMesh.AuthService.Entities;

namespace ToskaMesh.AuthService.Services;

public interface IRefreshTokenService
{
    Task<RefreshToken> IssueAsync(MeshUser user, string? clientId, string? ipAddress, CancellationToken cancellationToken = default);
    Task<RefreshToken?> ValidateAsync(string token, CancellationToken cancellationToken = default);
    Task RevokeAsync(RefreshToken token, CancellationToken cancellationToken = default);
}

public class RefreshTokenService : IRefreshTokenService
{
    private readonly AuthDbContext _dbContext;

    public RefreshTokenService(AuthDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RefreshToken> IssueAsync(MeshUser user, string? clientId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var token = new RefreshToken
        {
            UserId = user.Id,
            ClientId = clientId,
            IpAddress = ipAddress,
            ExpiresAt = DateTime.UtcNow.AddDays(14)
        };

        _dbContext.RefreshTokens.Add(token);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return token;
    }

    public async Task<RefreshToken?> ValidateAsync(string token, CancellationToken cancellationToken = default)
    {
        var refreshToken = await _dbContext.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == token && !t.Revoked, cancellationToken);

        if (refreshToken == null)
        {
            return null;
        }

        if (refreshToken.ExpiresAt < DateTime.UtcNow)
        {
            refreshToken.Revoked = true;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }

        return refreshToken;
    }

    public async Task RevokeAsync(RefreshToken token, CancellationToken cancellationToken = default)
    {
        token.Revoked = true;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
