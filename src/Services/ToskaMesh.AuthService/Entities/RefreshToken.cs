using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace ToskaMesh.AuthService.Entities;

public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>
    /// Hashed refresh token value (stored in DB).
    /// </summary>
    [Column("Token")]
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>
    /// Raw token value to return to the caller; not persisted.
    /// </summary>
    [NotMapped]
    [JsonIgnore]
    public string? PlaintextToken { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public bool Revoked { get; set; }
    public string UserId { get; set; } = string.Empty;
    public MeshUser User { get; set; } = default!;
    public string? ClientId { get; set; }
    public string? IpAddress { get; set; }
}
