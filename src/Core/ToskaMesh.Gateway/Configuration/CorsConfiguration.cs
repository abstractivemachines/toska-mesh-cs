namespace ToskaMesh.Gateway.Configuration;

/// <summary>
/// Configuration for CORS policies.
/// </summary>
public class CorsConfiguration
{
    public const string SectionName = "Cors";

    /// <summary>
    /// Allows all origins when true. Otherwise <see cref="AllowedOrigins"/> must contain entries.
    /// </summary>
    public bool AllowAnyOrigin { get; set; } = false;

    /// <summary>
    /// Optional list of allowed origins.
    /// </summary>
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();

    public string[] AllowedHeaders { get; set; } = new[] { "Authorization", "Content-Type" };
    public string[] AllowedMethods { get; set; } = new[] { "GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS" };
}
