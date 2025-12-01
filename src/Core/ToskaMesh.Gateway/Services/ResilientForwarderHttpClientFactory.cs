using System.IO;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using ToskaMesh.Gateway.Configuration;
using Yarp.ReverseProxy.Forwarder;

namespace ToskaMesh.Gateway.Services;

/// <summary>
/// Wraps YARP's HttpClient factory with retry and circuit-breaker policies plus optional client certificates.
/// </summary>
public class ResilientForwarderHttpClientFactory : ForwarderHttpClientFactory
{
    private readonly GatewayResilienceOptions _resilienceOptions;
    private readonly GatewayTlsOptions _tlsOptions;
    private readonly ILogger<ResilientForwarderHttpClientFactory> _logger;
    private readonly IAsyncPolicy<HttpResponseMessage> _resiliencePipeline;

    public ResilientForwarderHttpClientFactory(
        GatewayResilienceOptions resilienceOptions,
        GatewayTlsOptions tlsOptions,
        ILogger<ResilientForwarderHttpClientFactory> logger)
    {
        _resilienceOptions = resilienceOptions;
        _tlsOptions = tlsOptions;
        _logger = logger;
        _resiliencePipeline = BuildPipeline();
    }

    protected override void ConfigureHandler(ForwarderHttpClientContext context, SocketsHttpHandler handler)
    {
        base.ConfigureHandler(context, handler);

        if (string.IsNullOrWhiteSpace(_tlsOptions.ClientCertificatePath)
            || string.IsNullOrWhiteSpace(_tlsOptions.ClientCertificatePassword)
            || !File.Exists(_tlsOptions.ClientCertificatePath))
        {
            return;
        }

        try
        {
            var clientCertificate = new X509Certificate2(_tlsOptions.ClientCertificatePath, _tlsOptions.ClientCertificatePassword);
            handler.SslOptions ??= new SslClientAuthenticationOptions();
            handler.SslOptions.ClientCertificates ??= new X509CertificateCollection();
            handler.SslOptions.ClientCertificates.Add(clientCertificate);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load gateway client certificate from {Path}", _tlsOptions.ClientCertificatePath);
        }
    }

    protected override HttpMessageHandler WrapHandler(ForwarderHttpClientContext context, HttpMessageHandler handler)
    {
        var policyHandler = new ResilienceHandler(_resiliencePipeline) { InnerHandler = handler };

        return base.WrapHandler(context, policyHandler);
    }

    private IAsyncPolicy<HttpResponseMessage> BuildPipeline()
    {
        TimeSpan BackoffWithJitter(int attempt)
        {
            var exponentialBackoff = _resilienceOptions.RetryBaseDelayMilliseconds
                                     * Math.Pow(_resilienceOptions.RetryBackoffExponent, attempt - 1);

            var jitter = Random.Shared.NextDouble() * _resilienceOptions.RetryJitterMilliseconds;
            return TimeSpan.FromMilliseconds(exponentialBackoff + jitter);
        }

        var retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                _resilienceOptions.RetryCount,
                attempt => BackoffWithJitter(attempt),
                onRetry: (outcome, delay, attempt, _) =>
                {
                    _logger.LogWarning(
                        outcome.Exception,
                        "Retrying upstream request (attempt {Attempt}/{RetryCount}) after {Delay} due to {Reason}",
                        attempt,
                        _resilienceOptions.RetryCount,
                        delay,
                        outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString());
                });

        var circuitBreakerPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .AdvancedCircuitBreakerAsync(
                failureThreshold: _resilienceOptions.CircuitBreakerFailureThreshold,
                samplingDuration: TimeSpan.FromSeconds(_resilienceOptions.CircuitBreakerSamplingDurationSeconds),
                minimumThroughput: _resilienceOptions.CircuitBreakerMinimumThroughput,
                durationOfBreak: TimeSpan.FromSeconds(_resilienceOptions.CircuitBreakerBreakDurationSeconds),
                onBreak: (outcome, breakDelay) =>
                {
                    _logger.LogWarning(
                        outcome.Exception,
                        "Opening upstream circuit for {BreakDelay} after detecting errors ({Reason})",
                        breakDelay,
                        outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString());
                },
                onReset: () => _logger.LogInformation("Upstream circuit closed; proxy calls resumed"),
                onHalfOpen: () => _logger.LogInformation("Upstream circuit half-open; probing health"));

        return Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);
    }

    private sealed class ResilienceHandler : DelegatingHandler
    {
        private readonly IAsyncPolicy<HttpResponseMessage> _policy;

        public ResilienceHandler(IAsyncPolicy<HttpResponseMessage> policy)
        {
            _policy = policy;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _policy.ExecuteAsync(ct => base.SendAsync(request, ct), cancellationToken);
        }
    }
}
