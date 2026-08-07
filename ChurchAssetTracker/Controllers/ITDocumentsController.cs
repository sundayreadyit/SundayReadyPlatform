using System.Text;
using System.Text.RegularExpressions;
using ChurchAssetTracker.Models;
using ChurchAssetTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChurchAssetTracker.Controllers;

[Authorize]
public class ITDocumentsController : Controller
{
    private readonly SystemSettingsService _settings;

    public ITDocumentsController(SystemSettingsService settings)
    {
        _settings = settings;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string search = "", string category = "All")
    {
        var selectedCategory = string.IsNullOrWhiteSpace(category) ? "All" : category.Trim();
        var searchValue = search?.Trim() ?? "";
        var root = await GetDocumentationRootAsync();
        var model = new ITDocumentLibraryViewModel
        {
            Search = searchValue,
            Category = selectedCategory,
            RootPath = root,
            RootExists = Directory.Exists(root)
        };

        List<ITDocumentRow> allDocs;
        try
        {
            allDocs = LoadDocumentsFromShare(root);
        }
        catch (Exception ex)
        {
            allDocs = new List<ITDocumentRow>();
            model.ErrorMessage = $"Unable to read the documentation library path. {ex.Message}";
        }

        var categories = allDocs
            .Select(d => d.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .ToList();
        categories.Insert(0, "All");
        model.Categories = categories;

        var docs = allDocs;
        if (!string.Equals(selectedCategory, "All", StringComparison.OrdinalIgnoreCase))
            docs = docs.Where(d => string.Equals(d.Category, selectedCategory, StringComparison.OrdinalIgnoreCase)).ToList();

        if (!string.IsNullOrWhiteSpace(searchValue))
        {
            docs = docs.Where(d =>
                Contains(d.DocumentNumber, searchValue) ||
                Contains(d.Title, searchValue) ||
                Contains(d.Description, searchValue) ||
                Contains(d.OriginalFileName, searchValue) ||
                Contains(d.Category, searchValue) ||
                Contains(d.RelativePath, searchValue)).ToList();
        }

        model.Documents = docs
            .OrderBy(d => d.Category)
            .ThenBy(d => string.IsNullOrWhiteSpace(d.DocumentNumber) ? "ZZZ" : d.DocumentNumber)
            .ThenBy(d => d.Title)
            .ToList();

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Download(string file)
    {
        if (string.IsNullOrWhiteSpace(file)) return NotFound();

        var fullPath = DecodePath(file);
        if (string.IsNullOrWhiteSpace(fullPath)) return NotFound();

        var root = await GetDocumentationRootAsync();
        var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fileFull = Path.GetFullPath(fullPath);

        if (!fileFull.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase)) return Forbid();
        if (!System.IO.File.Exists(fileFull)) return NotFound();

        return PhysicalFile(fileFull, GetContentType(fileFull), Path.GetFileName(fileFull));
    }

    private List<ITDocumentRow> LoadDocumentsFromShare(string root)
    {
        if (!Directory.Exists(root)) return new List<ITDocumentRow>();

        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".png", ".jpg", ".jpeg"
        };

        var files = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(f => allowed.Contains(Path.GetExtension(f)))
            .ToList();

        var list = new List<ITDocumentRow>();
        foreach (var file in files)
        {
            var info = new FileInfo(file);
            var relative = Path.GetRelativePath(root, file);
            var category = GetCategoryFromRelativePath(relative);
            var title = Path.GetFileNameWithoutExtension(info.Name).Replace('_', ' ').Replace('-', ' ').Trim();
            var docNumber = ExtractDocumentNumber(title);
            if (!string.IsNullOrWhiteSpace(docNumber))
                title = Regex.Replace(title, Regex.Escape(docNumber), "", RegexOptions.IgnoreCase).Trim(' ', '-', '_');

            var inferredDescription = BuildDescription(category, relative, docNumber);

            list.Add(new ITDocumentRow
            {
                DocumentNumber = docNumber,
                Category = category,
                Title = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(info.Name) : title,
                Description = inferredDescription,
                OriginalFileName = info.Name,
                FilePath = file,
                RelativePath = relative,
                ContentType = GetContentType(file),
                FileSizeBytes = info.Length,
                UploadedDate = info.CreationTime,
                ModifiedDate = info.LastWriteTime,
                DownloadToken = EncodePath(file)
            });
        }

        return list;
    }

    private async Task<string> GetDocumentationRootAsync()
    {
        var storage = await _settings.GetStorageAsync();
        return string.IsNullOrWhiteSpace(storage.DocumentLibraryRootPath) ? @"\\CWCA-DC\Documentation" : storage.DocumentLibraryRootPath.Trim();
    }

    private static string GetCategoryFromRelativePath(string relative)
    {
        var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[0])) return parts[0].Replace('_', ' ');

        var fileName = Path.GetFileNameWithoutExtension(relative);
        var number = ExtractDocumentNumber(fileName);
        if (number.StartsWith("IT-STD-", StringComparison.OrdinalIgnoreCase)) return "IT Standards";
        if (number.StartsWith("IT-SOP-", StringComparison.OrdinalIgnoreCase)) return "IT SOPs";
        if (number.StartsWith("IT-DR-", StringComparison.OrdinalIgnoreCase)) return "Disaster Recovery";
        if (number.StartsWith("IT-INF-", StringComparison.OrdinalIgnoreCase)) return "Infrastructure";
        if (number.StartsWith("IT-GUIDE-", StringComparison.OrdinalIgnoreCase)) return "User Guides";

        var lower = fileName.ToLowerInvariant();
        if (lower.Contains("enrollment")) return "Enrollment Forms";
        if (lower.Contains("school")) return "School Documents";
        if (lower.Contains("church")) return "Church Documents";
        if (lower.Contains("policy") || lower.Contains("policies")) return "Policies";
        if (lower.Contains("form")) return "Forms";
        return "Documents";
    }

    private static string BuildDescription(string category, string relativePath, string docNumber)
    {
        if (!string.IsNullOrWhiteSpace(docNumber))
            return $"{category} document {docNumber}. Source: {relativePath}";
        return $"{category} document. Source: {relativePath}";
    }

    private static string ExtractDocumentNumber(string value)
    {
        var match = Regex.Match(value ?? "", @"\b(IT-(STD|SOP|DR|GUIDE|INF|BRIEF|TPL|INDEX)-\d{3})\b", RegexOptions.IgnoreCase);
        return match.Success ? match.Value.ToUpperInvariant() : "";
    }

    private static bool Contains(string? value, string search) => !string.IsNullOrWhiteSpace(value) && value.Contains(search, StringComparison.OrdinalIgnoreCase);
    private static string EncodePath(string path) => Convert.ToBase64String(Encoding.UTF8.GetBytes(path)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static string DecodePath(string token)
    {
        try
        {
            var s = token.Replace('-', '+').Replace('_', '/');
            s = s.PadRight(s.Length + (4 - s.Length % 4) % 4, '=');
            return Encoding.UTF8.GetString(Convert.FromBase64String(s));
        }
        catch { return ""; }
    }

    private static string GetContentType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".doc" => "application/msword",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xls" => "application/vnd.ms-excel",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".ppt" => "application/vnd.ms-powerpoint",
        ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".txt" => "text/plain",
        _ => "application/octet-stream"
    };
}
