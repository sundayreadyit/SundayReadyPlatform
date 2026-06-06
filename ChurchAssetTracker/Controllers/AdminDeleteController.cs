using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace ChurchAssetTracker.Controllers;

[Authorize(Roles = "Admin")]
[Route("AdminDelete/[action]/{id?}")]
public class AdminDeleteController : Controller
{
    private readonly string _connectionString;

    public AdminDeleteController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Missing DefaultConnection in appsettings.json");
    }

    private SqlConnection CreateConnection() => new(_connectionString);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (currentUserId == id.ToString())
        {
            TempData["ErrorMessage"] = "You cannot delete your own account while logged in.";
            return RedirectToAction("Index", "AdminUsers");
        }

        try
        {
            string username = $"User ID {id}";

            await using var conn = CreateConnection();
            await conn.OpenAsync();
            await using var tx = await conn.BeginTransactionAsync();

            await using (var lookup = new SqlCommand("SELECT Username FROM dbo.Users WHERE UserId=@Id;", conn, (SqlTransaction)tx))
            {
                lookup.Parameters.AddWithValue("@Id", id);
                var result = await lookup.ExecuteScalarAsync();
                if (result == null)
                {
                    TempData["ErrorMessage"] = "User was not found.";
                    return RedirectToAction("Index", "AdminUsers");
                }
                username = Convert.ToString(result) ?? username;
            }

            await DeleteIfTableExistsAsync(conn, (SqlTransaction)tx, "dbo.UserPasswordSetupTokens", "UserId", id);
            await DeleteIfTableExistsAsync(conn, (SqlTransaction)tx, "dbo.UserRoles", "UserId", id);

            await using (var cmd = new SqlCommand("DELETE FROM dbo.Users WHERE UserId=@Id;", conn, (SqlTransaction)tx))
            {
                cmd.Parameters.AddWithValue("@Id", id);
                await cmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
            TempData["SuccessMessage"] = $"User '{username}' was permanently deleted.";
        }
        catch (SqlException ex) when (ex.Number == 547)
        {
            TempData["ErrorMessage"] = "This user cannot be deleted because they are referenced by tickets, comments, audit records, or other history. Deactivate the user instead.";
        }

        return RedirectToAction("Index", "AdminUsers");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePerson(int id)
    {
        try
        {
            await DeleteSingleRecordAsync("dbo.People", "PersonId", id);
            TempData["SuccessMessage"] = "Person was permanently deleted.";
        }
        catch (SqlException ex) when (ex.Number == 547)
        {
            TempData["ErrorMessage"] = "This person cannot be deleted because they are referenced by checkouts, keys, tickets, or other history. Mark them inactive instead.";
        }

        return RedirectToAction("Index", "People");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteStudent(int id)
    {
        try
        {
            await DeleteSingleRecordAsync("dbo.Students", "StudentId", id);
            TempData["SuccessMessage"] = "Student was permanently deleted.";
        }
        catch (SqlException ex) when (ex.Number == 547)
        {
            TempData["ErrorMessage"] = "This student cannot be deleted because they are referenced by other records. Mark them inactive instead.";
        }

        return RedirectToAction("Index", "Students");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteFacultyStaff(int id)
    {
        try
        {
            await DeleteSingleRecordAsync("dbo.FacultyStaff", "FacultyStaffId", id);
            TempData["SuccessMessage"] = "Faculty/Staff record was permanently deleted.";
        }
        catch (SqlException ex) when (ex.Number == 547)
        {
            TempData["ErrorMessage"] = "This faculty/staff record cannot be deleted because it is referenced by other records. Mark them inactive instead.";
        }

        return RedirectToAction("Index", "FacultyStaff");
    }

    private async Task DeleteSingleRecordAsync(string tableName, string keyName, int id)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();

        var sql = $"DELETE FROM {tableName} WHERE {keyName} = @Id;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Id", id);
        var rows = await cmd.ExecuteNonQueryAsync();

        if (rows == 0)
            TempData["ErrorMessage"] = "Record was not found.";
    }

    private static async Task DeleteIfTableExistsAsync(SqlConnection conn, SqlTransaction tx, string tableName, string keyName, int id)
    {
        var sql = $@"
            IF OBJECT_ID('{tableName}', 'U') IS NOT NULL
            BEGIN
                DELETE FROM {tableName}
                WHERE {keyName} = @Id;
            END";

        await using var cmd = new SqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("@Id", id);
        await cmd.ExecuteNonQueryAsync();
    }
}