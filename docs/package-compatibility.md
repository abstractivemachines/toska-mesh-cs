# Package Version Compatibility

This document describes version compatibility requirements for ToskaMesh dependencies.

## Messaging Packages

### MassTransit + AWS SDK Compatibility

| MassTransit Version | AWSSDK.Core Version | MassTransit.AmazonSQS | Status |
|---------------------|---------------------|----------------------|--------|
| 8.5.x               | 4.0.x               | 8.5.x                | Current |
| 8.5.x               | 3.7.x               | 8.5.x                | Compatible |
| 8.4.x               | 3.7.x               | 8.4.x                | Compatible |

**Current Configuration:**
- `MassTransit`: 8.5.7
- `MassTransit.AmazonSQS`: 8.5.7
- `MassTransit.RabbitMQ`: 8.5.7
- `AWSSDK.Core`: 4.0.3.8

**Important:** All MassTransit packages must use the same minor version (e.g., all 8.5.x).

### AWS SDK Notes

The AWS SDK v4 introduced breaking changes from v3. Key differences:

- **Credential handling**: v4 uses `Amazon.Runtime.Credentials` namespace
- **Async patterns**: Improved async/await support
- **Configuration**: New configuration patterns for service clients

When using LocalStack for development:
```json
{
  "Messaging": {
    "Transport": "AwsSqs",
    "AwsSqs": {
      "Region": "us-east-1",
      "ServiceUrl": "http://localhost:4566"
    }
  }
}
```

## Orleans Packages

### Orleans Version Matrix

| Orleans Version | .NET Version | Status |
|-----------------|--------------|--------|
| 9.2.x           | .NET 10      | Current |
| 9.1.x           | .NET 9/10    | Compatible |
| 8.x             | .NET 8       | Previous LTS |

**Current Configuration:**
- `Microsoft.Orleans.*`: 9.2.1
- `OrleansDashboard`: 8.2.0

**Important:** All `Microsoft.Orleans.*` packages must use the same version.

### Orleans Dashboard Compatibility

OrleansDashboard may lag behind Orleans releases. Version 8.2.0 is compatible with Orleans 9.x but some features may be limited.

## OpenTelemetry Packages

### Version Requirements

| Package | Version | Notes |
|---------|---------|-------|
| OpenTelemetry | 1.14.0 | Core library |
| OpenTelemetry.Extensions.Hosting | 1.14.0 | Must match core |
| OpenTelemetry.Instrumentation.* | 1.14.0 | Must match core |
| OpenTelemetry.Exporter.* | 1.14.0 | Should match core |

**Exception:** `OpenTelemetry.Exporter.Prometheus.AspNetCore` uses `1.7.0-rc.1` due to ASP.NET Core specific requirements.

## gRPC Packages

### Version Requirements

| Package | Version |
|---------|---------|
| Grpc.AspNetCore | 2.76.0 |
| Grpc.Net.Client | 2.76.0 |
| Grpc.Net.ClientFactory | 2.76.0 |
| Grpc.Tools | 2.76.0 |
| Google.Protobuf | 3.33.2 |

**Important:** All Grpc.* packages should use the same version.

## Updating Packages

### Safe Update Process

1. **Check compatibility matrix** above before updating
2. **Update related packages together** (e.g., all MassTransit packages)
3. **Run tests** after updates: `dotnet test`
4. **Verify LocalStack integration** if using AWS transport

### Version Constraints

When updating `Directory.Packages.props`:

```xml
<!-- MassTransit - update all together -->
<PackageVersion Include="MassTransit" Version="8.5.7" />
<PackageVersion Include="MassTransit.AmazonSQS" Version="8.5.7" />
<PackageVersion Include="MassTransit.RabbitMQ" Version="8.5.7" />

<!-- Orleans - update all together -->
<PackageVersion Include="Microsoft.Orleans.Client" Version="9.2.1" />
<PackageVersion Include="Microsoft.Orleans.Server" Version="9.2.1" />
<!-- ... all other Orleans packages ... -->
```

## Troubleshooting

### AWS SDK Version Conflicts

If you see errors like `Could not load type 'Amazon.Runtime.AWSCredentials'`:
1. Ensure `AWSSDK.Core` version matches what `MassTransit.AmazonSQS` expects
2. Clean and rebuild: `dotnet clean && dotnet build`
3. Check for transitive version conflicts: `dotnet list package --include-transitive`

### MassTransit Version Mismatch

If you see `MassTransit.Topology` or similar errors:
1. Ensure all MassTransit packages use the same version
2. Check `Directory.Packages.props` for inconsistencies

### Orleans Silo Startup Failures

If Orleans fails to start with version errors:
1. Ensure all `Microsoft.Orleans.*` packages match
2. Check clustering provider version compatibility

## Testing Compatibility

### Integration Tests

Run messaging integration tests to verify transport compatibility:

```bash
# RabbitMQ transport
dotnet test --filter "Category=Integration&Transport=RabbitMQ"

# AWS SQS transport (requires LocalStack)
docker run -d -p 4566:4566 localstack/localstack
dotnet test --filter "Category=Integration&Transport=AwsSqs"
```

### Version Verification Script

```bash
# List all package versions
dotnet list package --format json | jq '.projects[].frameworks[].topLevelPackages[]'

# Check for outdated packages
dotnet list package --outdated
```
