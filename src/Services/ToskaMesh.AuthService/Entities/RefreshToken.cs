namespace ToskaMesh.AuthService.Entities;

public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Token { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public bool Revoked { get; set; }
    public string UserId { get; set; } = string.Empty;
    public MeshUser User { get; set; } = default!;
    public string? ClientId { get; set; }
    public string? IpAddress { get; set; }
}
