using ChurchAssetTracker.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace ChurchAssetTracker.Controllers;

[Authorize(Roles = "Admin,KeyManager")]
[Route("KeyAssignments")]
public class KeyAssignmentsBulkController : Controller
{
    private readonly string _connectionString;

    public KeyAssignmentsBulkController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Missing DefaultConnection in appsettings.json");
    }

    private SqlConnection CreateConnection() => new(_connectionString);

    [HttpGet("BulkCreate")]
    public async Task<IActionResult> BulkCreate(int? personId = null)
    {
        var model = new KeyAssignmentBulkViewModel
        {
            PersonId = personId ?? 0,
            IssuedDate = DateTime.Today
        };

        await LoadBulkOptionsAsync(model);

        return View("~/Views/KeyAssignments/BulkCreate.cshtml", model);
    }

    [HttpPost("BulkCreate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkCreate(KeyAssignmentBulkViewModel model)
    {
        if (model.PersonId <= 0)
            ModelState.AddModelError(nameof(model.PersonId), "Select a person.");

        if (model.SelectedKeyIds == null || !model.SelectedKeyIds.Any())
            ModelState.AddModelError(nameof(model.SelectedKeyIds), "Select at least one key.");

        if (!ModelState.IsValid)
        {
            await LoadBulkOptionsAsync(model);
            return View("~/Views/KeyAssignments/BulkCreate.cshtml", model);
        }

        var created = 0;
        var skipped = 0;

        await using var conn = CreateConnection();
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        foreach (var keyId in model.SelectedKeyIds.Distinct())
        {
            const string existsSql = @"
                SELECT COUNT(*)
                FROM dbo.KeyAssignments
                WHERE PersonId = @PersonId
                  AND KeyId = @KeyId
                  AND ReturnedDate IS NULL;";

            await using (var existsCmd = new SqlCommand(existsSql, conn, (SqlTransaction)tx))
            {
                existsCmd.Parameters.AddWithValue("@PersonId", model.PersonId);
                existsCmd.Parameters.AddWithValue("@KeyId", keyId);

                var existingCount = Convert.ToInt32(await existsCmd.ExecuteScalarAsync());

                if (existingCount > 0)
                {
                    skipped++;
                    continue;
                }
            }

            const string insertSql = @"
                INSERT INTO dbo.KeyAssignments
                (
                    PersonId,
                    KeyId,
                    IssuedDate,
                    Notes
                )
                VALUES
                (
                    @PersonId,
                    @KeyId,
                    @IssuedDate,
                    @Notes
                );";

            await using (var insertCmd = new SqlCommand(insertSql, conn, (SqlTransaction)tx))
            {
                insertCmd.Parameters.AddWithValue("@PersonId", model.PersonId);
                insertCmd.Parameters.AddWithValue("@KeyId", keyId);
                insertCmd.Parameters.AddWithValue("@IssuedDate", model.IssuedDate);
                insertCmd.Parameters.AddWithValue("@Notes", string.IsNullOrWhiteSpace(model.Notes) ? DBNull.Value : model.Notes.Trim());

                await insertCmd.ExecuteNonQueryAsync();
                created++;
            }
        }

        await AddAuditAsync(
            conn,
            (SqlTransaction)tx,
            "BulkCreate",
            "KeyAssignment",
            null,
            $"Bulk assigned {created} key(s) to PersonId {model.PersonId}. Skipped {skipped} duplicate(s).");

        await tx.CommitAsync();

        TempData["SuccessMessage"] = skipped > 0
            ? $"Assigned {created} key(s). Skipped {skipped} key(s) already assigned to this person."
            : $"Assigned {created} key(s).";

        return RedirectToAction("Index", "KeyAssignments");
    }

    private async Task LoadBulkOptionsAsync(KeyAssignmentBulkViewModel model)
    {
        model.People.Clear();
        model.Keys.Clear();

        await using var conn = CreateConnection();
        await conn.OpenAsync();

        const string peopleSql = @"
            SELECT PersonId,
                   LTRIM(RTRIM(COALESCE(FirstName,'') + ' ' + COALESCE(LastName,''))) AS FullName
            FROM dbo.People
            WHERE IsActive = 1
            ORDER BY LastName, FirstName;";

        await using (var cmd = new SqlCommand(peopleSql, conn))
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            while (await r.ReadAsync())
            {
                model.People.Add(new BulkKeyAssignmentPersonOption
                {
                    PersonId = r.GetInt32(0),
                    FullName = r.GetString(1)
                });
            }
        }

        const string keySql = @"
            SELECT
                k.KeyId,
                k.KeyCode + ' - ' + k.KeyName AS DisplayName,
                CASE
                    WHEN @PersonId > 0
                         AND EXISTS (
                            SELECT 1
                            FROM dbo.KeyAssignments ka
                            WHERE ka.KeyId = k.KeyId
                              AND ka.PersonId = @PersonId
                              AND ka.ReturnedDate IS NULL
                         )
                    THEN 1
                    ELSE 0
                END AS AlreadyAssigned
            FROM dbo.Keys k
            WHERE k.IsActive = 1
            ORDER BY k.KeyName, k.KeyCode;";

        await using (var cmd = new SqlCommand(keySql, conn))
        {
            cmd.Parameters.AddWithValue("@PersonId", model.PersonId);

            await using var r = await cmd.ExecuteReaderAsync();

            while (await r.ReadAsync())
            {
                model.Keys.Add(new BulkKeyAssignmentKeyOption
                {
                    KeyId = r.GetInt32(0),
                    DisplayName = r.GetString(1),
                    IsAlreadyAssignedToSelectedPerson = r.GetInt32(2) == 1
                });
            }
        }
    }

    private static async Task AddAuditAsync(SqlConnection conn, SqlTransaction tx, string actionType, string entityType, int? entityId, string description)
    {
        const string sql = @"
            INSERT INTO dbo.AuditLog
            (
                ActionType,
                EntityType,
                EntityId,
                Description
            )
            VALUES
            (
                @ActionType,
                @EntityType,
                @EntityId,
                @Description
            );";

        await using var cmd = new SqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("@ActionType", actionType);
        cmd.Parameters.AddWithValue("@EntityType", entityType);
        cmd.Parameters.AddWithValue("@EntityId", entityId.HasValue ? entityId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@Description", description);
        await cmd.ExecuteNonQueryAsync();
    }
}