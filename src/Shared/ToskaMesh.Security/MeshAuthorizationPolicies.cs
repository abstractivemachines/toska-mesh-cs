using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace ToskaMesh.Security;

/// <summary>
/// Defines authorization policies for ToskaMesh.
/// </summary>
public static class MeshAuthorizationPolicies
{
    // Policy names
    public const string RequireAuthenticatedUser = "RequireAuthenticatedUser";
    public const string RequireAdminRole = "RequireAdminRole";
    public const string RequireServiceRole = "RequireServiceRole";
    public const string RequireUserRole = "RequireUserRole";
    public const string RequireApiKey = "RequireApiKey";

    // Role names
    public const string AdminRole = "Admin";
    public const string ServiceRole = "Service";
    public const string UserRole = "User";

    // Claim types
    public const string ServiceIdClaim = "service_id";
    public const string ApiKeyClaim = "api_key";

    /// <summary>
    /// Adds ToskaMesh authorization policies to the service collection.
    /// </summary>
    public static IServiceCollection AddMeshAuthorizationPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            // Require authenticated user
            options.AddPolicy(RequireAuthenticatedUser, policy =>
            {
                policy.RequireAuthenticatedUser();
            });

            // Require admin role
            options.AddPolicy(RequireAdminRole, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireRole(AdminRole);
            });

            // Require service role (for service-to-service communication)
            options.AddPolicy(RequireServiceRole, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireRole(ServiceRole);
                policy.RequireClaim(ServiceIdClaim);
            });

            // Require user role
            options.AddPolicy(RequireUserRole, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireRole(UserRole, AdminRole); // Users or Admins can access
            });

            // Require API key authentication
            options.AddPolicy(RequireApiKey, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireClaim(ApiKeyClaim);
            });
        });

        return services;
    }
}

/// <summary>
/// Authorization requirement for custom authorization handlers.
/// </summary>
public class ServiceAuthorizationRequirement : IAuthorizationRequirement
{
    public string RequiredServiceId { get; }

    public ServiceAuthorizationRequirement(string requiredServiceId)
    {
        RequiredServiceId = requiredServiceId;
    }
}

/// <summary>
/// Authorization handler for service-specific authorization.
/// </summary>
public class ServiceAuthorizationHandler : AuthorizationHandler<ServiceAuthorizationRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ServiceAuthorizationRequirement requirement)
    {
        var serviceIdClaim = context.User.FindFirst(MeshAuthorizationPolicies.ServiceIdClaim);

        if (serviceIdClaim != null && serviceIdClaim.Value == requirement.RequiredServiceId)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
