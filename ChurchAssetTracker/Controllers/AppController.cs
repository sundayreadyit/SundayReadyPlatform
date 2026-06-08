using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChurchAssetTracker.Controllers
{
    [Authorize]
    public class AppController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
