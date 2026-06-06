namespace ChurchAssetTracker.Data;

public class EmailSettings
{
    public bool Enabled { get; set; } = false;
    public string SmtpHost { get; set; } = "";
    public int SmtpPort { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromEmail { get; set; } = "portal@cwczillah.org";
    public string FromName { get; set; } = "CWC Operations Portal";
    public string AdminEmail { get; set; } = "";
    public string ITSupportEmail { get; set; } = "";
    public string ReservationsEmail { get; set; } = "";
}