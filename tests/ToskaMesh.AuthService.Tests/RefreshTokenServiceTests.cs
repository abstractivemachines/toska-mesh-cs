using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ToskaMesh.AuthService.Data;
using ToskaMesh.AuthService.Entities;
using ToskaMesh.AuthService.Services;
using Xunit;

namespace ToskaMesh.AuthService.Tests;

public class RefreshTokenServiceTests
{
    [Fact]
    public async Task IssueAsync_CreatesNewToken()
    {
        await using var context = CreateDbContext();
        var service = new RefreshTokenService(context);
        var user = await CreateTestUser(context);

        var token = await service.IssueAsync(user, "test-client", "127.0.0.1");

        token.Should().NotBeNull();
        token.UserId.Should().Be(user.Id);
        token.ClientId.Should().Be("test-client");
        token.IpAddress.Should().Be("127.0.0.1");
        token.PlaintextToken.Should().NotBeNullOrEmpty();
        token.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task IssueAsync_StoresHashedTokenInDatabase()
    {
        await using var context = CreateDbContext();
        var service = new RefreshTokenService(context);
        var user = await CreateTestUser(context);

        var token = await service.IssueAsync(user, null, null);

        // The stored TokenHash should not equal the plaintext
        token.TokenHash.Should().NotBe(token.PlaintextToken);
        token.TokenHash.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ValidateAsync_ReturnsTokenForValidToken()
    {
        await using var context = CreateDbContext();
        var service = new RefreshTokenService(context);
        var user = await CreateTestUser(context);

        var issued = await service.IssueAsync(user, null, null);
        var validated = await service.ValidateAsync(issued.PlaintextToken!);

        validated.Should().NotBeNull();
        validated!.Id.Should().Be(issued.Id);
        validated.User.Should().NotBeNull();
    }

    [Fact]
    public async Task ValidateAsync_ReturnsNullForNonExistentToken()
    {
        await using var context = CreateDbContext();
        var service = new RefreshTokenService(context);

        var result = await service.ValidateAsync("nonexistent-token-12345");

        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAsync_ReturnsNullForExpiredToken()
    {
        await using var context = CreateDbContext();
        var user = await CreateTestUser(context);

        // Directly create an expired token
        var expiredToken = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = "expired-hash",
            PlaintextToken = "expired-token",
            ExpiresAt = DateTime.UtcNow.AddDays(-1), // Already expired
            Revoked = false
        };
        context.RefreshTokens.Add(expiredToken);
        await context.SaveChangesAsync();

        var service = new RefreshTokenService(context);

        // We need to create a token that can be validated via hash
        var validToken = await service.IssueAsync(user, null, null);

        // Manually expire it
        validToken.ExpiresAt = DateTime.UtcNow.AddDays(-1);
        await context.SaveChangesAsync();

        var result = await service.ValidateAsync(validToken.PlaintextToken!);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAsync_ReturnsNullForRevokedToken()
    {
        await using var context = CreateDbContext();
        var service = new RefreshTokenService(context);
        var user = await CreateTestUser(context);

        var issued = await service.IssueAsync(user, null, null);
        await service.RevokeAsync(issued);

        var result = await service.ValidateAsync(issued.PlaintextToken!);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RevokeAsync_MarksTokenAsRevoked()
    {
        await using var context = CreateDbContext();
        var service = new RefreshTokenService(context);
        var user = await CreateTestUser(context);

        var token = await service.IssueAsync(user, null, null);
        token.Revoked.Should().BeFalse();

        await service.RevokeAsync(token);

        token.Revoked.Should().BeTrue();
    }

    [Fact]
    public async Task IssueAsync_GeneratesUniqueTokens()
    {
        await using var context = CreateDbContext();
        var service = new RefreshTokenService(context);
        var user = await CreateTestUser(context);

        var tokens = new HashSet<string>();
        for (int i = 0; i < 10; i++)
        {
            var token = await service.IssueAsync(user, null, null);
            tokens.Add(token.PlaintextToken!);
        }

        tokens.Should().HaveCount(10, "all tokens should be unique");
    }

    [Fact]
    public async Task IssueAsync_SetsExpirationToFourteenDays()
    {
        await using var context = CreateDbContext();
        var service = new RefreshTokenService(context);
        var user = await CreateTestUser(context);

        var now = DateTime.UtcNow;
        var token = await service.IssueAsync(user, null, null);

        var expectedExpiration = now.AddDays(14);
        token.ExpiresAt.Should().BeCloseTo(expectedExpiration, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ValidateAsync_MarksExpiredTokenAsRevoked()
    {
        await using var context = CreateDbContext();
        var service = new RefreshTokenService(context);
        var user = await CreateTestUser(context);

        var token = await service.IssueAsync(user, null, null);
        token.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await context.SaveChangesAsync();

        await service.ValidateAsync(token.PlaintextToken!);

        // After validation, the token should be marked revoked
        var refreshedToken = await context.RefreshTokens.FindAsync(token.Id);
        refreshedToken!.Revoked.Should().BeTrue();
    }

    private static AuthDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AuthDbContext(options);
    }

    private static async Task<MeshUser> CreateTestUser(AuthDbContext context)
    {
        var user = new MeshUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "testuser",
            Email = "test@example.com",
            FullName = "Test User"
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }
}
