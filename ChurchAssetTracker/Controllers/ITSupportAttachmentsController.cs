using System.Security.Claims;
using ChurchAssetTracker.Data;
using ChurchAssetTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChurchAssetTracker.Controllers;

[Authorize(Roles = "Admin,ITAdmin,ITSupportManager,ITSupportTech")]
[Route("ITSupport")]
public class ITSupportAttachmentsController : Controller
{
    private readonly SqlDataService _data;
    private readonly IEmailService _email;
    private readonly IWebHostEnvironment _environment;

    public ITSupportAttachmentsController(SqlDataService data, IEmailService email, IWebHostEnvironment environment)
    {
        _data = data;
        _email = email;
        _environment = environment;
    }

    [HttpPost("UploadAttachment")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadAttachment(int ticketId, IFormFile? attachment)
    {
        if (ticketId <= 0) return NotFound();

        var ticket = await _data.GetITSupportTicketAsync(ticketId);
        if (ticket == null) return NotFound();

        if (attachment == null || attachment.Length == 0)
        {
            TempData["TicketError"] = "Select a file to upload.";
            return RedirectToAction("Details", "ITSupport", new { id = ticketId });
        }

        try
        {
            var saved = await SaveITSupportAttachmentFileAsync(ticketId, attachment);
            var uploadedByUserId = GetCurrentUserId();

            await _data.CreateITSupportTicketAttachmentAsync(
                ticketId,
                saved.OriginalFileName,
                saved.StoredFileName,
                saved.FilePath,
                saved.ContentType,
                saved.FileSizeBytes,
                uploadedByUserId);

            var assignedEmail = await _data.GetITSupportTicketAssignedUserEmailAsync(ticketId);

            await TrySendITSupportEmailAsync(
                assignedEmail,
                "IT Ticket Attachment Uploaded",
                $@"An attachment was uploaded to an IT support ticket.

Ticket: {ticket.TicketNumber}
Title: {ticket.Title}
Attachment: {saved.OriginalFileName}
Uploaded by: {User.Identity?.Name}");

            TempData["SuccessMessage"] = "Attachment uploaded successfully.";
        }
        catch (Exception ex)
        {
            TempData["TicketError"] = ex.Message;
        }

        return RedirectToAction("Details", "ITSupport", new { id = ticketId });
    }

    [HttpGet("DownloadAttachment/{id:int}")]
    public async Task<IActionResult> DownloadAttachment(int id)
    {
        var attachment = await _data.GetITSupportTicketAttachmentAsync(id);
        if (attachment == null) return NotFound();

        var relativePath = attachment.FilePath.TrimStart('~', '/').Replace("/", Path.DirectorySeparatorChar.ToString());
        var fullPath = Path.Combine(_environment.WebRootPath, relativePath);

        if (!System.IO.File.Exists(fullPath)) return NotFound();

        var contentType = string.IsNullOrWhiteSpace(attachment.ContentType)
            ? "application/octet-stream"
            : attachment.ContentType;

        var bytes = await System.IO.File.ReadAllBytesAsync(fullPath);
        return File(bytes, contentType, attachment.OriginalFileName);
    }

    private int? GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var id) && id > 0 ? id : null;
    }

    private async Task<SavedAttachmentInfo> SaveITSupportAttachmentFileAsync(int ticketId, IFormFile file)
    {
        const long maxBytes = 10 * 1024 * 1024;
        if (file.Length > maxBytes)
            throw new InvalidOperationException("Attachment must be 10 MB or smaller.");

        var blockedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".bat", ".cmd", ".ps1", ".vbs", ".js", ".msi", ".scr", ".com", ".dll"
        };

        var originalFileName = Path.GetFileName(file.FileName);
        var extension = Path.GetExtension(originalFileName);

        if (blockedExtensions.Contains(extension))
            throw new InvalidOperationException("This file type is not allowed.");

        if (string.IsNullOrWhiteSpace(originalFileName))
            throw new InvalidOperationException("Invalid file name.");

        var uploadRoot = Path.Combine(_environment.WebRootPath, "uploads", "it-support-attachments", ticketId.ToString());
        Directory.CreateDirectory(uploadRoot);

        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(uploadRoot, storedFileName);

        await using var stream = System.IO.File.Create(fullPath);
        await file.CopyToAsync(stream);

        return new SavedAttachmentInfo
        {
            OriginalFileName = originalFileName,
            StoredFileName = storedFileName,
            FilePath = $"~/uploads/it-support-attachments/{ticketId}/{storedFileName}",
            ContentType = file.ContentType,
            FileSizeBytes = file.Length
        };
    }

    private async Task TrySendITSupportEmailAsync(string? assignedUserEmail, string subject, string body)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(assignedUserEmail))
            {
                await _email.SendEmailAsync(assignedUserEmail, $"[CWC Portal] {subject}", body);
                return;
            }

            await _email.SendITSupportEmailAsync($"[CWC Portal] {subject}", body);
        }
        catch
        {
            // Email should never block attachment uploads.
        }
    }

    private sealed class SavedAttachmentInfo
    {
        public string OriginalFileName { get; set; } = "";
        public string StoredFileName { get; set; } = "";
        public string FilePath { get; set; } = "";
        public string? ContentType { get; set; }
        public long FileSizeBytes { get; set; }
    }
}
