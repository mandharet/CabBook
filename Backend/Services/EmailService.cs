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
    private readonly IConfiguration _configuration;
    private readonly string _fromEmail;
    private readonly string _fromName;

    public EmailService(ILogger<EmailService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _fromEmail = _configuration["Email:FromEmail"] ?? "noreply@cabbook.com";
        _fromName = _configuration["Email:FromName"] ?? "CabBook";
    }

    public async Task SendUserApprovedEmailAsync(string email, string name)
    {
        var emailEnabled = _configuration.GetValue<bool>("Email:Enabled");
        var message = $"[{_fromName}] User Approved → {email}: Welcome to CabBook! Your account has been approved. You can now login with your email and OTP.";

        if (emailEnabled)
        {
            // TODO: Implement real SMTP integration
            _logger.LogInformation($"Sending email: {message}");
        }
        else
        {
            _logger.LogInformation($"[Email Disabled] {message}");
        }

        await Task.CompletedTask;
    }

    public async Task SendAddressApprovedEmailAsync(string email, string name)
    {
        var emailEnabled = _configuration.GetValue<bool>("Email:Enabled");
        var message = $"[{_fromName}] Addresses Approved → {email}: Your pickup and dropoff addresses have been verified.";

        if (emailEnabled)
        {
            // TODO: Implement real SMTP integration
            _logger.LogInformation($"Sending email: {message}");
        }
        else
        {
            _logger.LogInformation($"[Email Disabled] {message}");
        }

        await Task.CompletedTask;
    }

    public async Task SendUserRejectedEmailAsync(string email, string reason)
    {
        var emailEnabled = _configuration.GetValue<bool>("Email:Enabled");
        var message = $"[{_fromName}] User Rejected → {email}: {reason}";

        if (emailEnabled)
        {
            // TODO: Implement real SMTP integration
            _logger.LogInformation($"Sending email: {message}");
        }
        else
        {
            _logger.LogInformation($"[Email Disabled] {message}");
        }

        await Task.CompletedTask;
    }
}
