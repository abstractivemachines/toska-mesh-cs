namespace ToskaMesh.AuthService.Services;

public interface IEmailSender
{
    Task SendAsync(string email, string subject, string body, CancellationToken cancellationToken = default);
}

public class ConsoleEmailSender : IEmailSender
{
    private readonly ILogger<ConsoleEmailSender> _logger;
    private readonly IHostEnvironment _environment;

    public ConsoleEmailSender(ILogger<ConsoleEmailSender> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public Task SendAsync(string email, string subject, string body, CancellationToken cancellationToken = default)
    {
        if (_environment.IsDevelopment())
        {
            _logger.LogInformation("Email to {Email}: {Subject}\n{Body}", email, subject, body);
        }
        else
        {
            _logger.LogInformation("Email to {Email}: {Subject}", email, subject);
        }
        return Task.CompletedTask;
    }
}
