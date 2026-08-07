using System.Text;
using ChurchAssetTracker.Models;
using ChurchAssetTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace ChurchAssetTracker.Controllers;

[Authorize(Roles = "Admin,WorshipLeader")]
public class WorshipController : Controller
{
    private readonly SystemSettingsService _settings;
    private readonly WorshipPlanningService _planning;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<WorshipController> _logger;

    public WorshipController(SystemSettingsService settings, WorshipPlanningService planning, IWebHostEnvironment environment, ILogger<WorshipController> logger)
    {
        _settings = settings;
        _planning = planning;
        _environment = environment;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string search = "")
    {
        if (!await _settings.IsModuleEnabledAsync("Worship")) return NotFound();
        var storage = await _settings.GetStorageAsync();
        var root = storage.WorshipLibraryRootPath?.Trim() ?? "";
        var model = new WorshipPlanningHomeViewModel
        {
            Search = search?.Trim() ?? "",
            RootPath = root,
            RootExists = !string.IsNullOrWhiteSpace(root) && Directory.Exists(root),
            UpcomingSets = await _planning.GetUpcomingAsync(),
            RecentSets = await _planning.GetRecentAsync(6)
        };

        if (!model.RootExists) return View(model);
        try
        {
            var usage = await _planning.GetUsageAsync();
            model.Songs = EnumerateSongs(root, model.Search).Select(song =>
            {
                if (usage.TryGetValue(song.RelativePath, out var u))
                {
                    song.TimesUsed = u.TimesUsed;
                    song.LastUsedDate = u.LastUsedDate;
                }
                return song;
            }).ToList();
        }
        catch (Exception ex)
        {
            model.ErrorMessage = $"Unable to read the worship song library. {ex.Message}";
        }
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> CreateSet()
    {
        if (!await _settings.IsModuleEnabledAsync("Worship")) return NotFound();
        return View(new WorshipSetInputModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSet(WorshipSetInputModel model)
    {
        if (!await _settings.IsModuleEnabledAsync("Worship")) return NotFound();
        if (!ModelState.IsValid) return View(model);
        var id = await _planning.CreateSetAsync(model, User.Identity?.Name ?? "Portal User");
        TempData["Success"] = "Worship set created. Add songs and arrange the service order below.";
        return RedirectToAction(nameof(EditSet), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> EditSet(int id, string search = "")
    {
        if (!await _settings.IsModuleEnabledAsync("Worship")) return NotFound();
        var set = await _planning.GetSetAsync(id);
        if (set == null) return NotFound();
        var storage = await _settings.GetStorageAsync();
        var root = storage.WorshipLibraryRootPath?.Trim() ?? "";
        var model = new WorshipSetEditorViewModel { Set = set, Search = search?.Trim() ?? "" };
        if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
        {
            try { model.SearchResults = EnumerateSongs(root, model.Search).Take(string.IsNullOrWhiteSpace(model.Search) ? 24 : 100).ToList(); }
            catch (Exception ex) { model.ErrorMessage = ex.Message; }
        }
        else model.ErrorMessage = "The Worship Library path is not available. An administrator can configure it under Administration → Storage.";
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSet(WorshipSetInputModel model)
    {
        if (!await _settings.IsModuleEnabledAsync("Worship")) return NotFound();
        if (!ModelState.IsValid)
        {
            TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage));
            return RedirectToAction(nameof(EditSet), new { id = model.Id });
        }
        if (!new[] { "Draft", "Ready", "Completed" }.Contains(model.Status, StringComparer.OrdinalIgnoreCase)) model.Status = "Draft";
        await _planning.UpdateSetAsync(model);
        TempData["Success"] = "Service details saved.";
        return RedirectToAction(nameof(EditSet), new { id = model.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSong(int setId, string file)
    {
        if (!await _settings.IsModuleEnabledAsync("Worship")) return NotFound();
        var full = await ValidatePathAsync(DecodePath(file));
        if (full == null) { TempData["Error"] = "The selected song could not be found in the Worship Library."; return RedirectToAction(nameof(EditSet), new { id = setId }); }
        var root = (await _settings.GetStorageAsync()).WorshipLibraryRootPath!;
        var relative = Path.GetRelativePath(root, full);
        await _planning.AddSongAsync(setId, CleanTitle(Path.GetFileNameWithoutExtension(full)), relative);
        TempData["Success"] = $"Added {CleanTitle(Path.GetFileNameWithoutExtension(full))}.";
        return RedirectToAction(nameof(EditSet), new { id = setId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveSong(int setId, int itemId)
    {
        if (!await _settings.IsModuleEnabledAsync("Worship")) return NotFound();
        await _planning.RemoveSongAsync(itemId);
        TempData["Success"] = "Song removed from the set.";
        return RedirectToAction(nameof(EditSet), new { id = setId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSong(WorshipSetItemModel item)
    {
        if (!await _settings.IsModuleEnabledAsync("Worship")) return NotFound();
        await _planning.UpdateItemAsync(item);
        TempData["Success"] = "Song notes saved.";
        return RedirectToAction(nameof(EditSet), new { id = item.WorshipSetId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reorder(int setId, List<int> itemIds)
    {
        if (!await _settings.IsModuleEnabledAsync("Worship")) return NotFound();
        await _planning.ReorderAsync(setId, itemIds);
        return Json(new { ok = true });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DuplicateSet(int id)
    {
        if (!await _settings.IsModuleEnabledAsync("Worship")) return NotFound();
        var newId = await _planning.DuplicateAsync(id, User.Identity?.Name ?? "Portal User");
        TempData["Success"] = "Set duplicated. The copied service date was moved forward seven days; adjust it as needed.";
        return RedirectToAction(nameof(EditSet), new { id = newId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSet(int id)
    {
        if (!await _settings.IsModuleEnabledAsync("Worship")) return NotFound();
        await _planning.DeleteSetAsync(id);
        TempData["Success"] = "Worship set deleted.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> History()
    {
        if (!await _settings.IsModuleEnabledAsync("Worship")) return NotFound();
        return View(await _planning.GetRecentAsync(100));
    }

    [HttpGet]
    public async Task<IActionResult> PrintPacket(int id, bool includeCover = true, bool includeSetList = true)
    {
        if (!await _settings.IsModuleEnabledAsync("Worship")) return NotFound();
        var set = await _planning.GetSetAsync(id);
        if (set == null) return NotFound();
        if (!set.Items.Any()) { TempData["Error"] = "Add at least one song before printing a worship packet."; return RedirectToAction(nameof(EditSet), new { id }); }

        var storage = await _settings.GetStorageAsync();
        var root = storage.WorshipLibraryRootPath?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) { TempData["Error"] = "The Worship Library path is unavailable."; return RedirectToAction(nameof(EditSet), new { id }); }

        var missing = new List<string>();
        var files = new List<(WorshipSetItemModel Item, string Path)>();
        foreach (var item in set.Items)
        {
            var path = Path.GetFullPath(Path.Combine(root, item.RelativePath));
            var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!path.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(path) || !Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase)) missing.Add(item.SongTitle);
            else files.Add((item, path));
        }
        if (missing.Any())
        {
            TempData["Error"] = "The packet was not generated because these PDF files are missing or unavailable: " + string.Join(", ", missing);
            return RedirectToAction(nameof(EditSet), new { id });
        }

        try
        {
            using var output = new PdfDocument();
            output.Info.Title = $"{set.Title} - {set.ServiceDate:MMMM d, yyyy}";
            if (includeCover) await AddCoverPageAsync(output, set);
            if (includeSetList) await AddSetListPageAsync(output, set);
            foreach (var entry in files)
            {
                try
                {
                    using var input = PdfReader.Open(entry.Path, PdfDocumentOpenMode.Import);
                    for (var p = 0; p < input.PageCount; p++) output.AddPage(input.Pages[p]);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Unable to merge worship PDF {Path}", entry.Path);
                    TempData["Error"] = $"The packet could not be generated because '{entry.Item.SongTitle}' is not a readable PDF.";
                    return RedirectToAction(nameof(EditSet), new { id });
                }
            }
            using var ms = new MemoryStream();
            output.Save(ms, false);
            var safe = string.Concat(set.Title.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            Response.Headers["Content-Disposition"] = $"inline; filename=\"{safe}_{set.ServiceDate:yyyy-MM-dd}.pdf\"";
            return File(ms.ToArray(), "application/pdf");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to build worship packet for set {SetId}", id);
            TempData["Error"] = "The worship packet could not be generated. " + ex.Message;
            return RedirectToAction(nameof(EditSet), new { id });
        }
    }

    private async Task AddCoverPageAsync(PdfDocument document, WorshipSetDetail set)
    {
        var page = document.AddPage();
        page.Size = PdfSharpCore.PageSize.Letter;
        using var gfx = XGraphics.FromPdfPage(page);
        var branding = await _settings.GetBrandingAsync();
        var primary = ParseColor(branding.PrimaryColor);
        var dark = XColor.FromArgb(31, 41, 55);
        var muted = XColor.FromArgb(100, 116, 139);
        var titleFont = new XFont("Arial", 24, XFontStyle.Bold);
        var orgFont = new XFont("Arial", 12, XFontStyle.Bold);
        var bodyFont = new XFont("Arial", 11, XFontStyle.Regular);
        var smallFont = new XFont("Arial", 9, XFontStyle.Regular);

        var y = 54d;
        var logoPath = ResolveWebPath(branding.LogoPath);
        if (logoPath != null && System.IO.File.Exists(logoPath))
        {
            try
            {
                using var img = XImage.FromFile(logoPath);
                var maxW = 115d; var maxH = 80d;
                var ratio = Math.Min(maxW / img.PixelWidth, maxH / img.PixelHeight);
                var w = img.PixelWidth * ratio; var h = img.PixelHeight * ratio;
                gfx.DrawImage(img, 54, y, w, h); y += h + 18;
            }
            catch { /* Cover still renders without logo. */ }
        }
        gfx.DrawString(branding.OrganizationName, orgFont, new XSolidBrush(primary), new XRect(54, y, 504, 22), XStringFormats.TopLeft); y += 28;
        gfx.DrawString(set.Title, titleFont, new XSolidBrush(dark), new XRect(54, y, 504, 36), XStringFormats.TopLeft); y += 42;
        gfx.DrawString(set.ServiceDate.ToString("dddd, MMMM d, yyyy"), bodyFont, new XSolidBrush(muted), new XRect(54, y, 504, 22), XStringFormats.TopLeft); y += 34;
        gfx.DrawLine(new XPen(primary, 2), 54, y, 558, y); y += 24;
        gfx.DrawString("Service Packet", orgFont, new XSolidBrush(dark), new XRect(54, y, 504, 22), XStringFormats.TopLeft); y += 26;
        gfx.DrawString($"{set.Items.Count} song{(set.Items.Count == 1 ? "" : "s")} included", bodyFont, new XSolidBrush(muted), new XRect(54, y, 504, 22), XStringFormats.TopLeft); y += 34;
        if (!string.IsNullOrWhiteSpace(set.Notes))
        {
            gfx.DrawString("Service Notes", orgFont, new XSolidBrush(dark), new XRect(54, y, 504, 22), XStringFormats.TopLeft); y += 24;
            gfx.DrawString(set.Notes, smallFont, new XSolidBrush(muted), new XRect(54, y, 504, 150), XStringFormats.TopLeft);
        }
        gfx.DrawString("Prepared with Sunday Ready Platform", smallFont, new XSolidBrush(muted), new XRect(54, 735, 504, 18), XStringFormats.BottomLeft);
    }

    [HttpGet]
    public async Task<IActionResult> PrintSetList(int id)
    {
        if (!await _settings.IsModuleEnabledAsync("Worship")) return NotFound();
        var set = await _planning.GetSetAsync(id);
        if (set == null) return NotFound();
        if (!set.Items.Any())
        {
            TempData["Error"] = "Add at least one song before printing the set list.";
            return RedirectToAction(nameof(EditSet), new { id });
        }

        using var output = new PdfDocument();
        output.Info.Title = $"{set.Title} - Set List - {set.ServiceDate:MMMM d, yyyy}";
        await AddSetListPageAsync(output, set);
        using var ms = new MemoryStream();
        output.Save(ms, false);
        var safe = string.Concat(set.Title.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        Response.Headers["Content-Disposition"] = $"inline; filename=\"{safe}_SetList_{set.ServiceDate:yyyy-MM-dd}.pdf\"";
        return File(ms.ToArray(), "application/pdf");
    }

    private async Task AddSetListPageAsync(PdfDocument document, WorshipSetDetail set)
    {
        var page = document.AddPage();
        page.Size = PdfSharpCore.PageSize.Letter;
        using var gfx = XGraphics.FromPdfPage(page);
        var branding = await _settings.GetBrandingAsync();
        var primary = ParseColor(branding.PrimaryColor);
        var dark = XColor.FromArgb(31, 41, 55);
        var muted = XColor.FromArgb(100, 116, 139);
        var headingFont = new XFont("Arial", 22, XFontStyle.Bold);
        var subFont = new XFont("Arial", 11, XFontStyle.Regular);
        var songFont = new XFont("Arial", 16, XFontStyle.Bold);
        var numberFont = new XFont("Arial", 13, XFontStyle.Bold);
        var footerFont = new XFont("Arial", 8, XFontStyle.Regular);

        gfx.DrawString("WORSHIP SET LIST", headingFont, new XSolidBrush(dark), new XRect(54, 50, 504, 32), XStringFormats.TopLeft);
        gfx.DrawString($"{set.ServiceDate:dddd, MMMM d, yyyy}", subFont, new XSolidBrush(muted), new XRect(54, 84, 504, 20), XStringFormats.TopLeft);
        gfx.DrawLine(new XPen(primary, 2), 54, 116, 558, 116);

        var y = 142d;
        var index = 1;
        foreach (var item in set.Items)
        {
            // A dedicated, uncluttered running order for musicians and presentation operators.
            gfx.DrawString($"{index}.", numberFont, new XSolidBrush(primary), new XRect(62, y + 2, 32, 24), XStringFormats.TopLeft);
            gfx.DrawString(item.SongTitle, songFont, new XSolidBrush(dark), new XRect(100, y, 440, 26), XStringFormats.TopLeft);
            y += 34;
            index++;
            if (y > 700) break;
        }

        gfx.DrawString(branding.OrganizationName, footerFont, new XSolidBrush(muted), new XRect(54, 735, 504, 16), XStringFormats.BottomLeft);
    }

    [HttpGet]
    public async Task<IActionResult> ViewSetSong(int setId, int itemId)
    {
        if (!await _settings.IsModuleEnabledAsync("Worship")) return NotFound();
        var set = await _planning.GetSetAsync(setId);
        var item = set?.Items.FirstOrDefault(x => x.Id == itemId);
        if (item == null) return NotFound();
        var root = (await _settings.GetStorageAsync()).WorshipLibraryRootPath?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(root)) return NotFound();
        var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(root, item.RelativePath));
        if (!full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(full)) return NotFound();
        return PhysicalFile(full, GetContentType(full), enableRangeProcessing: true);
    }

    [HttpGet]
    public async Task<IActionResult> ViewSong(string file)
    {
        if (!await _settings.IsModuleEnabledAsync("Worship")) return NotFound();
        var validated = await ValidatePathAsync(DecodePath(file));
        if (validated == null) return NotFound();
        return PhysicalFile(validated, GetContentType(validated), enableRangeProcessing: true);
    }

    [HttpGet]
    public async Task<IActionResult> Download(string file)
    {
        if (!await _settings.IsModuleEnabledAsync("Worship")) return NotFound();
        var validated = await ValidatePathAsync(DecodePath(file));
        if (validated == null) return NotFound();
        return PhysicalFile(validated, GetContentType(validated), Path.GetFileName(validated));
    }

    private IEnumerable<WorshipSongRow> EnumerateSongs(string root, string search)
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".pdf", ".doc", ".docx", ".txt", ".png", ".jpg", ".jpeg" };
        var files = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories).Where(f => allowed.Contains(Path.GetExtension(f)));
        if (!string.IsNullOrWhiteSpace(search)) files = files.Where(f => Path.GetFileNameWithoutExtension(f).Contains(search, StringComparison.OrdinalIgnoreCase) || Path.GetFileName(f).Contains(search, StringComparison.OrdinalIgnoreCase));
        return files.Select(f =>
        {
            var info = new FileInfo(f);
            return new WorshipSongRow { Title=CleanTitle(Path.GetFileNameWithoutExtension(info.Name)), FileName=info.Name, RelativePath=Path.GetRelativePath(root,f), Extension=info.Extension, FileSizeBytes=info.Length, ModifiedDate=info.LastWriteTime, Token=EncodePath(f) };
        }).OrderBy(x => x.Title).ToList();
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

    private string? ResolveWebPath(string? webPath)
    {
        if (string.IsNullOrWhiteSpace(webPath)) return null;
        var path = webPath.TrimStart('~').TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(_environment.WebRootPath, path);
    }

    private static XColor ParseColor(string? value)
    {
        var s = (value ?? "#174c2f").Trim().TrimStart('#');
        if (s.Length == 6 && int.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out var rgb))
            return XColor.FromArgb(255, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
        return XColor.FromArgb(255, 23, 76, 47);
    }

    private static string CleanTitle(string value) => value.Replace('_', ' ').Replace('-', ' ').Trim();
    private static string EncodePath(string path) => Convert.ToBase64String(Encoding.UTF8.GetBytes(path)).TrimEnd('=').Replace('+','-').Replace('/','_');
    private static string DecodePath(string token)
    {
        try { var s=token.Replace('-','+').Replace('_','/'); s=s.PadRight(s.Length+(4-s.Length%4)%4,'='); return Encoding.UTF8.GetString(Convert.FromBase64String(s)); }
        catch { return ""; }
    }
    private static string GetContentType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".pdf"=>"application/pdf", ".doc"=>"application/msword", ".docx"=>"application/vnd.openxmlformats-officedocument.wordprocessingml.document", ".txt"=>"text/plain", ".png"=>"image/png", ".jpg" or ".jpeg"=>"image/jpeg", _=>"application/octet-stream"
    };
}
