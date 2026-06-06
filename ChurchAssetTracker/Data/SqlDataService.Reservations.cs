
using Microsoft.Data.SqlClient;

namespace ChurchAssetTracker.Data;

public partial class SqlDataService
{
    public async Task<List<ReservationRow>> GetReservationsAsync(string status = "Active")
    {
        var list = new List<ReservationRow>();
        await using var conn = CreateConnection();

        var statusFilter = status switch
        {
            "Pending" => "AND r.Status = 'Pending'",
            "Approved" => "AND r.Status = 'Approved'",
            "Denied" => "AND r.Status = 'Denied'",
            "Cancelled" => "AND r.Status = 'Cancelled'",
            "All" => "",
            _ => "AND r.Status IN ('Pending', 'Approved')"
        };

        var sql = $@"SELECT r.ReservationId, r.EventName, r.RequestedByPersonId,
                LTRIM(RTRIM(COALESCE(p.FirstName, '') + ' ' + COALESCE(p.LastName, ''))) AS RequestedByName,
                r.AccessAreaId, aa.AreaName, r.StartDateTime, r.EndDateTime, r.Status, r.Purpose,
                r.SetupNotes, r.AccessKeyNeeds, r.ContactName, r.ContactPhone, r.ContactEmail,
                r.IsPublicEvent, r.Notes, au.DisplayName AS ApprovedByName, r.ApprovedDate, r.CreatedDate
            FROM dbo.Reservations r
            LEFT JOIN dbo.People p ON r.RequestedByPersonId = p.PersonId
            LEFT JOIN dbo.AccessAreas aa ON r.AccessAreaId = aa.AccessAreaId
            LEFT JOIN dbo.Users au ON r.ApprovedByUserId = au.UserId
            WHERE 1 = 1 {statusFilter}
            ORDER BY r.StartDateTime DESC";

        await using var cmd = new SqlCommand(sql, conn);
        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(ReadReservationRow(r));
        return list;
    }

    public async Task<ReservationRow?> GetReservationAsync(int reservationId)
    {
        await using var conn = CreateConnection();
        const string sql = @"SELECT r.ReservationId, r.EventName, r.RequestedByPersonId,
                LTRIM(RTRIM(COALESCE(p.FirstName, '') + ' ' + COALESCE(p.LastName, ''))) AS RequestedByName,
                r.AccessAreaId, aa.AreaName, r.StartDateTime, r.EndDateTime, r.Status, r.Purpose,
                r.SetupNotes, r.AccessKeyNeeds, r.ContactName, r.ContactPhone, r.ContactEmail,
                r.IsPublicEvent, r.Notes, au.DisplayName AS ApprovedByName, r.ApprovedDate, r.CreatedDate
            FROM dbo.Reservations r
            LEFT JOIN dbo.People p ON r.RequestedByPersonId = p.PersonId
            LEFT JOIN dbo.AccessAreas aa ON r.AccessAreaId = aa.AccessAreaId
            LEFT JOIN dbo.Users au ON r.ApprovedByUserId = au.UserId
            WHERE r.ReservationId = @ReservationId";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ReservationId", reservationId);
        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;
        return ReadReservationRow(r);
    }

    public async Task<ReservationForm> BuildReservationFormAsync(ReservationForm? form = null)
    {
        form ??= new ReservationForm();
        form.People = await GetPersonOptionsForReservationsAsync();
        form.AccessAreas = await GetAccessAreaOptionsForReservationsAsync();
        return form;
    }

    public async Task<ReservationForm?> GetReservationFormAsync(int reservationId)
    {
        var row = await GetReservationAsync(reservationId);
        if (row == null) return null;
        return await BuildReservationFormAsync(new ReservationForm
        {
            ReservationId = row.ReservationId,
            EventName = row.EventName,
            RequestedByPersonId = row.RequestedByPersonId,
            AccessAreaId = row.AccessAreaId,
            StartDateTime = row.StartDateTime,
            EndDateTime = row.EndDateTime,
            Status = row.Status,
            Purpose = row.Purpose,
            SetupNotes = row.SetupNotes,
            AccessKeyNeeds = row.AccessKeyNeeds,
            ContactName = row.ContactName,
            ContactPhone = row.ContactPhone,
            ContactEmail = row.ContactEmail,
            IsPublicEvent = row.IsPublicEvent,
            Notes = row.Notes
        });
    }

    public async Task<List<ReservationConflictRow>> GetReservationConflictsAsync(int reservationId, int? accessAreaId, DateTime startDateTime, DateTime endDateTime)
    {
        var list = new List<ReservationConflictRow>();
        if (accessAreaId == null) return list;
        await using var conn = CreateConnection();
        const string sql = @"SELECT r.ReservationId, r.EventName, aa.AreaName, r.StartDateTime, r.EndDateTime, r.Status
            FROM dbo.Reservations r
            LEFT JOIN dbo.AccessAreas aa ON r.AccessAreaId = aa.AccessAreaId
            WHERE r.ReservationId <> @ReservationId
              AND r.AccessAreaId = @AccessAreaId
              AND r.Status IN ('Pending', 'Approved')
              AND r.StartDateTime < @EndDateTime
              AND r.EndDateTime > @StartDateTime
            ORDER BY r.StartDateTime";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ReservationId", reservationId);
        cmd.Parameters.AddWithValue("@AccessAreaId", accessAreaId.Value);
        cmd.Parameters.AddWithValue("@StartDateTime", startDateTime);
        cmd.Parameters.AddWithValue("@EndDateTime", endDateTime);
        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new ReservationConflictRow {
                ReservationId = r.GetInt32(0), EventName = r.GetString(1),
                AccessAreaName = r.IsDBNull(2) ? null : r.GetString(2),
                StartDateTime = r.GetDateTime(3), EndDateTime = r.GetDateTime(4), Status = r.GetString(5)
            });
        }
        return list;
    }

    public async Task<int> CreateReservationAsync(ReservationForm model, string username)
    {
        await using var conn = CreateConnection();
        const string sql = @"INSERT INTO dbo.Reservations
            (EventName, RequestedByPersonId, AccessAreaId, StartDateTime, EndDateTime, Status, Purpose, SetupNotes, AccessKeyNeeds, ContactName, ContactPhone, ContactEmail, IsPublicEvent, Notes, CreatedByUserId)
            SELECT @EventName, @RequestedByPersonId, @AccessAreaId, @StartDateTime, @EndDateTime, @Status, @Purpose, @SetupNotes, @AccessKeyNeeds, @ContactName, @ContactPhone, @ContactEmail, @IsPublicEvent, @Notes, u.UserId
            FROM (SELECT 1 AS x) seed LEFT JOIN dbo.Users u ON u.Username = @Username;
            SELECT CAST(SCOPE_IDENTITY() AS int);";
        await using var cmd = new SqlCommand(sql, conn);
        AddReservationParameters(cmd, model);
        cmd.Parameters.AddWithValue("@Username", username);
        await conn.OpenAsync();
        var id = (int)await cmd.ExecuteScalarAsync();
        await WriteReservationAuditLogAsync(username, "Create", "Reservation", id, $"Created reservation: {model.EventName}");
        return id;
    }

    public async Task UpdateReservationAsync(ReservationForm model, string username)
    {
        await using var conn = CreateConnection();
        const string sql = @"UPDATE dbo.Reservations SET EventName=@EventName, RequestedByPersonId=@RequestedByPersonId,
            AccessAreaId=@AccessAreaId, StartDateTime=@StartDateTime, EndDateTime=@EndDateTime, Status=@Status,
            Purpose=@Purpose, SetupNotes=@SetupNotes, AccessKeyNeeds=@AccessKeyNeeds, ContactName=@ContactName,
            ContactPhone=@ContactPhone, ContactEmail=@ContactEmail, IsPublicEvent=@IsPublicEvent, Notes=@Notes,
            UpdatedDate=SYSDATETIME() WHERE ReservationId=@ReservationId";
        await using var cmd = new SqlCommand(sql, conn);
        AddReservationParameters(cmd, model);
        cmd.Parameters.AddWithValue("@ReservationId", model.ReservationId);
        await conn.OpenAsync();
        await cmd.ExecuteNonQueryAsync();
        await WriteReservationAuditLogAsync(username, "Update", "Reservation", model.ReservationId, $"Updated reservation: {model.EventName}");
    }

    public async Task SetReservationStatusAsync(int reservationId, string status, string username)
    {
        await using var conn = CreateConnection();
        var isApproved = status == "Approved";
        const string sql = @"UPDATE r SET Status=@Status,
            ApprovedByUserId = CASE WHEN @IsApproved = 1 THEN u.UserId ELSE ApprovedByUserId END,
            ApprovedDate = CASE WHEN @IsApproved = 1 THEN SYSDATETIME() ELSE ApprovedDate END,
            UpdatedDate=SYSDATETIME()
            FROM dbo.Reservations r LEFT JOIN dbo.Users u ON u.Username=@Username
            WHERE r.ReservationId=@ReservationId";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ReservationId", reservationId);
        cmd.Parameters.AddWithValue("@Status", status);
        cmd.Parameters.AddWithValue("@IsApproved", isApproved ? 1 : 0);
        cmd.Parameters.AddWithValue("@Username", username);
        await conn.OpenAsync();
        await cmd.ExecuteNonQueryAsync();
        await WriteReservationAuditLogAsync(username, status, "Reservation", reservationId, $"Reservation marked {status}");
    }

    private static ReservationRow ReadReservationRow(SqlDataReader r) => new ReservationRow
    {
        ReservationId = r.GetInt32(0), EventName = r.GetString(1),
        RequestedByPersonId = r.IsDBNull(2) ? null : r.GetInt32(2),
        RequestedByName = r.IsDBNull(3) ? null : r.GetString(3),
        AccessAreaId = r.IsDBNull(4) ? null : r.GetInt32(4),
        AccessAreaName = r.IsDBNull(5) ? null : r.GetString(5),
        StartDateTime = r.GetDateTime(6), EndDateTime = r.GetDateTime(7), Status = r.GetString(8),
        Purpose = r.IsDBNull(9) ? null : r.GetString(9),
        SetupNotes = r.IsDBNull(10) ? null : r.GetString(10),
        AccessKeyNeeds = r.IsDBNull(11) ? null : r.GetString(11),
        ContactName = r.IsDBNull(12) ? null : r.GetString(12),
        ContactPhone = r.IsDBNull(13) ? null : r.GetString(13),
        ContactEmail = r.IsDBNull(14) ? null : r.GetString(14),
        IsPublicEvent = r.GetBoolean(15), Notes = r.IsDBNull(16) ? null : r.GetString(16),
        ApprovedByName = r.IsDBNull(17) ? null : r.GetString(17),
        ApprovedDate = r.IsDBNull(18) ? null : r.GetDateTime(18),
        CreatedDate = r.GetDateTime(19)
    };

    private static void AddReservationParameters(SqlCommand cmd, ReservationForm model)
    {
        cmd.Parameters.AddWithValue("@EventName", model.EventName.Trim());
        cmd.Parameters.AddWithValue("@RequestedByPersonId", model.RequestedByPersonId.HasValue ? model.RequestedByPersonId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@AccessAreaId", model.AccessAreaId.HasValue ? model.AccessAreaId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@StartDateTime", model.StartDateTime);
        cmd.Parameters.AddWithValue("@EndDateTime", model.EndDateTime);
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

    private async Task<List<PersonOption>> GetPersonOptionsForReservationsAsync()
    {
        var list = new List<PersonOption>();
        await using var conn = CreateConnection();
        const string sql = @"SELECT PersonId, LTRIM(RTRIM(FirstName + ' ' + LastName)) FROM dbo.People WHERE IsActive=1 ORDER BY LastName, FirstName";
        await using var cmd = new SqlCommand(sql, conn);
        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(new PersonOption { PersonId = r.GetInt32(0), FullName = r.GetString(1) });
        return list;
    }

    private async Task<List<AccessAreaOption>> GetAccessAreaOptionsForReservationsAsync()
    {
        var list = new List<AccessAreaOption>();
        await using var conn = CreateConnection();
        const string sql = @"SELECT AccessAreaId, AreaName FROM dbo.AccessAreas WHERE IsActive=1 ORDER BY AreaName";
        await using var cmd = new SqlCommand(sql, conn);
        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(new AccessAreaOption { AccessAreaId = r.GetInt32(0), AreaName = r.GetString(1) });
        return list;
    }

    private async Task WriteReservationAuditLogAsync(string username, string actionType, string entityType, int entityId, string description)
    {
        await using var conn = CreateConnection();
        const string sql = @"INSERT INTO dbo.AuditLog (UserId, ActionType, EntityType, EntityId, Description)
            SELECT TOP 1 UserId, @ActionType, @EntityType, @EntityId, @Description FROM dbo.Users WHERE Username=@Username
            UNION ALL SELECT NULL, @ActionType, @EntityType, @EntityId, @Description WHERE NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Username=@Username);";
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
