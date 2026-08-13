using ChurchAssetTracker.Data;
using ChurchAssetTracker.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Authorization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<ModuleEnabledFilter>();
builder.Services.AddScoped<LicenseEnforcementFilter>();
builder.Services.AddControllersWithViews(options =>
{
    var authenticatedUserPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.Filters.Add(new AuthorizeFilter(authenticatedUserPolicy));
    options.Filters.AddService<ModuleEnabledFilter>();
    options.Filters.AddService<LicenseEnforcementFilter>();
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.Name = "ChurchAssetTracker.Auth";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

builder.Services.AddAuthorization();
var dataProtectionKeysPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtectionKeys");
Directory.CreateDirectory(dataProtectionKeysPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
    .SetApplicationName("CWCOperationsPortal");
builder.Services.AddScoped<SqlDataService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<SystemSettingsService>();
builder.Services.AddScoped<WorshipPlanningService>();
builder.Services.AddScoped<IEmailService, SmtpEmailService>();
builder.Services.AddHttpClient("SundayReadyLicensing", client => client.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddScoped<LicenseService>();

var app = builder.Build();

// Ensure cumulative v1.5.3 Worship schema and the WorshipLeader role exist at startup.
using (var startupScope = app.Services.CreateScope())
{
    var worshipPlanning = startupScope.ServiceProvider.GetRequiredService<WorshipPlanningService>();
    await worshipPlanning.EnsureSchemaAsync();
    var licensing = startupScope.ServiceProvider.GetRequiredService<LicenseService>();
    await licensing.EnsureSchemaAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Dashboard/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.Run();
