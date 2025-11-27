# Change: Fix Configuration/Security Issues

**Date:** 2025-11-27
**Type:** Security Fix
**Files:**
- `src/Shared/ToskaMesh.Security/JwtTokenService.cs`
- `src/Core/ToskaMesh.Gateway/Program.cs`

## Summary

Fixed two security configuration issues identified in the code review.

## Issues Fixed

### 1. Weak Default JWT Secret

**Problem:** `JwtTokenOptions.Secret` defaulted to an empty string, which could cause cryptic runtime failures or weak security if not properly configured.

**Solution:** Added validation in `JwtTokenService` constructor:
- Throws `ArgumentException` if secret is null/empty
- Throws `ArgumentException` if secret is less than 32 characters
- Fails fast at startup rather than at runtime during token operations

```csharp
if (string.IsNullOrWhiteSpace(options.Secret))
{
    throw new ArgumentException("JWT secret must be configured...", nameof(options));
}

if (options.Secret.Length < MinimumSecretLength)
{
    throw new ArgumentException($"JWT secret must be at least {MinimumSecretLength} characters...", nameof(options));
}
```

### 2. Rate Limiter Bypass via Proxy

**Problem:** The rate limiter used `context.Connection.RemoteIpAddress` directly, which returns the proxy's IP when the gateway is behind a load balancer or reverse proxy. This meant all clients behind the proxy shared the same rate limit bucket.

**Solution:** Added `GetClientIpAddress()` helper that:
1. First checks `X-Forwarded-For` header (set by proxies/load balancers)
2. Parses the first IP from the comma-separated list (original client)
3. Falls back to direct connection IP if header is not present

```csharp
static string GetClientIpAddress(HttpContext context)
{
    var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
    if (!string.IsNullOrEmpty(forwardedFor))
    {
        var clientIp = forwardedFor.Split(',', ...).FirstOrDefault();
        if (!string.IsNullOrEmpty(clientIp))
            return clientIp;
    }
    return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
```

## Security Considerations

- The 32-character minimum for JWT secrets ensures adequate entropy for HMAC-SHA256
- `X-Forwarded-For` can be spoofed by clients, but this is acceptable for rate limiting (not authentication)
- If stricter IP validation is needed, consider using `X-Real-IP` or configuring trusted proxy ranges

## Files Changed

- `src/Shared/ToskaMesh.Security/JwtTokenService.cs` - Added constructor validation
- `src/Core/ToskaMesh.Gateway/Program.cs` - Added `GetClientIpAddress()` helper and updated rate limiter
