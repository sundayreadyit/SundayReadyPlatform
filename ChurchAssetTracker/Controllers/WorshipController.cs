using System.Text;
using ChurchAssetTracker.Models;
using ChurchAssetTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChurchAssetTracker.Controllers;

[Authorize]
public class WorshipController : Controller
{
    private readonly SystemSettingsService _settings;

    public WorshipController(SystemSettingsService settings) => _settings = settings;

    [HttpGet]
    public async Task<IActionResult> Index(string search = "")
    {
        if (!await _settings.IsModuleEnabledAsync("Worship")) return NotFound();
        var storage = await _settings.GetStorageAsync();
        var root = storage.WorshipLibraryRootPath?.Trim() ?? "";
        var model = new WorshipLibraryViewModel
        {
            Search = search?.Trim() ?? "",
            RootPath = root,
            RootExists = !string.IsNullOrWhiteSpace(root) && Directory.Exists(root)
        };

        if (!model.RootExists) return View(model);

        try
        {
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".pdf", ".doc", ".docx", ".txt", ".png", ".jpg", ".jpeg" };
            var files = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
                .Where(f => allowed.Contains(Path.GetExtension(f)));

            if (!string.IsNullOrWhiteSpace(model.Search))
                files = files.Where(f => Path.GetFileNameWithoutExtension(f).Contains(model.Search, StringComparison.OrdinalIgnoreCase) || Path.GetFileName(f).Contains(model.Search, StringComparison.OrdinalIgnoreCase));

            model.Songs = files.Select(f =>
            {
                var info = new FileInfo(f);
                return new WorshipSongRow
                {
                    Title = CleanTitle(Path.GetFileNameWithoutExtension(info.Name)),
                    FileName = info.Name,
                    RelativePath = Path.GetRelativePath(root, f),
                    Extension = info.Extension,
                    FileSizeBytes = info.Length,
                    ModifiedDate = info.LastWriteTime,
                    Token = EncodePath(f)
                };
            }).OrderBy(x => x.Title).ToList();
        }
        catch (Exception ex)
        {
            model.ErrorMessage = $"Unable to read the worship song library. {ex.Message}";
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> ViewSong(string file)
    {
        if (!await _settings.IsModuleEnabledAsync("Worship")) return NotFound();
        var path = DecodePath(file);
        var validated = await ValidatePathAsync(path);
        if (validated == null) return NotFound();
        var contentType = GetContentType(validated);
        return PhysicalFile(validated, contentType, enableRangeProcessing: true);
    }

    [HttpGet]
    public async Task<IActionResult> Download(string file)
    {
        if (!await _settings.IsModuleEnabledAsync("Worship")) return NotFound();
        var path = DecodePath(file);
        var validated = await ValidatePathAsync(path);
        if (validated == null) return NotFound();
        return PhysicalFile(validated, GetContentType(validated), Path.GetFileName(validated));
    }

    private async Task<string?> ValidatePathAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var root = (await _settings.GetStorageAsync()).WorshipLibraryRootPath;
        if (string.IsNullOrWhiteSpace(root)) return null;
        var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fileFull = Path.GetFullPath(path);
        if (!fileFull.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(fileFull)) return null;
        return fileFull;
    }

    private static string CleanTitle(string value) => value.Replace('_', ' ').Replace('-', ' ').Trim();
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
        ".txt" => "text/plain",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        _ => "application/octet-stream"
    };
}
