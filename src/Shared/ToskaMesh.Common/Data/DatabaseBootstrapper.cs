using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace ToskaMesh.Common.Data;

public static class DatabaseBootstrapper
{
    private const string SectionName = "Database";

    public static async Task EnsureDatabaseAsync<TContext>(
        this IServiceProvider services,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken cancellationToken = default)
        where TContext : DbContext
    {
        var options = configuration.GetSection(SectionName).Get<DatabaseBootstrapOptions>()
            ?? new DatabaseBootstrapOptions();

        if (!options.AutoMigrate)
        {
            return;
        }

        var delay = TimeSpan.FromSeconds(Math.Max(1, options.AutoMigrateDelaySeconds));
        var maxAttempts = Math.Max(1, options.AutoMigrateMaxAttempts);
        Exception? lastError = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var scope = services.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<TContext>();
                var migrations = await context.Database.GetMigrationsAsync(cancellationToken);

                if (migrations.Any())
                {
                    await context.Database.MigrateAsync(cancellationToken);
                }
                else
                {
                    await context.Database.EnsureCreatedAsync(cancellationToken);
                }

                logger.LogInformation(
                    "Database bootstrap completed for {DbContext} (attempt {Attempt}/{MaxAttempts}).",
                    typeof(TContext).Name,
                    attempt,
                    maxAttempts);
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                lastError = ex;
                logger.LogWarning(
                    ex,
                    "Database bootstrap attempt {Attempt}/{MaxAttempts} failed; retrying in {DelaySeconds}s.",
                    attempt,
                    maxAttempts,
                    delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception ex)
            {
                lastError = ex;
                break;
            }
        }

        if (lastError != null)
        {
            throw new InvalidOperationException(
                $"Database bootstrap failed for {typeof(TContext).Name} after {maxAttempts} attempts.",
                lastError);
        }
    }
}

public sealed class DatabaseBootstrapOptions
{
    public bool AutoMigrate { get; set; }
    public int AutoMigrateMaxAttempts { get; set; } = 5;
    public int AutoMigrateDelaySeconds { get; set; } = 5;
}
