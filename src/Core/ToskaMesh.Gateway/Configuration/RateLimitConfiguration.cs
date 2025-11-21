namespace ToskaMesh.Gateway.Configuration;

/// <summary>
/// Rate limiting configuration settings.
/// </summary>
public class RateLimitConfiguration
{
    public const string SectionName = "RateLimit";

    public bool EnableRateLimiting { get; set; } = true;
    public int PermitLimit { get; set; } = 100;
    public int WindowSeconds { get; set; } = 60;
    public int QueueLimit { get; set; } = 10;
}
