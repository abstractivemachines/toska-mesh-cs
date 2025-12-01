namespace ToskaMesh.Gateway.Configuration;

/// <summary>
/// TLS options for outbound calls from the gateway to mesh services.
/// </summary>
public class GatewayTlsOptions
{
    public const string SectionName = "Mesh:Gateway:Tls";

    /// <summary>
    /// Path to the client certificate (PFX) presented to upstream services.
    /// </summary>
    public string ClientCertificatePath { get; set; } = string.Empty;

    /// <summary>
    /// Password for the client certificate (PFX).
    /// </summary>
    public string ClientCertificatePassword { get; set; } = string.Empty;
}
