using System.Net;
using System.Net.Mail;
using ChurchAssetTracker.Data;
using Microsoft.Extensions.Options;

namespace ChurchAssetTracker.Services;

public class SmtpEmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(
        IOptions<EmailSettings> settings,
        ILogger<SmtpEmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string body)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("Email disabled. Would have sent to {ToEmail}: {Subject}", toEmail, subject);
            return;
        }

        if (string.IsNullOrWhiteSpace(toEmail))
        {
            _logger.LogWarning("Email skipped because recipient address was blank. Subject: {Subject}", subject);
            return;
        }

        if (string.IsNullOrWhiteSpace(_settings.SmtpHost))
            throw new InvalidOperationException("EmailSettings:SmtpHost is not configured.");

        if (string.IsNullOrWhiteSpace(_settings.FromEmail))
            throw new InvalidOperationException("EmailSettings:FromEmail is not configured.");

        using var message = new MailMessage
        {
            From = new MailAddress(_settings.FromEmail, _settings.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };

        message.To.Add(toEmail);

        using var client = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort)
        {
            EnableSsl = _settings.UseSsl
        };

        if (!string.IsNullOrWhiteSpace(_settings.Username))
        {
            client.Credentials = new NetworkCredential(_settings.Username, _settings.Password);
        }

        await client.SendMailAsync(message);
    }

    public async Task SendAdminEmailAsync(string subject, string body)
    {
        await SendEmailAsync(_settings.AdminEmail, subject, body);
    }

    public async Task SendITSupportEmailAsync(string subject, string body)
    {
        var recipient = string.IsNullOrWhiteSpace(_settings.ITSupportEmail)
            ? _settings.AdminEmail
            : _settings.ITSupportEmail;

        await SendEmailAsync(recipient, subject, body);
    }

    public async Task SendReservationsEmailAsync(string subject, string body)
    {
        var recipient = string.IsNullOrWhiteSpace(_settings.ReservationsEmail)
            ? _settings.AdminEmail
            : _settings.ReservationsEmail;

        await SendEmailAsync(recipient, subject, body);
    }
}