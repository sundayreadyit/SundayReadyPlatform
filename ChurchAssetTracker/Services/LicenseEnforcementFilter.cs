using ChurchAssetTracker.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ChurchAssetTracker.Services;

/// <summary>
/// Enforces Sunday Ready Platform license access centrally. Safe/read-only HTTP
/// methods continue to work in View-Only mode; data-changing requests are blocked
/// server-side even if a client bypasses the UI.
/// </summary>
public class LicenseEnforcementFilter : IAsyncActionFilter
{
    public const string LicenseStateItemKey = "SundayReady.LicenseState";

    private readonly LicenseService _licenses;
    private readonly ILogger<LicenseEnforcementFilter> _logger;

    public LicenseEnforcementFilter(LicenseService licenses, ILogger<LicenseEnforcementFilter> logger)
    {
        _licenses = licenses;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            await next();
            return;
        }

        var state = await _licenses.GetEnforcementStateAsync();
        context.HttpContext.Items[LicenseStateItemKey] = state;

        if (!state.IsViewOnly || IsSafeMethod(context.HttpContext.Request.Method) || IsExemptController(context.Controller))
        {
            await next();
            return;
        }

        _logger.LogWarning(
            "Blocked write request {Method} {Path} because Sunday Ready Platform is in View-Only mode. License status: {Status}",
            context.HttpContext.Request.Method,
            context.HttpContext.Request.Path,
            state.Status);

        context.HttpContext.Response.Headers["X-SundayReady-License-Mode"] = "ViewOnly";

        if (ExpectsJson(context.HttpContext.Request))
        {
            context.Result = new ObjectResult(new
            {
                error = "LicenseViewOnly",
                message = state.AccessReason ?? "Sunday Ready Platform is currently in View-Only mode.",
                status = state.Status
            })
            {
                StatusCode = StatusCodes.Status423Locked
            };
            return;
        }

        context.Result = new RedirectToActionResult(
            actionName: "Index",
            controllerName: "License",
            routeValues: new { viewOnly = true });
    }

    private static bool IsSafeMethod(string method) =>
        HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method);

    private static bool IsExemptController(object controller)
    {
        var name = controller.GetType().Name;
        return name.Equals("LicenseController", StringComparison.Ordinal)
            || name.Equals("AccountController", StringComparison.Ordinal);
    }

    private static bool ExpectsJson(HttpRequest request)
    {
        if (request.Headers["X-Requested-With"].ToString().Equals("XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
            return true;

        return request.GetTypedHeaders().Accept?.Any(x =>
            x.MediaType.Value?.Contains("json", StringComparison.OrdinalIgnoreCase) == true) == true;
    }
}
