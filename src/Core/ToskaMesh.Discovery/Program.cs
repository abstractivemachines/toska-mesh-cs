using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using ToskaMesh.Common.Extensions;
using ToskaMesh.Common.Health;
using ToskaMesh.Common.Messaging;
using ToskaMesh.Common.ServiceDiscovery;
using ToskaMesh.Discovery.Grpc;
using ToskaMesh.Discovery.Services;
using ToskaMesh.Security;
using ToskaMesh.Telemetry;

var builder = WebApplication.CreateBuilder(args);

var consulHealthConfig = builder.Configuration.GetSection(ConsulHealthCheckOptions.SectionName).Get<ConsulHealthCheckOptions>()
    ?? new ConsulHealthCheckOptions();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddGrpc();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "ToskaMesh Discovery Service", Version = "v1" });
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
            options.HostName = consulHealthConfig.HostName;
            options.Port = consulHealthConfig.Port;
        });
    });

builder.Services.AddHttpClient();

// Add discovery service manager
builder.Services.AddScoped<IServiceManager, ServiceManager>();
builder.Services.AddHostedService<ServiceDiscoveryBackgroundService>();

// Add telemetry
builder.Services.AddMeshTelemetry("Discovery");
builder.Services.AddMeshAuthorizationPolicies();

var serviceAuthOptions = builder.Configuration.GetSection("Mesh:ServiceAuth").Get<MeshServiceAuthOptions>() ?? new MeshServiceAuthOptions();
builder.Services.AddSingleton(serviceAuthOptions);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = serviceAuthOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = serviceAuthOptions.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(serviceAuthOptions.Secret)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

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
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGrpcService<DiscoveryGrpcService>();

app.Logger.LogInformation("ToskaMesh Discovery Service starting...");

app.Run();
