using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ToskaMesh.Protocols;
using ToskaMesh.Runtime;
using Xunit;

namespace ToskaMesh.Runtime.Tests;

public class MeshServiceHostTests
{
    [Fact]
    public async Task Minimal_service_can_run_in_memory_and_handle_requests()
    {
        var registry = new RecordingServiceRegistry();

        await using var handle = await MeshServiceHost.StartAsync(
            app =>
            {
                app.MapGet("/hello", () => Results.Ok(new { message = "hi" }));
            },
            options =>
            {
                options.ServiceName = "hello-api";
                options.Port = 0; // use ephemeral port
            },
            services =>
            {
                services.AddSingleton<IServiceRegistry>(registry);
            });

        var response = await handle.Client!.GetAsync("/hello");
        var content = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode);
        Assert.Contains("\"message\":\"hi\"", content);
        Assert.Single(registry.Registrations);
        Assert.Equal("hello-api", registry.Registrations[0].ServiceName);
    }

    [Fact]
    public async Task Options_are_applied_when_running_in_memory()
    {
        await using var handle = await MeshServiceHost.StartAsync(
            app =>
            {
                app.MapGet("/ping", () => "pong");
            },
            options =>
            {
                options.ServiceName = "orders-api";
                options.Port = 6001;
                options.Metadata["lb_strategy"] = "RoundRobin";
            });

        var opts = handle.Services.GetRequiredService<MeshServiceOptions>();
        Assert.Equal("orders-api", opts.ServiceName);
        Assert.Equal(6001, opts.Port);
        Assert.Equal("RoundRobin", opts.Metadata["lb_strategy"]);
    }

    [Fact]
    public async Task Custom_middleware_runs_before_route()
    {
        await using var handle = await MeshServiceHost.StartAsync(
            app =>
            {
                app.Use(async (ctx, next) =>
                {
                    ctx.Response.Headers.Add("x-mesh-middleware", "yes");
                    await next();
                });
                app.MapGet("/hello", () => "hi");
            },
            options =>
            {
                options.ServiceName = "middleware-api";
                options.Port = 0;
            });

        var response = await handle.Client.GetAsync("/hello");
        var header = response.Headers.GetValues("x-mesh-middleware").FirstOrDefault();

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal("yes", header);
    }

    [Fact]
    public async Task Fails_fast_when_no_registry_and_noop_not_allowed()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await MeshServiceHost.StartAsync(
                app => { app.MapGet("/hello", () => "hi"); },
                options =>
                {
                    options.ServiceName = "no-registry";
                    options.Port = 0;
                    options.AllowNoopServiceRegistry = false;
                });
        });
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
