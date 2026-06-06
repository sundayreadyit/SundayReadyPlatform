using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ChurchAssetTracker.Data;

namespace ChurchAssetTracker.Controllers;

[Authorize(Roles = "Admin,Pastor")]
public class AuditController : Controller
{
    private readonly SqlDataService _data;
    public AuditController(SqlDataService data) => _data = data;

    public async Task<IActionResult> Index()
    {
        return View(await _data.GetAuditLogsAsync());
    }
}
