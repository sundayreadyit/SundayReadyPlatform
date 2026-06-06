using Microsoft.Data.SqlClient;

namespace ChurchAssetTracker.Data;

public partial class SqlDataService
{
    public List<RecurringReservationOccurrence> BuildRecurringOccurrences(ReservationForm model)
    {
        var occurrences = new List<RecurringReservationOccurrence>();

        if (model.RecurrencePattern == "None" || string.IsNullOrWhiteSpace(model.RecurrencePattern))
        {
            occurrences.Add(new RecurringReservationOccurrence
            {
                StartDateTime = model.StartDateTime,
                EndDateTime = model.EndDateTime
            });

            return occurrences;
        }

        if (!model.RecurrenceEndDate.HasValue)
            return occurrences;

        var start = model.StartDateTime;
        var end = model.EndDateTime;
        var recurrenceEndDate = model.RecurrenceEndDate.Value.Date;

        var safetyCounter = 0;

        while (start.Date <= recurrenceEndDate && safetyCounter < 370)
        {
            occurrences.Add(new RecurringReservationOccurrence
            {
                StartDateTime = start,
                EndDateTime = end
            });

            switch (model.RecurrencePattern)
            {
                case "Weekly":
                    start = start.AddDays(7);
                    end = end.AddDays(7);
                    break;

                case "Biweekly":
                    start = start.AddDays(14);
                    end = end.AddDays(14);
                    break;

                case "Monthly":
                    start = start.AddMonths(1);
                    end = end.AddMonths(1);
                    break;

                default:
                    return occurrences;
            }

            safetyCounter++;
        }

        return occurrences;
    }

    public async Task<List<ReservationConflictRow>> GetRecurringReservationConflictsAsync(ReservationForm model, int reservationIdToExclude = 0)
    {
        var conflicts = new List<ReservationConflictRow>();
        var occurrences = BuildRecurringOccurrences(model);

        foreach (var occurrence in occurrences)
        {
            var occurrenceConflicts = await GetReservationConflictsAsync(
                reservationIdToExclude,
                model.AccessAreaId,
                occurrence.StartDateTime,
                occurrence.EndDateTime);

            conflicts.AddRange(occurrenceConflicts);
        }

        return conflicts
            .GroupBy(c => c.ReservationId)
            .Select(g => g.First())
            .OrderBy(c => c.StartDateTime)
            .ToList();
    }

    public async Task<RecurringReservationCreateResult> CreateReservationOrSeriesAsync(ReservationForm model, string username)
    {
        var result = new RecurringReservationCreateResult();

        var conflicts = await GetRecurringReservationConflictsAsync(model, 0);
        if (conflicts.Any())
        {
            result.Conflicts = conflicts;
            return result;
        }

        var occurrences = BuildRecurringOccurrences(model);
        if (!occurrences.Any())
            return result;

        if (model.RecurrencePattern == "None" || string.IsNullOrWhiteSpace(model.RecurrencePattern))
        {
            await CreateReservationAsync(model, username);
            result.CreatedCount = 1;
            return result;
        }

        var groupId = Guid.NewGuid();

        foreach (var occurrence in occurrences)
        {
            await CreateReservationOccurrenceAsync(model, occurrence, username, groupId);
            result.CreatedCount++;
        }

        await WriteRecurringReservationAuditLogAsync(username, "CreateSeries", "Reservation", 0, $"Created recurring reservation series '{model.EventName}' with {result.CreatedCount} occurrences.");
        return result;
    }

    private async Task<int> CreateReservationOccurrenceAsync(ReservationForm model, RecurringReservationOccurrence occurrence, string username, Guid recurrenceGroupId)
    {
        await using var conn = CreateConnection();

        const string sql = @"
            INSERT INTO dbo.Reservations
            (
                EventName,
                RequestedByPersonId,
                AccessAreaId,
                StartDateTime,
                EndDateTime,
                Status,
                Purpose,
                SetupNotes,
                AccessKeyNeeds,
                ContactName,
                ContactPhone,
                ContactEmail,
                IsPublicEvent,
                Notes,
                CreatedByUserId,
                RecurrenceGroupId,
                IsRecurring,
                RecurrencePattern,
                RecurrenceEndDate
            )
            SELECT
                @EventName,
                @RequestedByPersonId,
                @AccessAreaId,
                @StartDateTime,
                @EndDateTime,
                @Status,
                @Purpose,
                @SetupNotes,
                @AccessKeyNeeds,
                @ContactName,
                @ContactPhone,
                @ContactEmail,
                @IsPublicEvent,
                @Notes,
                u.UserId,
                @RecurrenceGroupId,
                1,
                @RecurrencePattern,
                @RecurrenceEndDate
            FROM (SELECT 1 AS x) seed
            LEFT JOIN dbo.Users u ON u.Username = @Username;

            SELECT CAST(SCOPE_IDENTITY() AS int);";

        await using var cmd = new SqlCommand(sql, conn);
        AddRecurringReservationParameters(cmd, model, occurrence);
        cmd.Parameters.AddWithValue("@Username", username);
        cmd.Parameters.AddWithValue("@RecurrenceGroupId", recurrenceGroupId);
        cmd.Parameters.AddWithValue("@RecurrencePattern", model.RecurrencePattern);
        cmd.Parameters.AddWithValue("@RecurrenceEndDate", model.RecurrenceEndDate.HasValue ? model.RecurrenceEndDate.Value.Date : DBNull.Value);

        await conn.OpenAsync();
        var id = (int)await cmd.ExecuteScalarAsync();

        await WriteRecurringReservationAuditLogAsync(username, "Create", "Reservation", id, $"Created recurring reservation occurrence: {model.EventName}");
        return id;
    }

    private static void AddRecurringReservationParameters(SqlCommand cmd, ReservationForm model, RecurringReservationOccurrence occurrence)
    {
        cmd.Parameters.AddWithValue("@EventName", model.EventName.Trim());
        cmd.Parameters.AddWithValue("@RequestedByPersonId", model.RequestedByPersonId.HasValue ? model.RequestedByPersonId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@AccessAreaId", model.AccessAreaId.HasValue ? model.AccessAreaId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@StartDateTime", occurrence.StartDateTime);
        cmd.Parameters.AddWithValue("@EndDateTime", occurrence.EndDateTime);
        cmd.Parameters.AddWithValue("@Status", string.IsNullOrWhiteSpace(model.Status) ? "Pending" : model.Status);
        cmd.Parameters.AddWithValue("@Purpose", string.IsNullOrWhiteSpace(model.Purpose) ? DBNull.Value : model.Purpose.Trim());
        cmd.Parameters.AddWithValue("@SetupNotes", string.IsNullOrWhiteSpace(model.SetupNotes) ? DBNull.Value : model.SetupNotes.Trim());
        cmd.Parameters.AddWithValue("@AccessKeyNeeds", string.IsNullOrWhiteSpace(model.AccessKeyNeeds) ? DBNull.Value : model.AccessKeyNeeds.Trim());
        cmd.Parameters.AddWithValue("@ContactName", string.IsNullOrWhiteSpace(model.ContactName) ? DBNull.Value : model.ContactName.Trim());
        cmd.Parameters.AddWithValue("@ContactPhone", string.IsNullOrWhiteSpace(model.ContactPhone) ? DBNull.Value : model.ContactPhone.Trim());
        cmd.Parameters.AddWithValue("@ContactEmail", string.IsNullOrWhiteSpace(model.ContactEmail) ? DBNull.Value : model.ContactEmail.Trim());
        cmd.Parameters.AddWithValue("@IsPublicEvent", model.IsPublicEvent);
        cmd.Parameters.AddWithValue("@Notes", string.IsNullOrWhiteSpace(model.Notes) ? DBNull.Value : model.Notes.Trim());
    }

    private async Task WriteRecurringReservationAuditLogAsync(string username, string actionType, string entityType, int entityId, string description)
    {
        await using var conn = CreateConnection();

        const string sql = @"
            INSERT INTO dbo.AuditLog (UserId, ActionType, EntityType, EntityId, Description)
            SELECT TOP 1 UserId, @ActionType, @EntityType, @EntityId, @Description
            FROM dbo.Users
            WHERE Username = @Username
            UNION ALL
            SELECT NULL, @ActionType, @EntityType, @EntityId, @Description
            WHERE NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Username = @Username);";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Username", username);
        cmd.Parameters.AddWithValue("@ActionType", actionType);
        cmd.Parameters.AddWithValue("@EntityType", entityType);
        cmd.Parameters.AddWithValue("@EntityId", entityId);
        cmd.Parameters.AddWithValue("@Description", description);

        await conn.OpenAsync();
        await cmd.ExecuteNonQueryAsync();
    }
}