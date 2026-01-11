# Migration Guide: v0.1 to v0.2

This guide covers breaking changes and migration steps for upgrading from ToskaMesh v0.1 to v0.2.

## Breaking Changes Summary

1. **Removed `MeshService` base class** - Stateless services must migrate to `MeshLambdaService`
2. **Removed `MeshStatefulService` base class** - Stateful services must migrate to `MeshStatefulLambdaService`
3. **Removed CLI `--style` flag** - The `toska init --style base` option is no longer available

## Migrating from MeshService to MeshLambdaService

The `MeshService` base class has been removed in favor of the lambda-style `MeshLambdaService`. This provides a more concise API and better aligns with modern .NET minimal API patterns.

### Before (v0.1 - Base Class Style)

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ToskaMesh.Common.ServiceDiscovery;
using ToskaMesh.Protocols;
using ToskaMesh.Runtime;

await MeshService.RunAsync<AdderService>();

public sealed class AdderService : MeshService
{
    private readonly IConfiguration _configuration;

    public AdderService()
    {
        _configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
    }

    public override void ConfigureOptions(MeshServiceOptions options)
    {
        options.ServiceName ??= "adder-mesh-service";
        options.Address ??= "0.0.0.0";
        options.Port = options.Port == 0 ? 8083 : options.Port;
        options.Routing.HealthCheckEndpoint ??= "/health";
        if (options.Routing.Strategy == default)
        {
            options.Routing.Strategy = LoadBalancingStrategy.RoundRobin;
        }
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddGrpcServiceRegistry(_configuration);
    }

    public override void ConfigureApp(MeshServiceApp app)
    {
        app.MapGet("/health", () => Results.Ok("ok"));

        app.MapGet("/add", (double a, double b) =>
            Results.Ok(new { a, b, sum = a + b }));

        app.MapPost("/add", (AddRequest request) =>
            Results.Ok(new { request.A, request.B, sum = request.A + request.B }));
    }
}

public sealed record AddRequest(double A, double B);
```

### After (v0.2 - Lambda Style)

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ToskaMesh.Common.ServiceDiscovery;
using ToskaMesh.Protocols;
using ToskaMesh.Runtime;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

await MeshLambdaService.RunAsync(
    app =>
    {
        app.MapGet("/health", () => Results.Ok("ok"));

        // GET /add?a=1&b=2
        app.MapGet("/add", (double a, double b) =>
            Results.Ok(new { a, b, sum = a + b }));

        // POST /add  { "a": 1, "b": 2 }
        app.MapPost("/add", (AddRequest request) =>
            Results.Ok(new { request.A, request.B, sum = request.A + request.B }));
    },
    options =>
    {
        options.ServiceName ??= "adder-mesh-service";
        options.Address ??= "0.0.0.0";
        options.Port = options.Port == 0 ? 8083 : options.Port;
        options.Routing.HealthCheckEndpoint ??= "/health";
        if (options.Routing.Strategy == default)
        {
            options.Routing.Strategy = LoadBalancingStrategy.RoundRobin;
        }
    },
    services =>
    {
        var provider = configuration.GetValue<string>("Mesh:Service:ServiceRegistryProvider");
        if (string.Equals(provider, "consul", StringComparison.OrdinalIgnoreCase))
        {
            services.AddConsulServiceRegistry(configuration);
        }
        else
        {
            services.AddGrpcServiceRegistry(configuration);
        }
    });

public sealed record AddRequest(double A, double B);
```

### Migration Steps

1. **Remove the class inheritance**: Delete `public sealed class YourService : MeshService`
2. **Move configuration to top-level**: Extract `IConfiguration` setup to the top of the file
3. **Replace `RunAsync<T>()` with lambda `RunAsync()`**:
   - First parameter: `Action<MeshServiceApp>` - your `ConfigureApp` logic
   - Second parameter: `Action<MeshServiceOptions>` - your `ConfigureOptions` logic  
   - Third parameter: `Action<IServiceCollection>` - your `ConfigureServices` logic
4. **Update any field references**: Replace `_configuration` field access with the local `configuration` variable

## Migrating from MeshStatefulService to MeshStatefulLambdaService

The same pattern applies to stateful services.

### Before (v0.1)

```csharp
await MeshStatefulService.RunAsync<MyStatefulService>();

public sealed class MyStatefulService : MeshStatefulService
{
    public override void ConfigureStateful(StatefulHostOptions options)
    {
        options.ServiceName = "inventory-stateful";
        options.Orleans.ClusterId = "prod-cluster";
    }

    public override void ConfigureOptions(MeshServiceOptions options)
    {
        options.ServiceName = "inventory-stateful";
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        // Custom services
    }
}
```

### After (v0.2)

```csharp
await MeshStatefulLambdaService.RunAsync(
    configureStateful: stateful =>
    {
        stateful.ServiceName = "inventory-stateful";
        stateful.Orleans.ClusterId = "prod-cluster";
    },
    configureService: options =>
    {
        options.ServiceName = "inventory-stateful";
    },
    configureServices: services =>
    {
        // Custom services
    });
```

## CLI Scaffolding Changes

The `--style` flag has been removed from `toska init`. All stateless services now use the lambda style.

### Before (v0.1)

```bash
toska init my-service --type stateless --style base
toska init my-service --type stateless --style lambda
```

### After (v0.2)

```bash
toska init my-service --type stateless
```

## New Feature: AWS SNS/SQS Transport

v0.2 adds AWS SNS/SQS as an alternative messaging transport to RabbitMQ.

### Configuration

```json
{
  "Messaging": {
    "Transport": "AwsSqs",
    "AwsSqs": {
      "Region": "us-east-1",
      "Scope": "dev"
    }
  }
}
```

### Security Best Practices

**Prefer IAM roles over explicit credentials.** The AWS SDK automatically uses the default credential provider chain when explicit credentials are not configured:

1. Environment variables (`AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`)
2. Shared credentials file (`~/.aws/credentials`)
3. IAM roles for EC2/ECS/EKS

Only use explicit `AccessKeyId` and `SecretAccessKey` for local development with LocalStack:

```json
{
  "Messaging": {
    "Transport": "AwsSqs",
    "AwsSqs": {
      "Region": "us-east-1",
      "ServiceUrl": "http://localhost:4566",
      "AccessKeyId": "test",
      "SecretAccessKey": "test"
    }
  }
}
```

See [evented-communication.md](evented-communication.md) for full transport configuration details.

## Validation Changes

Configuration is now validated at startup. Invalid configurations will throw `MessagingConfigurationException` with descriptive error messages:

- Missing required fields (e.g., AWS Region)
- Partial credential configuration (AccessKeyId without SecretAccessKey)
- Invalid ServiceUrl format

## Questions?

- See [MeshLambdaService quickstart](meshlambdaservice-quickstart.md) for complete examples
- See [evented-communication.md](evented-communication.md) for messaging transport details
- Check `examples/` directory for runnable samples
