namespace ChurchAssetTracker.Services;

public interface IEmailService
{
    Task SendEmailAsync(string toEmail, string subject, string body);
    Task SendAdminEmailAsync(string subject, string body);
    Task SendITSupportEmailAsync(string subject, string body);
    Task SendReservationsEmailAsync(string subject, string body);
}