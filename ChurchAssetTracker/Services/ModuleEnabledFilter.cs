using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ChurchAssetTracker.Services;

public class ModuleEnabledFilter : IAsyncActionFilter
{
    private readonly SystemSettingsService _settings;

    private static readonly Dictionary<string,string> ControllerModules = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Reservations"] = "Reservations",
        ["Assets"] = "Assets",
        ["Checkouts"] = "Assets",
        ["Keys"] = "Keys",
        ["KeyAssignments"] = "Keys",
        ["KeyAssignmentsBulk"] = "Keys",
        ["KeyAccess"] = "Keys",
        ["AccessAreas"] = "Keys",
        ["Students"] = "School",
        ["FacultyStaff"] = "School",
        ["ITAssets"] = "ITAssets",
        ["ITSupport"] = "ITSupport",
        ["ITSupportAttachments"] = "ITSupport",
        ["PasswordVault"] = "PasswordVault",
        ["ITDocuments"] = "Documentation",
        ["Worship"] = "Worship"
    };

    public ModuleEnabledFilter(SystemSettingsService settings) => _settings = settings;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var controller = context.RouteData.Values["controller"]?.ToString() ?? "";
        if (ControllerModules.TryGetValue(controller, out var module) && !await _settings.IsModuleEnabledAsync(module))
        {
            context.Result = new NotFoundObjectResult("This portal module is disabled by an administrator.");
            return;
        }
        await next();
    }
}
