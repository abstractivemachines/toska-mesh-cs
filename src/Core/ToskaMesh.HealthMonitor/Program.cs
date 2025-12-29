using Microsoft.AspNetCore.Mvc;
using ToskaMesh.Common.Extensions;
using ToskaMesh.Common.Health;
using ToskaMesh.HealthMonitor.Configuration;
using ToskaMesh.HealthMonitor.Services;

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
// HealthMonitor is infrastructure - don't trace health check loops
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

app.MapControllers();

app.Run();
