using ToskaMesh.Common.Extensions;
using ToskaMesh.Common.Health;
using ToskaMesh.Common.Messaging;
using ToskaMesh.Common.ServiceDiscovery;
using ToskaMesh.Discovery.Services;
using ToskaMesh.Telemetry;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Toska Mesh Discovery Service", Version = "v1" });
});

builder.Services.AddMeshInfrastructure(
    builder.Configuration,
    configureOptions: options =>
    {
        options.EnableMassTransit = true;
        options.EnableRedisCache = false;
    },
    configureHealthChecks: health =>
    {
        health.AddConsul(options =>
        {
            options.HostName = "localhost";
            options.Port = 8500;
        });
    });

// Add discovery service manager
builder.Services.AddSingleton<IServiceManager, ServiceManager>();
builder.Services.AddHostedService<ServiceDiscoveryBackgroundService>();

// Add telemetry
builder.Services.AddMeshTelemetry("Discovery");

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
app.UseSwagger();
app.UseSwaggerUI();

app.UseGlobalExceptionHandler();
app.UseCors();
app.UseMeshHealthChecks();
app.UseRouting();
app.MapControllers();

app.Logger.LogInformation("Toska Mesh Discovery Service starting...");

app.Run();
