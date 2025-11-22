using FluentAssertions;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ToskaMesh.Security;
using Xunit;

namespace ToskaMesh.Security.Tests;

public class MeshServiceTokenProviderTests
{
    [Fact]
    public async Task GeneratesTokenWithServiceClaims()
    {
        var authOptions = new MeshServiceAuthOptions
        {
            Secret = "test-secret-key-value-1234567890",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            TokenLifetimeMinutes = 5
        };

        var identityOptions = new MeshServiceIdentityOptions
        {
            ServiceName = "Gateway",
            Roles = new[] { "Edge" }
        };

        var provider = new MeshServiceTokenProvider(authOptions, identityOptions);
        var token = await provider.GetTokenAsync();

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.Claims.Should().Contain(claim => claim.Type == MeshAuthorizationPolicies.ServiceIdClaim && claim.Value == "Gateway");
        jwt.Claims.Should().Contain(claim => claim.Type == ClaimTypes.Role && claim.Value == MeshAuthorizationPolicies.ServiceRole);
        jwt.Claims.Should().Contain(claim => claim.Type == ClaimTypes.Role && claim.Value == "Edge");

        // Calling again should reuse cached token (same string)
        var second = await provider.GetTokenAsync();
        second.Should().Be(token);
    }
}
