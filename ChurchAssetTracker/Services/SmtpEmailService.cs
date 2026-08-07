using System.Net;
using System.Net.Mail;

namespace ChurchAssetTracker.Services;

public class SmtpEmailService : IEmailService
{
    private readonly SystemSettingsService _systemSettings;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(SystemSettingsService systemSettings, ILogger<SmtpEmailService> logger)
    {
        _systemSettings = systemSettings;
        _logger = logger;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string body)
    {
        var settings = await _systemSettings.GetEmailAsync();

        if (!settings.Enabled)
        {
            _logger.LogInformation("Email disabled. Would have sent to {ToEmail}: {Subject}", toEmail, subject);
            return;
        }

        if (string.IsNullOrWhiteSpace(toEmail))
        {
            _logger.LogWarning("Email skipped because recipient address was blank. Subject: {Subject}", subject);
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.SmtpHost))
            throw new InvalidOperationException("SMTP host is not configured. Open Administration > System Settings > Email.");

        if (string.IsNullOrWhiteSpace(settings.FromEmail))
            throw new InvalidOperationException("Default From address is not configured. Open Administration > System Settings > Email.");

        using var message = new MailMessage
        {
            From = new MailAddress(settings.FromEmail, string.IsNullOrWhiteSpace(settings.FromName) ? settings.FromEmail : settings.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };
        message.To.Add(toEmail);

        using var client = new SmtpClient(settings.SmtpHost, settings.SmtpPort)
        {
            EnableSsl = settings.UseSsl
        };

        if (!string.IsNullOrWhiteSpace(settings.Username))
            client.Credentials = new NetworkCredential(settings.Username, settings.Password);

        await client.SendMailAsync(message);
    }

    public async Task SendAdminEmailAsync(string subject, string body)
    {
        var settings = await _systemSettings.GetEmailAsync();
        await SendEmailAsync(settings.AdminEmail, subject, body);
    }

    public async Task SendITSupportEmailAsync(string subject, string body)
    {
        var settings = await _systemSettings.GetEmailAsync();
        var recipient = string.IsNullOrWhiteSpace(settings.ITSupportEmail) ? settings.AdminEmail : settings.ITSupportEmail;
        await SendEmailAsync(recipient, subject, body);
    }

    public async Task SendReservationsEmailAsync(string subject, string body)
    {
        var settings = await _systemSettings.GetEmailAsync();
        var recipient = string.IsNullOrWhiteSpace(settings.ReservationsEmail) ? settings.AdminEmail : settings.ReservationsEmail;
        await SendEmailAsync(recipient, subject, body);
    }
}
