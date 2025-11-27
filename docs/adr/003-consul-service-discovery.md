# ADR-003: Use Consul for Service Discovery

## Status
Accepted

## Context

Service discovery is essential for a service mesh. Services need to:
- Register themselves on startup
- Discover other services dynamically
- Report health status
- Handle service instance changes

Options considered:
1. **HashiCorp Consul** - Full-featured service mesh and discovery
2. **Netflix Eureka** - Service registry (Java-centric)
3. **Kubernetes DNS** - Native K8s service discovery
4. **etcd** - Distributed key-value store
5. **Custom gRPC registry** - Purpose-built for Toska Mesh

## Decision

We chose **Consul** as the primary service discovery mechanism:

1. **Feature-Rich**: Service discovery, health checks, KV store, and service mesh capabilities
2. **Platform Agnostic**: Works in Docker, Kubernetes, VMs, and bare metal
3. **Mature**: Production-proven at scale
4. **Multi-Datacenter**: Built-in support for geo-distributed deployments
5. **Good .NET Support**: Consul NuGet package and Steeltoe integration

Additionally, we created a **gRPC-based registry** (`ToskaMesh.Discovery`) as an alternative:
- Lighter weight for simple deployments
- No external dependencies
- Better suited for development/testing

## Consequences

### Positive
- Industry-standard service discovery
- Rich health checking capabilities
- UI for visibility and debugging
- Can leverage Consul Connect for mTLS (future)

### Negative
- Additional infrastructure component to deploy
- Consul agent required on each node (or connect to remote Consul)
- Learning curve for Consul configuration

### Implementation Notes
- `ConsulServiceRegistry` implements `IServiceRegistry` interface
- `GrpcServiceRegistry` provides alternative implementation
- Services can be configured to use either via `ServiceRegistryProvider` enum
- Registration times tracked locally (Consul doesn't store this natively)
