using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ToskaMesh.Common.Extensions;
using ToskaMesh.Common.Health;
using ToskaMesh.Common.ServiceDiscovery;
using ToskaMesh.Gateway.Configuration;
using ToskaMesh.Gateway.Middleware;
using ToskaMesh.Gateway.Services;
using ToskaMesh.Protocols;
using ToskaMesh.Telemetry;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Add configuration
var jwtConfig = builder.Configuration.GetSection(JwtConfiguration.SectionName).Get<JwtConfiguration>()
    ?? new JwtConfiguration();
var rateLimitConfig = builder.Configuration.GetSection(RateLimitConfiguration.SectionName).Get<RateLimitConfiguration>()
    ?? new RateLimitConfiguration();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add Mesh common services (includes Consul, MassTransit, etc.)
builder.Services.AddMeshCommon();

// Add OpenTelemetry
builder.Services.AddMeshTelemetry("Gateway");

// Add health checks
builder.Services.AddMeshHealthChecks();

// Configure JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = jwtConfig.ValidateIssuer,
            ValidateAudience = jwtConfig.ValidateAudience,
            ValidateLifetime = jwtConfig.ValidateLifetime,
            ValidateIssuerSigningKey = jwtConfig.ValidateIssuerSigningKey,
            ValidIssuer = jwtConfig.Issuer,
            ValidAudience = jwtConfig.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtConfig.SecretKey)),
            ClockSkew = TimeSpan.FromMinutes(5)
        };
    });

builder.Services.AddAuthorization();

// Configure Rate Limiting
if (rateLimitConfig.EnableRateLimiting)
{
    builder.Services.AddRateLimiter(options =>
    {
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            // Rate limit by IP address
            var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            return RateLimitPartition.GetFixedWindowLimiter(ipAddress, _ =>
                new FixedWindowRateLimiterOptions
                {
                    PermitLimit = rateLimitConfig.PermitLimit,
                    Window = TimeSpan.FromSeconds(rateLimitConfig.WindowSeconds),
                    QueueLimit = rateLimitConfig.QueueLimit,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                });
        });

        options.OnRejected = async (context, cancellationToken) =>
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.HttpContext.Response.WriteAsync(
                "Too many requests. Please try again later.",
                cancellationToken);
        };
    });
}

// Add YARP reverse proxy with Consul-based configuration
builder.Services.AddSingleton<ConsulProxyConfigProvider>();
builder.Services.AddSingleton<IProxyConfigProvider>(sp => sp.GetRequiredService<ConsulProxyConfigProvider>());
builder.Services.AddReverseProxy()
    .LoadFromMemory(Array.Empty<Yarp.ReverseProxy.Configuration.RouteConfig>(),
                    Array.Empty<Yarp.ReverseProxy.Configuration.ClusterConfig>());

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Middleware ordering is important
app.UseRequestLogging(); // Custom logging middleware

app.UseCors();

app.UseHttpsRedirection();

// Rate limiting
if (rateLimitConfig.EnableRateLimiting)
{
    app.UseRateLimiter();
}

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Enable Prometheus metrics endpoint
app.UseOpenTelemetryPrometheusScrapingEndpoint();

// Map endpoints
app.MapControllers();
app.MapReverseProxy();

// Health check endpoints
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready");
app.MapHealthChecks("/health/live");

app.Run();
