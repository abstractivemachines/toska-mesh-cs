namespace ToskaMesh.AuthService.Services;

public interface IEmailSender
{
    Task SendAsync(string email, string subject, string body, CancellationToken cancellationToken = default);
}

public class ConsoleEmailSender : IEmailSender
{
    private readonly ILogger<ConsoleEmailSender> _logger;

    public ConsoleEmailSender(ILogger<ConsoleEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string email, string subject, string body, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Email to {Email}: {Subject}\n{Body}", email, subject, body);
        return Task.CompletedTask;
    }
}
