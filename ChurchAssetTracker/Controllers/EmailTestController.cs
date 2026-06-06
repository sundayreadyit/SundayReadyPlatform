using ChurchAssetTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChurchAssetTracker.Controllers;

[Authorize(Roles = "Admin")]
public class EmailTestController : Controller
{
    private readonly IEmailService _email;

    public EmailTestController(IEmailService email)
    {
        _email = email;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendTest(string toEmail)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            TempData["EmailTestError"] = "Enter an email address.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _email.SendEmailAsync(
                toEmail,
                "CWC Operations Portal Test Email",
                "This is a test email from the CWC Operations Portal. If you received this, SMTP email is working.");

            TempData["EmailTestMessage"] = "Test email sent or logged. If EmailSettings:Enabled is false, it was logged only.";
        }
        catch (Exception ex)
        {
            TempData["EmailTestError"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}