using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChurchAssetTracker.Controllers
{
    [Authorize(Roles = "Admin")]
    public class SystemInfoController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
