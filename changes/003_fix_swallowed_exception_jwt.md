# Change: Fix Swallowed Exception in JwtTokenService

**Date:** 2025-11-27
**Type:** Bug Fix
**File:** `src/Shared/ToskaMesh.Security/JwtTokenService.cs`

## Summary

Added proper logging for exceptions in the `ValidateToken` method instead of silently swallowing them.

## Problem

The `ValidateToken` method had a bare catch block that silently swallowed all exceptions:

```csharp
catch
{
    return null;
}
```

This made debugging token validation failures extremely difficult as there was no way to determine why a token was rejected.

## Solution

1. Added `ILogger<JwtTokenService>` dependency with optional injection (defaults to `NullLogger` for backward compatibility)

2. Replaced the bare catch with specific exception handlers:
   - `SecurityTokenExpiredException` - Logged at Debug level (expected in normal operation)
   - `SecurityTokenInvalidSignatureException` - Logged at Warning level (potential security issue)
   - `SecurityTokenException` - Logged at Warning level (other token issues)
   - `Exception` - Logged at Error level (unexpected errors)

## Log Levels Rationale

- **Debug** for expired tokens: This is expected behavior and happens frequently in normal operation
- **Warning** for invalid signatures and other token issues: Could indicate tampering or misconfiguration
- **Error** for unexpected exceptions: Should not happen and needs investigation

## Backward Compatibility

The logger parameter is optional with a default of `NullLogger<JwtTokenService>.Instance`, so existing code that instantiates `JwtTokenService` without a logger will continue to work.

## Files Changed

- `src/Shared/ToskaMesh.Security/JwtTokenService.cs`
  - Added `Microsoft.Extensions.Logging` imports
  - Added `ILogger<JwtTokenService>` field and constructor parameter
  - Replaced bare catch with specific exception handlers with logging
