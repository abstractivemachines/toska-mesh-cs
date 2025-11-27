# Change: Fix Inaccurate Timestamps in ConsulServiceRegistry

**Date:** 2025-11-27
**Type:** Bug Fix
**File:** `src/Shared/ToskaMesh.Common/ServiceDiscovery/ConsulServiceRegistry.cs`

## Summary

Fixed misleading timestamp values in `ConsulServiceRegistry` that were returning `DateTime.UtcNow` for `RegisteredAt` and `LastHealthCheck` fields, which made it appear that every service was just registered.

## Problem

The `ServiceInstance` record requires `RegisteredAt` and `LastHealthCheck` timestamps, but Consul doesn't natively track these values. The previous implementation returned `DateTime.UtcNow` for both fields:

```csharp
RegisteredAt: DateTime.UtcNow, // Consul doesn't track registration time, using current time
LastHealthCheck: DateTime.UtcNow // Using current time as approximation
```

This was actively misleading - every query would show services as freshly registered.

## Solution

1. **Track registration times locally** using a `ConcurrentDictionary<string, DateTime>` for services registered through this instance

2. **Use `DateTime.MinValue` as sentinel** for unknown timestamps:
   - `RegisteredAt`: Uses locally tracked time if available, otherwise `DateTime.MinValue`
   - `LastHealthCheck`: Always `DateTime.MinValue` since Consul doesn't expose this

3. **Clean up on deregistration** by removing entries from the tracking dictionary

## Behavior

| Scenario | RegisteredAt | LastHealthCheck |
|----------|--------------|-----------------|
| Service registered via this instance | Actual registration time | `DateTime.MinValue` |
| Service discovered (registered elsewhere) | `DateTime.MinValue` | `DateTime.MinValue` |

Consumers can check `if (instance.RegisteredAt == DateTime.MinValue)` to determine if the timestamp is unknown.

## Trade-offs

- Local tracking only works for services registered through this `ConsulServiceRegistry` instance
- Services registered by other instances or before this instance started will have `DateTime.MinValue`
- This is more honest than the previous approach which returned misleading values

## Files Changed

- `src/Shared/ToskaMesh.Common/ServiceDiscovery/ConsulServiceRegistry.cs`
  - Added `_registrationTimes` ConcurrentDictionary
  - Track time on `RegisterAsync`
  - Clean up on `DeregisterAsync`
  - Updated `GetServiceInstancesAsync` and `GetServiceInstanceAsync` to use tracked/sentinel values
