using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ChurchAssetTracker.Data;
using ChurchAssetTracker.Models;

namespace ChurchAssetTracker.Controllers;

[Authorize(Roles = "Admin,AssetManager")]
public class AssetsController : Controller
{
    private readonly SqlDataService _data;
    private readonly IWebHostEnvironment _environment;

    public AssetsController(SqlDataService data, IWebHostEnvironment environment)
    {
        _data = data;
        _environment = environment;
    }

    public async Task<IActionResult> Index()
    {
        return View(await _data.GetAssetsAsync());
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new AssetEditViewModel { IsActive = true, CurrentCondition = "Good" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AssetEditViewModel asset, IFormFile? photoFile)
    {
        var photoPath = await SaveAssetPhotoAsync(photoFile);

        if (!ModelState.IsValid)
            return View(asset);

        if (!string.IsNullOrWhiteSpace(photoPath))
            asset.PhotoPath = photoPath;

        await _data.CreateAssetAsync(asset);
        await _data.LogAuditAsync("Create", "Asset", null, $"Added asset: {asset.AssetName}");
        TempData["SuccessMessage"] = "Asset added successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var asset = await _data.GetAssetForEditAsync(id);
        if (asset == null)
            return NotFound();

        return View(asset);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(AssetEditViewModel asset, IFormFile? photoFile, bool removePhoto = false)
    {
        var existing = await _data.GetAssetForEditAsync(asset.AssetId);
        if (existing == null)
            return NotFound();

        var photoPath = await SaveAssetPhotoAsync(photoFile);

        if (!ModelState.IsValid)
        {
            asset.PhotoPath = existing.PhotoPath;
            return View(asset);
        }

        asset.PhotoPath = existing.PhotoPath;

        if (removePhoto)
        {
            DeleteUploadedFile(existing.PhotoPath);
            asset.PhotoPath = null;
        }

        if (!string.IsNullOrWhiteSpace(photoPath))
        {
            DeleteUploadedFile(existing.PhotoPath);
            asset.PhotoPath = photoPath;
        }

        await _data.UpdateAssetAsync(asset);
        await _data.LogAuditAsync("Update", "Asset", asset.AssetId, $"Updated asset: {asset.AssetName}");
        TempData["SuccessMessage"] = "Asset updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id)
    {
        await _data.DeactivateAssetAsync(id);
        await _data.LogAuditAsync("Deactivate", "Asset", id, $"Deactivated asset record ID {id}");
        TempData["SuccessMessage"] = "Asset deactivated successfully.";
        return RedirectToAction(nameof(Index));
    }
    private async Task<string?> SaveAssetPhotoAsync(IFormFile? file)
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

        var relativeFolder = "/uploads/assets";
        var folder = Path.Combine(_environment.WebRootPath, "uploads", "assets");
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
