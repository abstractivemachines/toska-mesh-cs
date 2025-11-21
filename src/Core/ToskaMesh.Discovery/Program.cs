using ToskaMesh.Common.Extensions;
using ToskaMesh.Common.Health;
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

// Add Toska Mesh common services
builder.Services.AddMeshCommon();

// Add Consul service registry
builder.Services.AddConsulServiceRegistry(builder.Configuration);

// Add discovery service manager
builder.Services.AddSingleton<IServiceManager, ServiceManager>();
builder.Services.AddHostedService<ServiceDiscoveryBackgroundService>();

// Add telemetry
builder.Services.AddMeshTelemetry("Discovery");

// Add health checks
builder.Services.AddMeshHealthChecks()
    .AddConsul(options =>
    {
        options.HostName = builder.Configuration["Consul:Address"] ?? "http://localhost:8500";
    });

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

app.UseGlobalExceptionHandler();
app.UseCors();
app.UseMeshHealthChecks();
app.UseRouting();
app.MapControllers();

app.Logger.LogInformation("Toska Mesh Discovery Service starting...");

app.Run();
