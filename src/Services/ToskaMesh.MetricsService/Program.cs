using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Prometheus;
using System.Text;
using ToskaMesh.Common.Data;
using ToskaMesh.Common.Extensions;
using ToskaMesh.Common.Health;
using ToskaMesh.MetricsService.Data;
using ToskaMesh.MetricsService.Services;
using ToskaMesh.Security;
using ToskaMesh.Telemetry;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddMeshInfrastructure(builder.Configuration, options =>
{
    options.EnableConsulServiceRegistry = false;
    options.EnableMassTransit = false;
    options.EnableRedisCache = false;
    options.EnableHealthChecks = false;
    options.ConfigureDatabase = (services, configuration) => services.AddPostgres<MetricsDbContext>(configuration);
});
builder.Services.AddMeshTelemetry("MetricsService");
builder.Services.AddMeshHealthChecks();
builder.Services.AddSingleton<IMetricsRegistry, MetricsRegistry>();
builder.Services.AddScoped<IMetricHistoryService, MetricHistoryService>();
builder.Services.AddScoped<ICustomMetricService, CustomMetricService>();
builder.Services.AddScoped<IAlertRuleService, AlertRuleService>();
builder.Services.AddScoped<IGrafanaProvisioningService, GrafanaProvisioningService>();

var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtTokenOptions>() ?? new JwtTokenOptions();
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret));

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
app.MapMetrics();

app.Run();
