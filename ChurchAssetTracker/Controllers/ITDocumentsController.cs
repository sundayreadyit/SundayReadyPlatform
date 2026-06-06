using ChurchAssetTracker.Data;
using ChurchAssetTracker.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChurchAssetTracker.Controllers;

[Authorize(Roles = "Admin,ITAssetManager,ITSupportManager,ITSupportTech")]
public class ITDocumentsController : Controller
{
    private readonly SqlDataService _data;
    private readonly IWebHostEnvironment _environment;

    public ITDocumentsController(SqlDataService data, IWebHostEnvironment environment)
    {
        _data = data;
        _environment = environment;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string search = "", string category = "All")
    {
        var model = new ITDocumentLibraryViewModel
        {
            Search = search ?? "",
            Category = string.IsNullOrWhiteSpace(category) ? "All" : category,
            Documents = await _data.GetITDocumentsAsync(search ?? "", string.IsNullOrWhiteSpace(category) ? "All" : category)
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(string category, string title, string? description, IFormFile? documentFile)
    {
        if (documentFile == null || documentFile.Length == 0)
        {
            TempData["ErrorMessage"] = "Please select a document to upload.";
            return RedirectToAction(nameof(Index));
        }

        if (documentFile.Length > 25 * 1024 * 1024)
        {
            TempData["ErrorMessage"] = "Document must be 25 MB or smaller.";
            return RedirectToAction(nameof(Index));
        }

        var allowedExtensions = new[]
        {
            ".pdf", ".jpg", ".jpeg", ".png", ".gif", ".webp",
            ".doc", ".docx", ".xls", ".xlsx", ".csv", ".txt",
            ".vsd", ".vsdx", ".drawio", ".zip"
        };

        var ext = Path.GetExtension(documentFile.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(ext))
        {
            TempData["ErrorMessage"] = "Allowed types: PDF, images, Word, Excel, CSV, TXT, Visio, draw.io, or ZIP.";
            return RedirectToAction(nameof(Index));
        }

        var relativeFolder = "/uploads/it-documents";
        var folder = Path.Combine(_environment.WebRootPath, "uploads", "it-documents");
        Directory.CreateDirectory(folder);

        var safeFileName = $"{Guid.NewGuid():N}{ext}";
        var physicalPath = Path.Combine(folder, safeFileName);

        await using (var stream = new FileStream(physicalPath, FileMode.Create))
        {
            await documentFile.CopyToAsync(stream);
        }

        await _data.CreateITDocumentAsync(new ITDocumentRow
        {
            Category = string.IsNullOrWhiteSpace(category) ? "Other" : category.Trim(),
            Title = string.IsNullOrWhiteSpace(title)
                ? Path.GetFileNameWithoutExtension(documentFile.FileName)
                : title.Trim(),
            Description = description,
            OriginalFileName = Path.GetFileName(documentFile.FileName),
            FilePath = $"{relativeFolder}/{safeFileName}",
            ContentType = documentFile.ContentType,
            FileSizeBytes = documentFile.Length,
            UploadedBy = User.Identity?.Name
        });

        TempData["SuccessMessage"] = "IT document uploaded.";
        return RedirectToAction(nameof(Index), new { category });
    }

    [HttpGet]
    public async Task<IActionResult> Download(int id)
    {
        var document = await _data.GetITDocumentAsync(id);

        if (document == null || !document.IsActive)
            return NotFound();

        var normalized = document.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var physicalPath = Path.Combine(_environment.WebRootPath, normalized);

        if (!System.IO.File.Exists(physicalPath))
            return NotFound();

        var contentType = string.IsNullOrWhiteSpace(document.ContentType)
            ? "application/octet-stream"
            : document.ContentType;

        return PhysicalFile(physicalPath, contentType, document.OriginalFileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var document = await _data.GetITDocumentAsync(id);

        if (document == null)
            return NotFound();

        await _data.DeactivateITDocumentAsync(id);

        TempData["SuccessMessage"] = "IT document removed.";
        return RedirectToAction(nameof(Index), new { category = document.Category });
    }
}
