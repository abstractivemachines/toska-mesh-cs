# ADR-001: Use Orleans for Clustering

## Status
Accepted

## Context

The original Elixir implementation uses BEAM/OTP for distributed computing with lightweight processes and supervision trees. When porting to C#/.NET, we needed a clustering solution that could provide similar capabilities:

- Distributed state management
- Virtual actor model (similar to Erlang processes)
- Automatic cluster membership and failure detection
- Persistence and reminders

Options considered:
1. **Microsoft Orleans** - Virtual actor framework from Microsoft
2. **Akka.NET** - Port of Akka from JVM
3. **Proto.Actor** - Lightweight actor framework
4. **Custom implementation** - Using raw distributed primitives

## Decision

We chose **Microsoft Orleans** for the following reasons:

1. **Virtual Actor Model**: Orleans grains are virtual actors that are automatically activated on demand and deactivated when idle, similar to Erlang processes
2. **Microsoft Support**: First-party support, active development, and integration with Azure
3. **Maturity**: Battle-tested in production at scale (Halo, Azure PlayFab)
4. **Clustering Providers**: Built-in support for Consul, Azure Storage, ADO.NET, and more
5. **Persistence**: Grain state can be persisted to various backends
6. **.NET Native**: Designed specifically for .NET, excellent integration

## Consequences

### Positive
- Simplified distributed state management
- Automatic cluster scaling and rebalancing
- Built-in fault tolerance and recovery
- Strong typing with C# interfaces for grain contracts
- Good tooling (Orleans Dashboard)

### Negative
- Learning curve for developers unfamiliar with actor model
- Overhead for simple services that don't need distributed state
- Requires careful grain design to avoid performance issues
- Dependency on Orleans ecosystem and versioning

### Mitigations
- Created `ToskaMesh.Runtime` abstraction to reduce Orleans coupling in business services
- Documented grain design patterns in codebase
- Orleans is optional - services can run without it for simpler deployments
