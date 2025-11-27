# Change: Extract Hardcoded Consul Health Check Configuration

**Date:** 2025-11-27
**Type:** Refactoring
**Files:**
- `src/Shared/ToskaMesh.Common/Health/ConsulHealthCheckOptions.cs` (new)
- `src/Core/ToskaMesh.Gateway/Program.cs`
- `src/Core/ToskaMesh.Discovery/Program.cs`
- `src/Core/ToskaMesh.Gateway/appsettings.json`
- `src/Core/ToskaMesh.Discovery/appsettings.json`

## Summary

Extracted hardcoded Consul health check configuration (`HostName: "consul"`, `Port: 8500`) into a shared configuration class that can be overridden via appsettings.json or environment variables.

## Problem

Consul health check configuration was hardcoded in two locations:
- `Gateway/Program.cs` (lines 63-68)
- `Discovery/Program.cs` (lines 33-37)

```csharp
health.AddConsul(options =>
{
    options.HostName = "consul";
    options.Port = 8500;
});
```

This was inconsistent with the rest of the codebase which uses a well-established configuration pattern with `SectionName` constants and appsettings binding.

## Solution

1. Created `ConsulHealthCheckOptions` class in `ToskaMesh.Common.Health` with sensible defaults
2. Updated both Gateway and Discovery to load configuration from `HealthChecks:Consul` section
3. Added the configuration section to both appsettings.json files

## Configuration

The new configuration section follows the established pattern:

```json
{
  "HealthChecks": {
    "Consul": {
      "HostName": "localhost",
      "Port": 8500
    }
  }
}
```

Can be overridden via environment variables:
- `HealthChecks__Consul__HostName`
- `HealthChecks__Consul__Port`

## Backward Compatibility

The `ConsulHealthCheckOptions` class has defaults (`HostName: "consul"`, `Port: 8500`) matching the previous hardcoded values, so existing deployments without the new config section will continue to work.
