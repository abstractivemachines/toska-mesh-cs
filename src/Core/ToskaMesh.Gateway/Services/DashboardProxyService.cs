using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ToskaMesh.Common;
using ToskaMesh.Gateway.Models;
using ToskaMesh.Protocols;
using ToskaMesh.Security;

namespace ToskaMesh.Gateway.Services;

public interface IDashboardProxyService
{
    Task<ProxyResponse?> ProxyPrometheusAsync(string pathAndQuery, CancellationToken cancellationToken);
    Task<ProxyResponse?> ProxyTracingAsync(string pathAndQuery, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<DashboardServiceCatalogItem>> GetServiceCatalogAsync(CancellationToken cancellationToken);
}

public sealed class DashboardProxyService : IDashboardProxyService
{
    public const string PrometheusClientName = "Dashboard.Prometheus";
    public const string TracingClientName = "Dashboard.Tracing";
    public const string DiscoveryClientName = "Dashboard.Discovery";
    public const string HealthClientName = "Dashboard.Health";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMeshServiceTokenProvider _tokenProvider;
    private readonly ILogger<DashboardProxyService> _logger;

    public DashboardProxyService(
        IHttpClientFactory httpClientFactory,
        IMeshServiceTokenProvider tokenProvider,
        ILogger<DashboardProxyService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _tokenProvider = tokenProvider;
        _logger = logger;
    }

    public Task<ProxyResponse?> ProxyPrometheusAsync(string pathAndQuery, CancellationToken cancellationToken)
    {
        return ProxyAsync(
            PrometheusClientName,
            pathAndQuery,
            includeAuth: false,
            cancellationToken);
    }

    public Task<ProxyResponse?> ProxyTracingAsync(string pathAndQuery, CancellationToken cancellationToken)
    {
        return ProxyAsync(
            TracingClientName,
            pathAndQuery,
            includeAuth: true,
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<DashboardServiceCatalogItem>> GetServiceCatalogAsync(CancellationToken cancellationToken)
    {
        var serviceNames = await GetServiceNamesAsync(cancellationToken);
        var healthSnapshots = await GetHealthSnapshotsAsync(cancellationToken);

        var results = new List<DashboardServiceCatalogItem>(serviceNames.Count);
        foreach (var serviceName in serviceNames)
        {
            var instancesTask = GetServiceInstancesAsync(serviceName, cancellationToken);
            var metadataTask = GetServiceMetadataSummaryAsync(serviceName, cancellationToken);

            await Task.WhenAll(instancesTask, metadataTask);

            var instances = instancesTask.Result;
            var metadata = metadataTask.Result;
            var serviceHealth = healthSnapshots
                .Where(snapshot => snapshot.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            results.Add(new DashboardServiceCatalogItem(
                serviceName,
                instances,
                serviceHealth,
                metadata));
        }

        return results;
    }

    private async Task<IReadOnlyCollection<string>> GetServiceNamesAsync(CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(DiscoveryClientName);
        var response = await GetApiResponseAsync<IReadOnlyCollection<string>>(
            client,
            "api/ServiceDiscovery/services",
            includeAuth: false,
            cancellationToken);

        return response?.Data ?? Array.Empty<string>();
    }

    private async Task<IReadOnlyCollection<ServiceInstance>> GetServiceInstancesAsync(
        string serviceName,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(DiscoveryClientName);
        var response = await GetApiResponseAsync<IReadOnlyCollection<ServiceInstance>>(
            client,
            $"api/ServiceDiscovery/services/{serviceName}/instances",
            includeAuth: false,
            cancellationToken);

        return response?.Data ?? Array.Empty<ServiceInstance>();
    }

    private async Task<DashboardServiceMetadataSummary?> GetServiceMetadataSummaryAsync(
        string serviceName,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(DiscoveryClientName);
        var response = await GetApiResponseAsync<DashboardServiceMetadataSummary>(
            client,
            $"api/ServiceDiscovery/services/{serviceName}/metadata",
            includeAuth: false,
            cancellationToken);

        return response?.Data;
    }

    private async Task<IReadOnlyCollection<DashboardHealthSnapshot>> GetHealthSnapshotsAsync(CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HealthClientName);
        try
        {
            var response = await client.GetFromJsonAsync<IReadOnlyCollection<DashboardHealthSnapshot>>(
                "api/Status",
                SerializerOptions,
                cancellationToken);

            return response ?? Array.Empty<DashboardHealthSnapshot>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch health snapshots from HealthMonitor");
            return Array.Empty<DashboardHealthSnapshot>();
        }
    }

    private async Task<ProxyResponse?> ProxyAsync(
        string clientName,
        string pathAndQuery,
        bool includeAuth,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient(clientName);
            using var request = new HttpRequestMessage(HttpMethod.Get, pathAndQuery);

            if (includeAuth)
            {
                var token = await _tokenProvider.GetTokenAsync(cancellationToken);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            using var response = await client.SendAsync(request, cancellationToken);
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";

            return new ProxyResponse(payload, contentType, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dashboard proxy request failed for {ClientName} {Path}", clientName, pathAndQuery);
            return null;
        }
    }

    private async Task<ApiResponse<T>?> GetApiResponseAsync<T>(
        HttpClient client,
        string path,
        bool includeAuth,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);

        if (includeAuth)
        {
            var token = await _tokenProvider.GetTokenAsync(cancellationToken);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Dashboard proxy request to {Path} failed with {StatusCode}", path, response.StatusCode);
            return null;
        }

        try
        {
            return await response.Content.ReadFromJsonAsync<ApiResponse<T>>(SerializerOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse dashboard API response from {Path}", path);
            return null;
        }
    }
}
