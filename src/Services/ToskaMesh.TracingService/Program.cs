using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Text;
using ToskaMesh.Common.Data;
using ToskaMesh.Common.Extensions;
using ToskaMesh.Common.Health;
using ToskaMesh.Security;
using ToskaMesh.Telemetry;
using ToskaMesh.TracingService.Data;
using ToskaMesh.TracingService.Models;
using ToskaMesh.TracingService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddMeshInfrastructure(builder.Configuration, options =>
{
    options.EnableConsulServiceRegistry = false;
    options.EnableMassTransit = false;
    options.EnableRedisCache = false;
    options.ConfigureDatabase = (services, configuration) => services.AddPostgres<TracingDbContext>(configuration);
});
builder.Services.AddMeshTelemetry("TracingService");
builder.Services.AddMeshHealthChecks();

builder.Services.AddScoped<ITraceStorageService, TraceStorageService>();
builder.Services.AddScoped<ITraceAnalyticsService, TraceAnalyticsService>();

var tracingOptions = builder.Configuration.GetSection("Tracing").Get<TracingExporterOptions>() ?? new TracingExporterOptions();
builder.Services.AddSingleton(tracingOptions);

builder.Services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder =>
    {
        tracerProviderBuilder
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("TracingService"))
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .SetSampler(new TraceIdRatioBasedSampler(Math.Clamp(tracingOptions.SamplingRatio, 0.0, 1.0)));

        if (tracingOptions.EnableConsoleExporter)
        {
            tracerProviderBuilder.AddConsoleExporter();
        }

        if (tracingOptions.Jaeger?.Enabled == true)
        {
            tracerProviderBuilder.AddJaegerExporter(options =>
            {
                options.AgentHost = tracingOptions.Jaeger.Host;
                options.AgentPort = tracingOptions.Jaeger.Port;
            });
        }

        if (tracingOptions.Zipkin?.Enabled == true)
        {
            tracerProviderBuilder.AddZipkinExporter(options =>
            {
                options.Endpoint = new Uri(tracingOptions.Zipkin.Endpoint);
            });
        }
    });

var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtTokenOptions>() ?? new JwtTokenOptions();
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret));
builder.Services.AddSingleton(new JwtTokenService(jwtOptions));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = signingKey
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseMeshHealthChecks();
app.UseAuthentication();
app.UseAuthorization();
app.UseOpenTelemetryPrometheusScrapingEndpoint();
app.MapControllers();

app.Run();
