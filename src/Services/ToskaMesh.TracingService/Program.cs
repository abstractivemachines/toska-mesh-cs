using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using ToskaMesh.Common.Data;
using ToskaMesh.Common.Extensions;
using ToskaMesh.Common.Health;
using ToskaMesh.Security;
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
    options.EnableHealthChecks = false;
    options.ConfigureDatabase = (services, configuration) => services.AddPostgres<TracingDbContext>(configuration);
});
// TracingService is infrastructure - don't trace itself to avoid feedback loop
// builder.Services.AddMeshTelemetry(builder.Configuration, "TracingService");
builder.Services.AddMeshHealthChecks();

builder.Services.AddScoped<ITraceStorageService, TraceStorageService>();
builder.Services.AddScoped<ITraceAnalyticsService, TraceAnalyticsService>();
builder.Services.Configure<TraceQueryDefaultsOptions>(builder.Configuration.GetSection("Tracing:QueryDefaults"));
builder.Services.Configure<TraceSummaryRefreshOptions>(builder.Configuration.GetSection("Tracing:SummaryRefresh"));
builder.Services.AddHostedService<TraceSummaryRefreshService>();

// TracingService is infrastructure - it stores traces but doesn't generate its own
// to avoid feedback loops and noise in the trace data
var tracingOptions = builder.Configuration.GetSection("Tracing").Get<TracingExporterOptions>() ?? new TracingExporterOptions();
builder.Services.AddSingleton(tracingOptions);

var serviceAuthOptions = builder.Configuration.GetSection("Mesh:ServiceAuth").Get<MeshServiceAuthOptions>() ?? new MeshServiceAuthOptions();
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(serviceAuthOptions.Secret));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = serviceAuthOptions.Issuer,
            ValidAudience = serviceAuthOptions.Audience,
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
app.MapControllers();

await app.Services.EnsureDatabaseAsync<TracingDbContext>(
    app.Configuration,
    app.Logger,
    app.Lifetime.ApplicationStopping);
await app.Services.EnsureTraceSummariesAsync(app.Logger, app.Lifetime.ApplicationStopping);

app.Run();
