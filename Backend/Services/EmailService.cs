namespace CabBook.Services;

public interface IEmailService
{
    Task SendUserApprovedEmailAsync(string email, string name);
    Task SendAddressApprovedEmailAsync(string email, string name);
    Task SendUserRejectedEmailAsync(string email, string reason);
}

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;

    public EmailService(ILogger<EmailService> logger)
    {
        _logger = logger;
    }

    public async Task SendUserApprovedEmailAsync(string email, string name)
    {
        // TODO: Implement SMTP integration
        // For now, just log the email
        _logger.LogInformation($"Email to {email}: User Approved - Welcome to CabBook! Your account has been approved.");
        await Task.CompletedTask;
    }

    public async Task SendAddressApprovedEmailAsync(string email, string name)
    {
        // TODO: Implement SMTP integration
        _logger.LogInformation($"Email to {email}: Addresses Approved - Your pickup and dropoff addresses have been verified.");
        await Task.CompletedTask;
    }

    public async Task SendUserRejectedEmailAsync(string email, string reason)
    {
        // TODO: Implement SMTP integration
        _logger.LogInformation($"Email to {email}: User Rejected - {reason}");
        await Task.CompletedTask;
    }
}
