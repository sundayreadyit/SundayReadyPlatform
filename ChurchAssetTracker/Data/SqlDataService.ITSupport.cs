using Microsoft.Data.SqlClient;

namespace ChurchAssetTracker.Data;

public partial class SqlDataService
{
    public async Task<ITSupportDashboardSummary> GetITSupportDashboardSummaryAsync()
    {
        var summary = new ITSupportDashboardSummary();
        await using var conn = CreateConnection();

        const string sql = @"
            SELECT
                SUM(CASE WHEN Status NOT IN ('Resolved','Closed') THEN 1 ELSE 0 END) AS OpenTickets,
                SUM(CASE WHEN Priority = 'Critical' AND Status NOT IN ('Resolved','Closed') THEN 1 ELSE 0 END) AS CriticalTickets,
                SUM(CASE WHEN Status = 'Waiting on User' THEN 1 ELSE 0 END) AS WaitingOnUser,
                SUM(CASE WHEN CAST(ResolvedDate AS date) = CAST(GETDATE() AS date) THEN 1 ELSE 0 END) AS ResolvedToday
            FROM dbo.ITSupportTickets;";

        await using var cmd = new SqlCommand(sql, conn);
        await conn.OpenAsync();

        await using var r = await cmd.ExecuteReaderAsync();
        if (await r.ReadAsync())
        {
            summary.OpenTickets = r.IsDBNull(0) ? 0 : r.GetInt32(0);
            summary.CriticalTickets = r.IsDBNull(1) ? 0 : r.GetInt32(1);
            summary.WaitingOnUser = r.IsDBNull(2) ? 0 : r.GetInt32(2);
            summary.ResolvedToday = r.IsDBNull(3) ? 0 : r.GetInt32(3);
        }

        return summary;
    }

    public async Task<List<ITSupportTicketRow>> GetITSupportTicketsAsync(string status = "Open")
    {
        var list = new List<ITSupportTicketRow>();
        await using var conn = CreateConnection();

        var filter = status switch
        {
            "New" => "AND t.Status = 'New'",
            "Assigned" => "AND t.Status = 'Assigned'",
            "In Progress" => "AND t.Status = 'In Progress'",
            "Waiting on User" => "AND t.Status = 'Waiting on User'",
            "Resolved" => "AND t.Status = 'Resolved'",
            "Closed" => "AND t.Status = 'Closed'",
            "All" => "",
            _ => "AND t.Status NOT IN ('Resolved','Closed')"
        };

        var sql = $@"
            SELECT
                t.TicketId,
                t.TicketNumber,
                t.Title,
                t.Description,
                t.Category,
                t.Priority,
                t.Status,
                t.RequestedByPersonId,
                t.RequestedByUserId,
                COALESCE(NULLIF(t.RequestedByName, ''), ru.DisplayName, LTRIM(RTRIM(COALESCE(p.FirstName,'') + ' ' + COALESCE(p.LastName,'')))) AS RequestedByName,
                t.RequestedByEmail,
                t.RequestedByPhone,
                t.AssignedToUserId,
                au.DisplayName AS AssignedToName,
                t.ITAssetId,
                CASE 
                    WHEN ia.ITAssetId IS NULL THEN NULL
                    ELSE LTRIM(RTRIM(
                        COALESCE(NULLIF(ia.AssetName,''), 'IT Asset #' + CAST(ia.ITAssetId AS NVARCHAR(20))) +
                        CASE WHEN NULLIF(ia.Make,'') IS NOT NULL THEN ' - ' + ia.Make ELSE '' END +
                        CASE WHEN NULLIF(ia.Model,'') IS NOT NULL THEN ' ' + ia.Model ELSE '' END +
                        CASE WHEN NULLIF(ia.IPAddress,'') IS NOT NULL THEN ' (' + ia.IPAddress + ')' ELSE '' END
                    ))
                END AS ITAssetName,
                t.AccessAreaId,
                aa.AreaName,
                t.DueDate,
                cu.DisplayName AS CreatedByName,
                t.CreatedDate,
                t.UpdatedDate,
                t.ResolvedDate,
                t.ClosedDate
            FROM dbo.ITSupportTickets t
            LEFT JOIN dbo.People p ON t.RequestedByPersonId = p.PersonId
            LEFT JOIN dbo.Users ru ON t.RequestedByUserId = ru.UserId
            LEFT JOIN dbo.Users au ON t.AssignedToUserId = au.UserId
            LEFT JOIN dbo.Users cu ON t.CreatedByUserId = cu.UserId
            LEFT JOIN dbo.AccessAreas aa ON t.AccessAreaId = aa.AccessAreaId
            LEFT JOIN dbo.ITAssets ia ON t.ITAssetId = ia.ITAssetId
            WHERE 1 = 1
            {filter}
            ORDER BY
                CASE t.Priority
                    WHEN 'Critical' THEN 1
                    WHEN 'High' THEN 2
                    WHEN 'Medium' THEN 3
                    ELSE 4
                END,
                t.CreatedDate DESC";

        await using var cmd = new SqlCommand(sql, conn);
        await conn.OpenAsync();

        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(ReadITSupportTicketRow(r));
        }

        return list;
    }

    public async Task<ITSupportTicketRow?> GetITSupportTicketAsync(int ticketId)
    {
        await using var conn = CreateConnection();

        const string sql = @"
            SELECT
                t.TicketId,
                t.TicketNumber,
                t.Title,
                t.Description,
                t.Category,
                t.Priority,
                t.Status,
                t.RequestedByPersonId,
                t.RequestedByUserId,
                COALESCE(NULLIF(t.RequestedByName, ''), ru.DisplayName, LTRIM(RTRIM(COALESCE(p.FirstName,'') + ' ' + COALESCE(p.LastName,'')))) AS RequestedByName,
                t.RequestedByEmail,
                t.RequestedByPhone,
                t.AssignedToUserId,
                au.DisplayName AS AssignedToName,
                t.ITAssetId,
                CASE 
                    WHEN ia.ITAssetId IS NULL THEN NULL
                    ELSE LTRIM(RTRIM(
                        COALESCE(NULLIF(ia.AssetName,''), 'IT Asset #' + CAST(ia.ITAssetId AS NVARCHAR(20))) +
                        CASE WHEN NULLIF(ia.Make,'') IS NOT NULL THEN ' - ' + ia.Make ELSE '' END +
                        CASE WHEN NULLIF(ia.Model,'') IS NOT NULL THEN ' ' + ia.Model ELSE '' END +
                        CASE WHEN NULLIF(ia.IPAddress,'') IS NOT NULL THEN ' (' + ia.IPAddress + ')' ELSE '' END
                    ))
                END AS ITAssetName,
                t.AccessAreaId,
                aa.AreaName,
                t.DueDate,
                cu.DisplayName AS CreatedByName,
                t.CreatedDate,
                t.UpdatedDate,
                t.ResolvedDate,
                t.ClosedDate
            FROM dbo.ITSupportTickets t
            LEFT JOIN dbo.People p ON t.RequestedByPersonId = p.PersonId
            LEFT JOIN dbo.Users ru ON t.RequestedByUserId = ru.UserId
            LEFT JOIN dbo.Users au ON t.AssignedToUserId = au.UserId
            LEFT JOIN dbo.Users cu ON t.CreatedByUserId = cu.UserId
            LEFT JOIN dbo.AccessAreas aa ON t.AccessAreaId = aa.AccessAreaId
            LEFT JOIN dbo.ITAssets ia ON t.ITAssetId = ia.ITAssetId
            WHERE t.TicketId = @TicketId";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@TicketId", ticketId);

        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();

        if (!await r.ReadAsync()) return null;
        return ReadITSupportTicketRow(r);
    }

    public async Task<ITSupportTicketForm> BuildITSupportTicketFormAsync(ITSupportTicketForm? form = null)
    {
        form ??= new ITSupportTicketForm();
        form.People = await GetITSupportPeopleOptionsAsync();
        form.RequesterUsers = await GetPortalUserOptionsAsync();
        form.Users = await GetITSupportUserOptionsAsync();
        form.ITAssets = await GetITAssetOptionsAsync();
        form.AccessAreas = await GetITSupportAccessAreaOptionsAsync();
        return form;
    }

    public async Task<ITSupportTicketForm?> GetITSupportTicketFormAsync(int ticketId)
    {
        var ticket = await GetITSupportTicketAsync(ticketId);
        if (ticket == null) return null;

        var form = new ITSupportTicketForm
        {
            TicketId = ticket.TicketId,
            Title = ticket.Title,
            Description = ticket.Description,
            Category = ticket.Category,
            Priority = ticket.Priority,
            Status = ticket.Status,
            RequestedByPersonId = ticket.RequestedByPersonId,
            RequestedByUserId = ticket.RequestedByUserId,
            RequestedByName = ticket.RequestedByName,
            RequestedByEmail = ticket.RequestedByEmail,
            RequestedByPhone = ticket.RequestedByPhone,
            AssignedToUserId = ticket.AssignedToUserId,
            ITAssetId = ticket.ITAssetId,
            AccessAreaId = ticket.AccessAreaId,
            DueDate = ticket.DueDate
        };

        return await BuildITSupportTicketFormAsync(form);
    }

    public async Task<int> CreateITSupportTicketAsync(ITSupportTicketForm model, string username)
    {
        await using var conn = CreateConnection();

        const string sql = @"
            INSERT INTO dbo.ITSupportTickets
            (
                Title, Description, Category, Priority, Status,
                RequestedByPersonId, RequestedByUserId, RequestedByName, RequestedByEmail, RequestedByPhone,
                AssignedToUserId, ITAssetId, AccessAreaId, DueDate,
                CreatedByUserId
            )
            SELECT
                @Title, @Description, @Category, @Priority, @Status,
                @RequestedByPersonId, @RequestedByUserId, @RequestedByName, @RequestedByEmail, @RequestedByPhone,
                @AssignedToUserId, @ITAssetId, @AccessAreaId, @DueDate,
                u.UserId
            FROM (SELECT 1 AS x) seed
            LEFT JOIN dbo.Users u ON u.Username = @Username;

            DECLARE @NewId INT = CAST(SCOPE_IDENTITY() AS INT);

            UPDATE dbo.ITSupportTickets
            SET TicketNumber = 'IT-' + RIGHT('00000' + CAST(@NewId AS NVARCHAR(10)), 5)
            WHERE TicketId = @NewId;

            SELECT @NewId;";

        await using var cmd = new SqlCommand(sql, conn);
        AddITSupportTicketParameters(cmd, model);
        cmd.Parameters.AddWithValue("@Username", username);

        await conn.OpenAsync();
        var id = (int)await cmd.ExecuteScalarAsync();

        await WriteITSupportAuditLogAsync(username, "Create", "ITSupportTicket", id, $"Created IT support ticket: {model.Title}");
        return id;
    }

    public async Task UpdateITSupportTicketAsync(ITSupportTicketForm model, string username)
    {
        await using var conn = CreateConnection();

        const string sql = @"
            UPDATE dbo.ITSupportTickets
            SET Title = @Title,
                Description = @Description,
                Category = @Category,
                Priority = @Priority,
                Status = @Status,
                RequestedByPersonId = @RequestedByPersonId,
                RequestedByUserId = @RequestedByUserId,
                RequestedByName = @RequestedByName,
                RequestedByEmail = @RequestedByEmail,
                RequestedByPhone = @RequestedByPhone,
                AssignedToUserId = @AssignedToUserId,
                ITAssetId = @ITAssetId,
                AccessAreaId = @AccessAreaId,
                DueDate = @DueDate,
                UpdatedDate = SYSDATETIME(),
                ResolvedDate = CASE WHEN @Status = 'Resolved' AND ResolvedDate IS NULL THEN SYSDATETIME() ELSE ResolvedDate END,
                ClosedDate = CASE WHEN @Status = 'Closed' AND ClosedDate IS NULL THEN SYSDATETIME() ELSE ClosedDate END
            WHERE TicketId = @TicketId";

        await using var cmd = new SqlCommand(sql, conn);
        AddITSupportTicketParameters(cmd, model);
        cmd.Parameters.AddWithValue("@TicketId", model.TicketId);

        await conn.OpenAsync();
        await cmd.ExecuteNonQueryAsync();

        await WriteITSupportAuditLogAsync(username, "Update", "ITSupportTicket", model.TicketId, $"Updated IT support ticket: {model.Title}");
    }

    public async Task<List<ITSupportTicketCommentRow>> GetITSupportTicketCommentsAsync(int ticketId)
    {
        var list = new List<ITSupportTicketCommentRow>();
        await using var conn = CreateConnection();

        const string sql = @"
            SELECT c.CommentId, c.TicketId, c.CommentText, c.IsInternal, COALESCE(NULLIF(u.DisplayName, ''), u.Username, 'Unknown'), c.CreatedDate
            FROM dbo.ITSupportTicketComments c
            LEFT JOIN dbo.Users u ON c.CreatedByUserId = u.UserId
            WHERE c.TicketId = @TicketId
            ORDER BY c.CreatedDate DESC";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@TicketId", ticketId);

        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();

        while (await r.ReadAsync())
        {
            list.Add(new ITSupportTicketCommentRow
            {
                CommentId = r.GetInt32(0),
                TicketId = r.GetInt32(1),
                CommentText = r.GetString(2),
                IsInternal = r.GetBoolean(3),
                CreatedByName = r.IsDBNull(4) ? null : r.GetString(4),
                CreatedDate = r.GetDateTime(5)
            });
        }

        return list;
    }

    public async Task<int> AddITSupportTicketCommentAsync(ITSupportCommentForm model, int createdByUserId, string username)
    {
        await using var conn = CreateConnection();

        const string sql = @"
            INSERT INTO dbo.ITSupportTicketComments
            (
                TicketId,
                CommentText,
                IsInternal,
                CreatedByUserId
            )
            OUTPUT INSERTED.CommentId
            VALUES
            (
                @TicketId,
                @CommentText,
                @IsInternal,
                NULLIF(@CreatedByUserId, 0)
            );";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@TicketId", model.TicketId);
        cmd.Parameters.AddWithValue("@CommentText", model.CommentText.Trim());
        cmd.Parameters.AddWithValue("@IsInternal", model.IsInternal);
        cmd.Parameters.AddWithValue("@CreatedByUserId", createdByUserId);

        await conn.OpenAsync();
        var result = await cmd.ExecuteScalarAsync();
        var commentId = Convert.ToInt32(result);

        await WriteITSupportAuditLogAsync(username, "Comment", "ITSupportTicket", model.TicketId, "Added comment to IT support ticket");
        return commentId;
    }

    private static ITSupportTicketRow ReadITSupportTicketRow(SqlDataReader r)
    {
        return new ITSupportTicketRow
        {
            TicketId = r.GetInt32(0),
            TicketNumber = r.IsDBNull(1) ? null : r.GetString(1),
            Title = r.GetString(2),
            Description = r.IsDBNull(3) ? null : r.GetString(3),
            Category = r.IsDBNull(4) ? null : r.GetString(4),
            Priority = r.GetString(5),
            Status = r.GetString(6),
            RequestedByPersonId = r.IsDBNull(7) ? null : r.GetInt32(7),
            RequestedByUserId = r.IsDBNull(8) ? null : r.GetInt32(8),
            RequestedByName = r.IsDBNull(9) ? null : r.GetString(9),
            RequestedByEmail = r.IsDBNull(10) ? null : r.GetString(10),
            RequestedByPhone = r.IsDBNull(11) ? null : r.GetString(11),
            AssignedToUserId = r.IsDBNull(12) ? null : r.GetInt32(12),
            AssignedToName = r.IsDBNull(13) ? null : r.GetString(13),
            ITAssetId = r.IsDBNull(14) ? null : r.GetInt32(14),
            ITAssetName = r.IsDBNull(15) ? null : r.GetString(15),
            AccessAreaId = r.IsDBNull(16) ? null : r.GetInt32(16),
            AccessAreaName = r.IsDBNull(17) ? null : r.GetString(17),
            DueDate = r.IsDBNull(18) ? null : r.GetDateTime(18),
            CreatedByName = r.IsDBNull(19) ? null : r.GetString(19),
            CreatedDate = r.GetDateTime(20),
            UpdatedDate = r.IsDBNull(21) ? null : r.GetDateTime(21),
            ResolvedDate = r.IsDBNull(22) ? null : r.GetDateTime(22),
            ClosedDate = r.IsDBNull(23) ? null : r.GetDateTime(23)
        };
    }

    private static void AddITSupportTicketParameters(SqlCommand cmd, ITSupportTicketForm model)
    {
        cmd.Parameters.AddWithValue("@Title", model.Title.Trim());
        cmd.Parameters.AddWithValue("@Description", string.IsNullOrWhiteSpace(model.Description) ? DBNull.Value : model.Description.Trim());
        cmd.Parameters.AddWithValue("@Category", string.IsNullOrWhiteSpace(model.Category) ? DBNull.Value : model.Category.Trim());
        cmd.Parameters.AddWithValue("@Priority", string.IsNullOrWhiteSpace(model.Priority) ? "Medium" : model.Priority);
        cmd.Parameters.AddWithValue("@Status", string.IsNullOrWhiteSpace(model.Status) ? "New" : model.Status);
        cmd.Parameters.AddWithValue("@RequestedByPersonId", model.RequestedByPersonId.HasValue ? model.RequestedByPersonId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@RequestedByUserId", model.RequestedByUserId.HasValue ? model.RequestedByUserId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@RequestedByName", string.IsNullOrWhiteSpace(model.RequestedByName) ? DBNull.Value : model.RequestedByName.Trim());
        cmd.Parameters.AddWithValue("@RequestedByEmail", string.IsNullOrWhiteSpace(model.RequestedByEmail) ? DBNull.Value : model.RequestedByEmail.Trim());
        cmd.Parameters.AddWithValue("@RequestedByPhone", string.IsNullOrWhiteSpace(model.RequestedByPhone) ? DBNull.Value : model.RequestedByPhone.Trim());
        cmd.Parameters.AddWithValue("@AssignedToUserId", model.AssignedToUserId.HasValue ? model.AssignedToUserId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@ITAssetId", model.ITAssetId.HasValue ? model.ITAssetId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@AccessAreaId", model.AccessAreaId.HasValue ? model.AccessAreaId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@DueDate", model.DueDate.HasValue ? model.DueDate.Value : DBNull.Value);
    }

    private async Task<List<PersonOption>> GetITSupportPeopleOptionsAsync()
    {
        var list = new List<PersonOption>();
        await using var conn = CreateConnection();
        const string sql = "SELECT PersonId, LTRIM(RTRIM(FirstName + ' ' + LastName)) FROM dbo.People WHERE IsActive = 1 ORDER BY LastName, FirstName";
        await using var cmd = new SqlCommand(sql, conn);
        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new PersonOption { PersonId = r.GetInt32(0), FullName = r.GetString(1) });
        return list;
    }


    private async Task<List<UserOption>> GetPortalUserOptionsAsync()
    {
        var list = new List<UserOption>();
        await using var conn = CreateConnection();
        const string sql = @"SELECT UserId, DisplayName, Email
                             FROM dbo.Users
                             WHERE IsActive = 1
                             ORDER BY DisplayName";
        await using var cmd = new SqlCommand(sql, conn);
        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new UserOption
            {
                UserId = r.GetInt32(0),
                DisplayName = r.GetString(1),
                Email = r.IsDBNull(2) ? null : r.GetString(2)
            });
        return list;
    }

    public async Task<UserOption?> GetITSupportRequesterUserOptionAsync(int userId)
    {
        await using var conn = CreateConnection();
        const string sql = @"SELECT UserId, DisplayName, Email
                             FROM dbo.Users
                             WHERE UserId = @UserId AND IsActive = 1";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@UserId", userId);
        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;
        return new UserOption
        {
            UserId = r.GetInt32(0),
            DisplayName = r.GetString(1),
            Email = r.IsDBNull(2) ? null : r.GetString(2)
        };
    }

    public async Task ApplyRequesterUserContactAsync(ITSupportTicketForm model)
    {
        if (!model.RequestedByUserId.HasValue) return;

        var user = await GetITSupportRequesterUserOptionAsync(model.RequestedByUserId.Value);
        if (user == null) return;

        model.RequestedByName = user.DisplayName;
        model.RequestedByEmail = user.Email;
        model.RequestedByPhone = user.Phone;
        model.RequestedByPersonId = null;
    }

    private async Task<List<UserOption>> GetITSupportUserOptionsAsync()
    {
        var list = new List<UserOption>();
        await using var conn = CreateConnection();
        const string sql = "SELECT UserId, DisplayName FROM dbo.Users WHERE IsActive = 1 ORDER BY DisplayName";
        await using var cmd = new SqlCommand(sql, conn);
        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new UserOption { UserId = r.GetInt32(0), DisplayName = r.GetString(1) });
        return list;
    }

    private async Task<List<ITAssetOption>> GetITAssetOptionsAsync()
    {
        var list = new List<ITAssetOption>();
        await using var conn = CreateConnection();

        const string sql = @"
            SELECT ITAssetId,
                   LTRIM(RTRIM(
                       COALESCE(NULLIF(AssetName,''), 'IT Asset #' + CAST(ITAssetId AS NVARCHAR(20))) +
                       CASE WHEN NULLIF(Make,'') IS NOT NULL THEN ' - ' + Make ELSE '' END +
                       CASE WHEN NULLIF(Model,'') IS NOT NULL THEN ' ' + Model ELSE '' END +
                       CASE WHEN NULLIF(IPAddress,'') IS NOT NULL THEN ' (' + IPAddress + ')' ELSE '' END
                   )) AS DisplayName
            FROM dbo.ITAssets
            WHERE IsActive = 1
            ORDER BY AssetName, Make, Model";

        await using var cmd = new SqlCommand(sql, conn);
        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new ITAssetOption { ITAssetId = r.GetInt32(0), DisplayName = r.GetString(1) });
        return list;
    }

    private async Task<List<AccessAreaOption>> GetITSupportAccessAreaOptionsAsync()
    {
        var list = new List<AccessAreaOption>();
        await using var conn = CreateConnection();
        const string sql = "SELECT AccessAreaId, AreaName FROM dbo.AccessAreas WHERE IsActive = 1 ORDER BY AreaName";
        await using var cmd = new SqlCommand(sql, conn);
        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new AccessAreaOption { AccessAreaId = r.GetInt32(0), AreaName = r.GetString(1) });
        return list;
    }

    private async Task WriteITSupportAuditLogAsync(string username, string actionType, string entityType, int entityId, string description)
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
