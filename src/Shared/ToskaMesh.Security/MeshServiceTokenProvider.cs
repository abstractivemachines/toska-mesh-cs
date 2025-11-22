using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Linq;

namespace ToskaMesh.Security;

public interface IMeshServiceTokenProvider
{
    Task<string> GetTokenAsync(CancellationToken cancellationToken = default);
}

public class MeshServiceTokenProvider : IMeshServiceTokenProvider
{
    private readonly MeshServiceAuthOptions _authOptions;
    private readonly MeshServiceIdentityOptions _identityOptions;
    private readonly object _sync = new();
    private string? _cachedToken;
    private DateTimeOffset _expiresAt;

    public MeshServiceTokenProvider(
        MeshServiceAuthOptions authOptions,
        MeshServiceIdentityOptions identityOptions)
    {
        _authOptions = authOptions;
        _identityOptions = identityOptions;
    }

    public Task<string> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(_cachedToken) && DateTimeOffset.UtcNow < _expiresAt - TimeSpan.FromSeconds(30))
        {
            return Task.FromResult(_cachedToken!);
        }

        lock (_sync)
        {
            if (!string.IsNullOrWhiteSpace(_cachedToken) && DateTimeOffset.UtcNow < _expiresAt - TimeSpan.FromSeconds(30))
            {
                return Task.FromResult(_cachedToken!);
            }

            var expires = DateTimeOffset.UtcNow.AddMinutes(Math.Max(1, _authOptions.TokenLifetimeMinutes));

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, _identityOptions.ServiceName),
                new Claim(ClaimTypes.Name, _identityOptions.ServiceName),
                new Claim(MeshAuthorizationPolicies.ServiceIdClaim, _identityOptions.ServiceName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
            };

            foreach (var role in _identityOptions.Roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            claims.Add(new Claim(ClaimTypes.Role, MeshAuthorizationPolicies.ServiceRole));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_authOptions.Secret));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _authOptions.Issuer,
                audience: _authOptions.Audience,
                claims: claims,
                expires: expires.UtcDateTime,
                signingCredentials: credentials);

            _cachedToken = new JwtSecurityTokenHandler().WriteToken(token);
            _expiresAt = expires;
            return Task.FromResult(_cachedToken!);
        }
    }
}

public class MeshServiceAuthOptions
{
    public string Secret { get; set; } = "change-me";
    public string Issuer { get; set; } = "ToskaMesh.Services";
    public string Audience { get; set; } = "ToskaMesh.Services";
    public int TokenLifetimeMinutes { get; set; } = 10;
}

public class MeshServiceIdentityOptions
{
    public string ServiceName { get; set; } = "MeshService";
    public string[] Roles { get; set; } = Array.Empty<string>();
}

public static class MeshServiceAuthenticationExtensions
{
    public static IServiceCollection AddMeshServiceIdentity(this IServiceCollection services, IConfiguration configuration)
    {
        if (!services.Any(sd => sd.ServiceType == typeof(MeshServiceAuthOptions)))
        {
            var authOptions = configuration.GetSection("Mesh:ServiceAuth").Get<MeshServiceAuthOptions>() ?? new MeshServiceAuthOptions();
            if (string.IsNullOrWhiteSpace(authOptions.Secret))
            {
                throw new InvalidOperationException("Mesh:ServiceAuth:Secret must be configured for service-to-service authentication.");
            }

            services.AddSingleton(authOptions);
        }

        if (!services.Any(sd => sd.ServiceType == typeof(MeshServiceIdentityOptions)))
        {
            var identityOptions = configuration.GetSection("Mesh:Identity").Get<MeshServiceIdentityOptions>() ?? new MeshServiceIdentityOptions
            {
                ServiceName = configuration["Mesh:ServiceName"] ?? configuration["DOTNET_APPLICATIONNAME"] ?? "MeshService"
            };

            if (string.IsNullOrWhiteSpace(identityOptions.ServiceName))
            {
                identityOptions.ServiceName = configuration["DOTNET_APPLICATIONNAME"] ?? "MeshService";
            }

            services.AddSingleton(identityOptions);
        }

        if (!services.Any(sd => sd.ServiceType == typeof(IMeshServiceTokenProvider)))
        {
            services.AddSingleton<IMeshServiceTokenProvider, MeshServiceTokenProvider>();
        }

        return services;
    }
}
