using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Versioning;
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
var corsConfig = builder.Configuration.GetSection(CorsConfiguration.SectionName).Get<CorsConfiguration>()
    ?? new CorsConfiguration();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("X-API-Version"),
        new MediaTypeApiVersionReader("x-api-version"),
        new QueryStringApiVersionReader("api-version"));
});
builder.Services.AddVersionedApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});
builder.Services.ConfigureOptions<ConfigureSwaggerOptions>();

// Add Mesh common services
builder.Services.AddMeshInfrastructure(
    builder.Configuration,
    configureOptions: options =>
    {
        options.EnableMassTransit = false;
        options.EnableRedisCache = false;
        options.ServiceRegistryProvider = ServiceRegistryProvider.Grpc;
    },
    configureHealthChecks: health =>
    {
        health.AddConsul(options =>
        {
            options.HostName = "consul";
            options.Port = 8500;
        });
    });

builder.Services.AddMeshTelemetry("Gateway");

// Add health checks
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
        if (corsConfig.AllowAnyOrigin || corsConfig.AllowedOrigins.Length == 0)
        {
            policy.AllowAnyOrigin();
        }
        else
        {
            policy.WithOrigins(corsConfig.AllowedOrigins)
                  .AllowCredentials();
        }

        if (corsConfig.AllowedHeaders.Length == 0 || corsConfig.AllowedHeaders.Contains("*"))
        {
            policy.AllowAnyHeader();
        }
        else
        {
            policy.WithHeaders(corsConfig.AllowedHeaders);
        }

        if (corsConfig.AllowedMethods.Length == 0 || corsConfig.AllowedMethods.Contains("*"))
        {
            policy.AllowAnyMethod();
        }
        else
        {
            policy.WithMethods(corsConfig.AllowedMethods);
        }
    });
});

var app = builder.Build();
var apiVersionDescriptionProvider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();

// Configure the HTTP request pipeline
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    foreach (var description in apiVersionDescriptionProvider.ApiVersionDescriptions)
    {
        options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", $"Gateway {description.GroupName.ToUpperInvariant()}");
    }
});

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

// Use mesh health checks (JSON formatted responses)
app.UseMeshHealthChecks();

// Map endpoints
app.MapControllers();
app.MapReverseProxy();

app.Run();
