namespace ToksaMesh.Common.Utilities;

/// <summary>
/// Retry policy configuration for resilient operations
/// </summary>
public class RetryPolicyOptions
{
    public int MaxRetries { get; set; } = 3;
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMilliseconds(100);
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);
    public double BackoffMultiplier { get; set; } = 2.0;
    public bool UseJitter { get; set; } = true;
}

/// <summary>
/// Utility class for executing operations with retry logic
/// </summary>
public static class RetryPolicy
{
    /// <summary>
    /// Execute an async operation with exponential backoff retry
    /// </summary>
    public static async Task<T> ExecuteAsync<T>(
        Func<Task<T>> operation,
        RetryPolicyOptions? options = null,
        Func<Exception, bool>? shouldRetry = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new RetryPolicyOptions();
        shouldRetry ??= _ => true;

        var attempt = 0;
        var delay = options.InitialDelay;

        while (true)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (attempt < options.MaxRetries && shouldRetry(ex))
            {
                attempt++;

                if (options.UseJitter)
                {
                    var jitter = Random.Shared.Next(0, (int)delay.TotalMilliseconds / 2);
                    delay = delay.Add(TimeSpan.FromMilliseconds(jitter));
                }

                await Task.Delay(delay, cancellationToken);

                delay = TimeSpan.FromMilliseconds(
                    Math.Min(delay.TotalMilliseconds * options.BackoffMultiplier,
                             options.MaxDelay.TotalMilliseconds));
            }
        }
    }

    /// <summary>
    /// Execute a void async operation with exponential backoff retry
    /// </summary>
    public static async Task ExecuteAsync(
        Func<Task> operation,
        RetryPolicyOptions? options = null,
        Func<Exception, bool>? shouldRetry = null,
        CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(async () =>
        {
            await operation();
            return true;
        }, options, shouldRetry, cancellationToken);
    }
}
