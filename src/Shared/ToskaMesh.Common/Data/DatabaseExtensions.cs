using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ToskaMesh.Common.Data;

/// <summary>
/// Extension methods for configuring database connections.
/// </summary>
public static class DatabaseExtensions
{
    /// <summary>
    /// Adds PostgreSQL DbContext to the service collection.
    /// </summary>
    public static IServiceCollection AddPostgres<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringKey = "DefaultConnection")
        where TContext : DbContext
    {
        var connectionString = configuration.GetConnectionString(connectionStringKey);

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException($"Connection string '{connectionStringKey}' not found.");
        }

        services.AddDbContext<TContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorCodesToAdd: null);

                npgsqlOptions.CommandTimeout(30);
            });

            // Enable sensitive data logging only in development
            options.EnableSensitiveDataLogging(false);
            options.EnableDetailedErrors(true);
        });

        return services;
    }

    /// <summary>
    /// Gets a PostgreSQL connection from the connection string.
    /// </summary>
    public static NpgsqlConnection CreatePostgresConnection(IConfiguration configuration, string connectionStringKey = "DefaultConnection")
    {
        var connectionString = configuration.GetConnectionString(connectionStringKey);

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException($"Connection string '{connectionStringKey}' not found.");
        }

        return new NpgsqlConnection(connectionString);
    }
}

/// <summary>
/// Factory for creating database connections.
/// </summary>
public interface IDatabaseConnectionFactory
{
    NpgsqlConnection CreateConnection();
}

/// <summary>
/// Implementation of database connection factory.
/// </summary>
public class DatabaseConnectionFactory : IDatabaseConnectionFactory
{
    private readonly string _connectionString;

    public DatabaseConnectionFactory(IConfiguration configuration, string connectionStringKey = "DefaultConnection")
    {
        _connectionString = configuration.GetConnectionString(connectionStringKey)
            ?? throw new InvalidOperationException($"Connection string '{connectionStringKey}' not found.");
    }

    public NpgsqlConnection CreateConnection()
    {
        return new NpgsqlConnection(_connectionString);
    }
}
