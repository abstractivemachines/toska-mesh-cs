using Microsoft.AspNetCore.Mvc;
using ToskaMesh.Common.Extensions;
using ToskaMesh.Common.Health;
using ToskaMesh.HealthMonitor.Configuration;
using ToskaMesh.HealthMonitor.Services;
using ToskaMesh.Telemetry;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<HealthMonitorOptions>(builder.Configuration.GetSection(HealthMonitorOptions.SectionName));
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMeshInfrastructure(builder.Configuration, options =>
{
    options.EnableMassTransit = false;
    options.EnableRedisCache = false;
});
builder.Services.AddMeshTelemetry(builder.Configuration, "HealthMonitor");
builder.Services.AddHttpClient();
builder.Services.AddSingleton<HealthReportCache>();
builder.Services.AddHostedService<HealthProbeWorker>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMeshHealthChecks();
app.UseOpenTelemetryPrometheusScrapingEndpoint();

app.MapControllers();

app.Run();
