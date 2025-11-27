# Change: Fix Non-Deterministic Hash in IPHash Load Balancing Strategy

**Date:** 2025-11-27
**Type:** Bug Fix
**File:** `src/Core/ToskaMesh.Router/Services/LoadBalancerService.cs`

## Summary

Replaced `string.GetHashCode()` with a deterministic FNV-1a hash algorithm in the IPHash load balancing strategy.

## Problem

The IPHash strategy used `string.GetHashCode()` to determine which service instance to route to:

```csharp
var hash = Math.Abs(key.GetHashCode());
return instances[hash % instances.Count];
```

In .NET Core/5+, `string.GetHashCode()` is randomized per process for security reasons (hash DoS prevention). This means:
- The same session ID would route to different instances after an app restart
- Different instances of the load balancer would route the same session to different backends
- Session affinity would be broken unpredictably

## Solution

Implemented `GetStableHash()` using the FNV-1a (Fowler-Noll-Vo) hash algorithm:

```csharp
private static int GetStableHash(string value)
{
    unchecked
    {
        const int fnvOffsetBasis = unchecked((int)2166136261);
        const int fnvPrime = 16777619;

        var hash = fnvOffsetBasis;
        foreach (var c in value)
        {
            hash ^= c;
            hash *= fnvPrime;
        }

        return Math.Abs(hash);
    }
}
```

### Why FNV-1a?
- **Deterministic**: Same input always produces same output
- **Fast**: Simple operations, no allocations
- **Good distribution**: Suitable for hash table use cases
- **Well-established**: Widely used in databases and distributed systems

## Impact

- IPHash load balancing now provides consistent routing across:
  - Process restarts
  - Multiple load balancer instances
  - Different machines
- Session affinity works reliably for the same session ID or correlation ID

## Files Changed

- `src/Core/ToskaMesh.Router/Services/LoadBalancerService.cs`
  - Added `GetStableHash()` method
  - Updated `SelectIpHash()` to use stable hash
