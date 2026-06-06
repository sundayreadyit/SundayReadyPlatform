using ChurchAssetTracker.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChurchAssetTracker.Controllers;

[Authorize(Roles = "Admin,SchoolAdmin,SchoolStaff")]
public class StudentsController : Controller
{
    private readonly SqlDataService _data;
    private readonly IWebHostEnvironment _environment;

    public StudentsController(SqlDataService data, IWebHostEnvironment environment)
    {
        _data = data;
        _environment = environment;
    }

    public async Task<IActionResult> Index(string search = "", bool includeInactive = false)
    {
        ViewBag.Search = search;
        ViewBag.IncludeInactive = includeInactive;
        return View(await _data.GetStudentsAsync(search, includeInactive));
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var student = await _data.GetStudentAsync(id);

        if (student == null)
            return NotFound();

        return View(new StudentFormViewModel
        {
            Student = student,
            Teachers = await _data.GetActiveTeacherOptionsAsync(),
            Documents = await _data.GetStudentDocumentsAsync(id)
        });
    }


    [HttpGet]
    public async Task<IActionResult> Create()
    {
        return View(new StudentFormViewModel
        {
            Student = new StudentRow { IsActive = true },
            Teachers = await _data.GetActiveTeacherOptionsAsync()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StudentFormViewModel model, IFormFile? photoFile)
    {
        ValidateStudent(model.Student);

        var photoPath = await SaveSchoolPhotoAsync(photoFile, "students");
        if (!ModelState.IsValid)
        {
            model.Teachers = await _data.GetActiveTeacherOptionsAsync();
            return View(model);
        }

        if (!string.IsNullOrWhiteSpace(photoPath))
            model.Student.PhotoPath = photoPath;

        await _data.CreateStudentAsync(model.Student, User.Identity?.Name ?? "Unknown");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var student = await _data.GetStudentAsync(id);
        if (student == null) return NotFound();

        return View(new StudentFormViewModel
        {
            Student = student,
            Teachers = await _data.GetActiveTeacherOptionsAsync(),
            Documents = await _data.GetStudentDocumentsAsync(id)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(StudentFormViewModel model, IFormFile? photoFile, bool removePhoto = false)
    {
        ValidateStudent(model.Student);

        var existing = await _data.GetStudentAsync(model.Student.StudentId);
        if (existing == null) return NotFound();

        var photoPath = await SaveSchoolPhotoAsync(photoFile, "students");
        if (!ModelState.IsValid)
        {
            model.Teachers = await _data.GetActiveTeacherOptionsAsync();
            model.Documents = await _data.GetStudentDocumentsAsync(model.Student.StudentId);
            model.Student.PhotoPath = existing.PhotoPath;
            return View(model);
        }

        model.Student.PhotoPath = existing.PhotoPath;

        if (removePhoto)
        {
            DeleteUploadedFile(existing.PhotoPath);
            model.Student.PhotoPath = null;
        }

        if (!string.IsNullOrWhiteSpace(photoPath))
        {
            DeleteUploadedFile(existing.PhotoPath);
            model.Student.PhotoPath = photoPath;
        }

        await _data.UpdateStudentAsync(model.Student, User.Identity?.Name ?? "Unknown");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Roster(int? teacherFacultyStaffId, string? gradeLevel)
    {
        var model = new StudentRosterViewModel
        {
            TeacherFacultyStaffId = teacherFacultyStaffId,
            GradeLevel = gradeLevel,
            Teachers = await _data.GetActiveTeacherOptionsAsync(),
            GradeLevels = await _data.GetStudentGradeLevelsAsync(),
            Students = await _data.GetStudentRosterAsync(teacherFacultyStaffId, gradeLevel)
        };

        return View(model);
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadDocument(int studentId, string? documentType, string? documentTitle, string? notes, IFormFile? documentFile)
    {
        var student = await _data.GetStudentAsync(studentId);

        if (student == null)
            return NotFound();

        if (documentFile == null || documentFile.Length == 0)
        {
            TempData["ErrorMessage"] = "Please select a document to upload.";
            return RedirectToAction(nameof(Edit), new { id = studentId });
        }

        if (documentFile.Length > 10 * 1024 * 1024)
        {
            TempData["ErrorMessage"] = "Document must be 10 MB or smaller.";
            return RedirectToAction(nameof(Edit), new { id = studentId });
        }

        var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".gif", ".webp", ".doc", ".docx", ".xls", ".xlsx", ".txt" };
        var ext = Path.GetExtension(documentFile.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(ext))
        {
            TempData["ErrorMessage"] = "Allowed document types are PDF, images, Word, Excel, or TXT.";
            return RedirectToAction(nameof(Edit), new { id = studentId });
        }

        var relativeFolder = $"/uploads/student-documents/{studentId}";
        var folder = Path.Combine(_environment.WebRootPath, "uploads", "student-documents", studentId.ToString());
        Directory.CreateDirectory(folder);

        var safeFileName = $"{Guid.NewGuid():N}{ext}";
        var physicalPath = Path.Combine(folder, safeFileName);

        await using (var stream = new FileStream(physicalPath, FileMode.Create))
        {
            await documentFile.CopyToAsync(stream);
        }

        await _data.CreateStudentDocumentAsync(new StudentDocumentRow
        {
            StudentId = studentId,
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

        TempData["SuccessMessage"] = "Student document uploaded.";
        return RedirectToAction(nameof(Edit), new { id = studentId });
    }

    [HttpGet]
    public async Task<IActionResult> DownloadDocument(int id)
    {
        var document = await _data.GetStudentDocumentAsync(id);

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
        var document = await _data.GetStudentDocumentAsync(id);

        if (document == null)
            return NotFound();

        await _data.DeactivateStudentDocumentAsync(id);

        TempData["SuccessMessage"] = "Student document removed.";
        return RedirectToAction(nameof(Edit), new { id = document.StudentId });
    }


    private void ValidateStudent(StudentRow model)
    {
        if (string.IsNullOrWhiteSpace(model.FirstName))
            ModelState.AddModelError("Student.FirstName", "First name is required.");

        if (string.IsNullOrWhiteSpace(model.LastName))
            ModelState.AddModelError("Student.LastName", "Last name is required.");
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
