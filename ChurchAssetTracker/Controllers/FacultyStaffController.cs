using ChurchAssetTracker.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChurchAssetTracker.Controllers;

[Authorize(Roles = "Admin,SchoolAdmin,SchoolStaff")]
public class FacultyStaffController : Controller
{
    private readonly SqlDataService _data;
    private readonly IWebHostEnvironment _environment;

    public FacultyStaffController(SqlDataService data, IWebHostEnvironment environment)
    {
        _data = data;
        _environment = environment;
    }

    public async Task<IActionResult> Index(string search = "", bool includeInactive = false)
    {
        ViewBag.Search = search;
        ViewBag.IncludeInactive = includeInactive;
        return View(await _data.GetFacultyStaffAsync(search, includeInactive));
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var member = await _data.GetFacultyStaffMemberAsync(id);

        if (member == null)
            return NotFound();

        return View(new FacultyStaffEditViewModel
        {
            FacultyStaff = member,
            Documents = await _data.GetFacultyStaffDocumentsAsync(id)
        });
    }


    [HttpGet]
    public IActionResult Create() => View(new FacultyStaffRow { IsActive = true });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FacultyStaffRow model, IFormFile? photoFile)
    {
        ValidateFacultyStaff(model);

        var photoPath = await SaveSchoolPhotoAsync(photoFile, "faculty");
        if (!ModelState.IsValid)
            return View(model);

        if (!string.IsNullOrWhiteSpace(photoPath))
            model.PhotoPath = photoPath;

        await _data.CreateFacultyStaffAsync(model, User.Identity?.Name ?? "Unknown");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var member = await _data.GetFacultyStaffMemberAsync(id);

        if (member == null)
            return NotFound();

        return View(new FacultyStaffEditViewModel
        {
            FacultyStaff = member,
            Documents = await _data.GetFacultyStaffDocumentsAsync(id)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(FacultyStaffEditViewModel model, IFormFile? photoFile, bool removePhoto = false)
    {
        ValidateFacultyStaff(model.FacultyStaff);

        var existing = await _data.GetFacultyStaffMemberAsync(model.FacultyStaff.FacultyStaffId);

        if (existing == null)
            return NotFound();

        var photoPath = await SaveSchoolPhotoAsync(photoFile, "faculty");

        if (!ModelState.IsValid)
        {
            model.FacultyStaff.PhotoPath = existing.PhotoPath;
            model.Documents = await _data.GetFacultyStaffDocumentsAsync(model.FacultyStaff.FacultyStaffId);
            return View(model);
        }

        model.FacultyStaff.PhotoPath = existing.PhotoPath;

        if (removePhoto)
        {
            DeleteUploadedFile(existing.PhotoPath);
            model.FacultyStaff.PhotoPath = null;
        }

        if (!string.IsNullOrWhiteSpace(photoPath))
        {
            DeleteUploadedFile(existing.PhotoPath);
            model.FacultyStaff.PhotoPath = photoPath;
        }

        await _data.UpdateFacultyStaffAsync(model.FacultyStaff, User.Identity?.Name ?? "Unknown");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadDocument(int facultyStaffId, string? documentType, string? documentTitle, string? notes, IFormFile? documentFile)
    {
        var member = await _data.GetFacultyStaffMemberAsync(facultyStaffId);

        if (member == null)
            return NotFound();

        if (documentFile == null || documentFile.Length == 0)
        {
            TempData["ErrorMessage"] = "Please select a document to upload.";
            return RedirectToAction(nameof(Edit), new { id = facultyStaffId });
        }

        if (documentFile.Length > 10 * 1024 * 1024)
        {
            TempData["ErrorMessage"] = "Document must be 10 MB or smaller.";
            return RedirectToAction(nameof(Edit), new { id = facultyStaffId });
        }

        var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".gif", ".webp", ".doc", ".docx", ".xls", ".xlsx", ".txt" };
        var ext = Path.GetExtension(documentFile.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(ext))
        {
            TempData["ErrorMessage"] = "Allowed document types are PDF, images, Word, Excel, or TXT.";
            return RedirectToAction(nameof(Edit), new { id = facultyStaffId });
        }

        var relativeFolder = $"/uploads/faculty-documents/{facultyStaffId}";
        var folder = Path.Combine(_environment.WebRootPath, "uploads", "faculty-documents", facultyStaffId.ToString());
        Directory.CreateDirectory(folder);

        var safeFileName = $"{Guid.NewGuid():N}{ext}";
        var physicalPath = Path.Combine(folder, safeFileName);

        await using (var stream = new FileStream(physicalPath, FileMode.Create))
        {
            await documentFile.CopyToAsync(stream);
        }

        await _data.CreateFacultyStaffDocumentAsync(new FacultyStaffDocumentRow
        {
            FacultyStaffId = facultyStaffId,
            DocumentType = string.IsNullOrWhiteSpace(documentType) ? "Other" : documentType.Trim(),
            DocumentTitle = string.IsNullOrWhiteSpace(documentTitle)
                ? Path.GetFileNameWithoutExtension(documentFile.FileName)
                : documentTitle.Trim(),
            OriginalFileName = Path.GetFileName(documentFile.FileName),
            FilePath = $"{relativeFolder}/{safeFileName}",
            ContentType = documentFile.ContentType,
            FileSizeBytes = documentFile.Length,
            Notes = notes,
            UploadedBy = User.Identity?.Name
        });

        TempData["SuccessMessage"] = "Faculty/staff document uploaded.";
        return RedirectToAction(nameof(Edit), new { id = facultyStaffId });
    }

    [HttpGet]
    public async Task<IActionResult> DownloadDocument(int id)
    {
        var document = await _data.GetFacultyStaffDocumentAsync(id);

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
    public async Task<IActionResult> DeleteDocument(int id)
    {
        var document = await _data.GetFacultyStaffDocumentAsync(id);

        if (document == null)
            return NotFound();

        await _data.DeactivateFacultyStaffDocumentAsync(id);

        TempData["SuccessMessage"] = "Faculty/staff document removed.";
        return RedirectToAction(nameof(Edit), new { id = document.FacultyStaffId });
    }

    private void ValidateFacultyStaff(FacultyStaffRow model)
    {
        if (string.IsNullOrWhiteSpace(model.FirstName))
            ModelState.AddModelError(nameof(model.FirstName), "First name is required.");

        if (string.IsNullOrWhiteSpace(model.LastName))
            ModelState.AddModelError(nameof(model.LastName), "Last name is required.");
    }

    private async Task<string?> SaveSchoolPhotoAsync(IFormFile? file, string folderName)
    {
        if (file == null || file.Length == 0)
            return null;

        if (file.Length > 2 * 1024 * 1024)
        {
            ModelState.AddModelError("Photo", "Photo must be 2 MB or smaller.");
            return null;
        }

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(ext))
        {
            ModelState.AddModelError("Photo", "Photo must be JPG, PNG, GIF, or WEBP.");
            return null;
        }

        var relativeFolder = $"/uploads/{folderName}";
        var folder = Path.Combine(_environment.WebRootPath, "uploads", folderName);
        Directory.CreateDirectory(folder);

        var fileName = $"{Guid.NewGuid():N}{ext}";
        var physicalPath = Path.Combine(folder, fileName);

        await using var stream = new FileStream(physicalPath, FileMode.Create);
        await file.CopyToAsync(stream);

        return $"{relativeFolder}/{fileName}";
    }

    private void DeleteUploadedFile(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return;

        var normalized = relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var physicalPath = Path.Combine(_environment.WebRootPath, normalized);

        if (System.IO.File.Exists(physicalPath))
            System.IO.File.Delete(physicalPath);
    }

}
