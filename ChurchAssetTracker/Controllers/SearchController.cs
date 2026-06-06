using ChurchAssetTracker.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChurchAssetTracker.Controllers;

[Authorize]
public class SearchController : Controller
{
    private readonly SqlDataService _data;

    public SearchController(SqlDataService data)
    {
        _data = data;
    }

    public async Task<IActionResult> Index(string? query)
    {
        var q = (query ?? "").Trim();

        var model = new GlobalSearchViewModel
        {
            Query = q
        };

        if (string.IsNullOrWhiteSpace(q))
            return View(model);

        var isAdmin = User.IsInRole("Admin");

        var canPeople = isAdmin;
        var canSchool = isAdmin || User.IsInRole("SchoolAdmin") || User.IsInRole("SchoolStaff");
        var canAssets = isAdmin || User.IsInRole("AssetManager");
        var canKeys = isAdmin || User.IsInRole("KeyManager");
        var canReservations = isAdmin || User.IsInRole("ReservationManager") || User.IsInRole("Pastor");
        var canITAssets = isAdmin || User.IsInRole("ITAdmin") || User.IsInRole("ITAssetManager") || User.IsInRole("ITAssetViewer");
        var canITSupport = isAdmin || User.IsInRole("ITAdmin") || User.IsInRole("ITSupportManager") || User.IsInRole("ITSupportTech");
        var canITRequester = User.IsInRole("ITRequester");

        if (canPeople)
        {
            AddGroup(model, "People", "👥", await _data.SearchPeopleAsync(q));
        }

        if (canSchool)
        {
            AddGroup(model, "Students", "🎓", await _data.SearchStudentsAsync(q));
            AddGroup(model, "Faculty / Staff", "👩‍🏫", await _data.SearchFacultyStaffAsync(q));
        }

        if (canITSupport)
        {
            AddGroup(model, "IT Support Tickets", "🎫", await _data.SearchITSupportTicketsAsync(q));
        }

        if (canITAssets)
        {
            AddGroup(model, "IT Assets", "💻", await _data.SearchITAssetsAsync(q));
        }

        if (canAssets)
        {
            AddGroup(model, "Assets", "📦", await _data.SearchAssetsAsync(q));
        }

        if (canReservations)
        {
            AddGroup(model, "Reservations", "📅", await _data.SearchReservationsAsync(q));
        }

        if (canKeys)
        {
            AddGroup(model, "Keys", "🔑", await _data.SearchKeysAsync(q));
            AddGroup(model, "Access Areas", "🚪", await _data.SearchAccessAreasAsync(q));
        }

        if (canITRequester && !canITSupport)
        {
            // Requester-only search can be expanded later to only their own tickets.
            // For now, avoid showing global ticket results to requester-only users.
        }

        return View(model);
    }

    private static void AddGroup(GlobalSearchViewModel model, string name, string icon, List<GlobalSearchResultItem> results)
    {
        if (results.Any())
        {
            model.Groups.Add(new GlobalSearchResultGroup
            {
                GroupName = name,
                Icon = icon,
                Results = results
            });
        }
    }
}