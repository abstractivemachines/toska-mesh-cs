using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ToskaMesh.Protocols;
using ToskaMesh.Runtime;
using Xunit;

namespace ToskaMesh.Runtime.Tests;

public class LambdaStyleServiceTests
{
    [Fact]
    public async Task Minimal_lambda_style_service_can_be_configured_with_mesh_defaults()
    {
        // Arrange: create a minimal WebApplication with mesh runtime enabled and a lambda endpoint.
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddMeshService(builder.Configuration, options =>
        {
            options.ServiceName = "orders-api";
            options.Port = 7070;
            options.Metadata["lb_strategy"] = "RoundRobin";
        });
        builder.Services.AddSingleton<IServiceRegistry, StubServiceRegistry>();

        var app = builder.Build();
        app.MapGet("/hello", () => "world");
        app.UseMeshDefaults();

        // Act: start and stop the app to exercise registration hooks.
        await app.StartAsync();
        var options = app.Services.GetRequiredService<MeshServiceOptions>();
        var registrar = app.Services.GetServices<IHostedService>().OfType<MeshAutoRegistrar>().Single();
        await app.StopAsync();

        // Assert: options reflect user intent and auto-registrar is present.
        Assert.Equal("orders-api", options.ServiceName);
        Assert.Equal(7070, options.Port);
        Assert.Equal("RoundRobin", options.Metadata["lb_strategy"]);
        Assert.NotNull(registrar);
    }

    private sealed class StubServiceRegistry : IServiceRegistry
    {
        public Task<bool> DeregisterAsync(string serviceId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<IEnumerable<string>> GetAllServicesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IEnumerable<string>>(Array.Empty<string>());

        public Task<ServiceInstance?> GetServiceInstanceAsync(string serviceId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ServiceInstance?>(null);

        public Task<IEnumerable<ServiceInstance>> GetServiceInstancesAsync(string serviceName, CancellationToken cancellationToken = default) =>
            Task.FromResult<IEnumerable<ServiceInstance>>(Array.Empty<ServiceInstance>());

        public Task<ServiceRegistrationResult> RegisterAsync(ServiceRegistration registration, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ServiceRegistrationResult(true, registration.ServiceId));

        public Task<bool> UpdateHealthStatusAsync(string serviceId, HealthStatus status, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    [Fact]
    public async Task Integration_style_minimal_service_can_handle_requests_via_test_server()
    {
        var registry = new RecordingServiceRegistry();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddMeshService(builder.Configuration, options =>
        {
            options.ServiceName = "hello-api";
            options.Port = 6000;
        });
        builder.Services.AddSingleton<IServiceRegistry>(registry);

        var app = builder.Build();
        app.MapGet("/hello", () => Results.Ok(new { message = "hi" }));
        app.UseMeshDefaults();

        await app.StartAsync();
        var server = app.GetTestServer();
        var client = server.CreateClient();

        var response = await client.GetAsync("/hello");
        var content = await response.Content.ReadAsStringAsync();

        await app.StopAsync();

        Assert.True(response.IsSuccessStatusCode);
        Assert.Contains("\"message\":\"hi\"", content);
        Assert.Single(registry.Registrations);
        Assert.Equal("hello-api", registry.Registrations[0].ServiceName);
    }

    private sealed class RecordingServiceRegistry : IServiceRegistry
    {
        public List<ServiceRegistration> Registrations { get; } = new();

        public Task<ServiceRegistrationResult> RegisterAsync(ServiceRegistration registration, CancellationToken cancellationToken = default)
        {
            Registrations.Add(registration);
            return Task.FromResult(new ServiceRegistrationResult(true, registration.ServiceId));
        }

        public Task<bool> DeregisterAsync(string serviceId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<IEnumerable<ServiceInstance>> GetServiceInstancesAsync(string serviceName, CancellationToken cancellationToken = default) =>
            Task.FromResult<IEnumerable<ServiceInstance>>(Array.Empty<ServiceInstance>());

        public Task<ServiceInstance?> GetServiceInstanceAsync(string serviceId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ServiceInstance?>(null);

        public Task<IEnumerable<string>> GetAllServicesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IEnumerable<string>>(Array.Empty<string>());

        public Task<bool> UpdateHealthStatusAsync(string serviceId, HealthStatus status, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }
}
