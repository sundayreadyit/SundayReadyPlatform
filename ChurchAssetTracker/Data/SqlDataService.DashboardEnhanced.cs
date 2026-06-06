using Microsoft.Data.SqlClient;

namespace ChurchAssetTracker.Data;

public partial class SqlDataService
{
    public async Task<DashboardEnhancedViewModel> GetEnhancedDashboardAsync(string studentSearch = "", string assetSearch = "")
    {
        var vm = new DashboardEnhancedViewModel();

        try { vm.ReservationSummary = await GetDashboardReservationSummaryAsync(); } catch { vm.ReservationSummary = new DashboardReservationSummary(); }
        try { vm.ITSupportSummary = await GetDashboardITSupportSummaryAsync(); } catch { vm.ITSupportSummary = new DashboardITSupportSummary(); }
        try { vm.UpcomingReservations = await GetDashboardUpcomingReservationsAsync(); } catch { vm.UpcomingReservations = new List<DashboardUpcomingReservation>(); }
        try { vm.OverdueCheckouts = await GetDashboardOverdueCheckoutsAsync(); } catch { vm.OverdueCheckouts = new List<DashboardOverdueCheckout>(); }
        try { vm.RecentAuditItems = await GetDashboardRecentAuditAsync(); } catch { vm.RecentAuditItems = new List<DashboardRecentAudit>(); }

        if (!string.IsNullOrWhiteSpace(studentSearch))
        {
            try { vm.StudentResults = await SearchStudentsQuickAsync(studentSearch); } catch { vm.StudentResults = new List<StudentQuickSearchRow>(); }
        }

        if (!string.IsNullOrWhiteSpace(assetSearch))
        {
            try { vm.ITAssetResults = await SearchITAssetsQuickAsync(assetSearch); } catch { vm.ITAssetResults = new List<ITAssetQuickSearchRow>(); }
        }

        return vm;
    }

    private async Task<DashboardReservationSummary> GetDashboardReservationSummaryAsync()
    {
        var summary = new DashboardReservationSummary();
        await using var conn = CreateConnection();

        const string sql = @"
            SELECT
                SUM(CASE WHEN StartDateTime >= GETDATE() THEN 1 ELSE 0 END),
                SUM(CASE WHEN Status = 'Pending' THEN 1 ELSE 0 END),
                SUM(CASE WHEN Status = 'Approved' THEN 1 ELSE 0 END)
            FROM dbo.Reservations";

        await using var cmd = new SqlCommand(sql, conn);
        await conn.OpenAsync();

        await using var r = await cmd.ExecuteReaderAsync();
        if (await r.ReadAsync())
        {
            summary.UpcomingReservations = r.IsDBNull(0) ? 0 : Convert.ToInt32(r.GetValue(0));
            summary.PendingReservations = r.IsDBNull(1) ? 0 : Convert.ToInt32(r.GetValue(1));
            summary.ApprovedReservations = r.IsDBNull(2) ? 0 : Convert.ToInt32(r.GetValue(2));
        }

        return summary;
    }

    private async Task<DashboardITSupportSummary> GetDashboardITSupportSummaryAsync()
    {
        var summary = new DashboardITSupportSummary();
        await using var conn = CreateConnection();

        const string sql = @"
            SELECT
                SUM(CASE WHEN Status NOT IN ('Resolved','Closed') THEN 1 ELSE 0 END),
                SUM(CASE WHEN Priority = 'Critical' AND Status NOT IN ('Resolved','Closed') THEN 1 ELSE 0 END),
                SUM(CASE WHEN Status = 'Waiting on User' THEN 1 ELSE 0 END)
            FROM dbo.ITSupportTickets";

        await using var cmd = new SqlCommand(sql, conn);
        await conn.OpenAsync();

        await using var r = await cmd.ExecuteReaderAsync();
        if (await r.ReadAsync())
        {
            summary.OpenTickets = r.IsDBNull(0) ? 0 : Convert.ToInt32(r.GetValue(0));
            summary.CriticalTickets = r.IsDBNull(1) ? 0 : Convert.ToInt32(r.GetValue(1));
            summary.WaitingOnUser = r.IsDBNull(2) ? 0 : Convert.ToInt32(r.GetValue(2));
        }

        return summary;
    }

    private async Task<List<DashboardUpcomingReservation>> GetDashboardUpcomingReservationsAsync()
    {
        var list = new List<DashboardUpcomingReservation>();
        await using var conn = CreateConnection();

        const string sql = @"
            SELECT TOP 10
                r.ReservationId,
                r.EventName,
                aa.AreaName,
                r.StartDateTime,
                r.Status
            FROM dbo.Reservations r
            LEFT JOIN dbo.AccessAreas aa ON r.AccessAreaId = aa.AccessAreaId
            WHERE r.StartDateTime >= GETDATE()
            ORDER BY r.StartDateTime";

        await using var cmd = new SqlCommand(sql, conn);
        await conn.OpenAsync();

        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new DashboardUpcomingReservation
            {
                ReservationId = r.GetInt32(0),
                EventName = r.GetString(1),
                AccessAreaName = r.IsDBNull(2) ? null : r.GetString(2),
                StartDateTime = r.GetDateTime(3),
                Status = r.GetString(4)
            });
        }

        return list;
    }

    private async Task<List<DashboardOverdueCheckout>> GetDashboardOverdueCheckoutsAsync()
    {
        var list = new List<DashboardOverdueCheckout>();
        await using var conn = CreateConnection();

        /*
           This version uses AssetCheckouts and common column names from the previous checkout modules.
           If your actual table differs, this section will fail safely and the dashboard will still load.
        */
        const string sql = @"
            SELECT TOP 10
                ac.CheckoutId,
                a.AssetName,
                LTRIM(RTRIM(COALESCE(p.FirstName,'') + ' ' + COALESCE(p.LastName,''))) AS BorrowerName,
                ac.ExpectedReturnDate
            FROM dbo.AssetCheckouts ac
            INNER JOIN dbo.Assets a ON ac.AssetId = a.AssetId
            INNER JOIN dbo.People p ON ac.PersonId = p.PersonId
            WHERE ac.ActualReturnDate IS NULL
              AND ac.ExpectedReturnDate IS NOT NULL
              AND ac.ExpectedReturnDate < GETDATE()
            ORDER BY ac.ExpectedReturnDate";

        await using var cmd = new SqlCommand(sql, conn);
        await conn.OpenAsync();

        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new DashboardOverdueCheckout
            {
                CheckoutId = r.GetInt32(0),
                AssetName = r.GetString(1),
                BorrowerName = r.GetString(2),
                DueDate = r.GetDateTime(3)
            });
        }

        return list;
    }

    private async Task<List<DashboardRecentAudit>> GetDashboardRecentAuditAsync()
    {
        var list = new List<DashboardRecentAudit>();
        await using var conn = CreateConnection();

        const string sql = @"
            SELECT TOP 15
                u.Username,
                a.ActionType,
                a.EntityType,
                a.Description,
                a.CreatedDate
            FROM dbo.AuditLog a
            LEFT JOIN dbo.Users u ON a.UserId = u.UserId
            ORDER BY a.CreatedDate DESC";

        await using var cmd = new SqlCommand(sql, conn);
        await conn.OpenAsync();

        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new DashboardRecentAudit
            {
                Username = r.IsDBNull(0) ? null : r.GetString(0),
                ActionType = r.GetString(1),
                EntityType = r.GetString(2),
                Description = r.IsDBNull(3) ? null : r.GetString(3),
                ActionDate = r.GetDateTime(4)
            });
        }

        return list;
    }

    private async Task<List<StudentQuickSearchRow>> SearchStudentsQuickAsync(string search)
    {
        var list = new List<StudentQuickSearchRow>();
        await using var conn = CreateConnection();

        const string sql = @"
            SELECT TOP 10
                StudentId,
                FirstName + ' ' + LastName,
                GradeLevel,
                Classroom
            FROM dbo.Students
            WHERE IsActive = 1
              AND (
                    FirstName LIKE '%' + @Search + '%'
                    OR LastName LIKE '%' + @Search + '%'
                    OR PreferredName LIKE '%' + @Search + '%'
                  )
            ORDER BY LastName, FirstName";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Search", search);

        await conn.OpenAsync();

        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new StudentQuickSearchRow
            {
                StudentId = r.GetInt32(0),
                FullName = r.GetString(1),
                GradeLevel = r.IsDBNull(2) ? null : r.GetString(2),
                Classroom = r.IsDBNull(3) ? null : r.GetString(3)
            });
        }

        return list;
    }

    private async Task<List<ITAssetQuickSearchRow>> SearchITAssetsQuickAsync(string search)
    {
        var list = new List<ITAssetQuickSearchRow>();
        await using var conn = CreateConnection();

        const string sql = @"
            SELECT TOP 10
                ITAssetId,
                AssetName,
                Make,
                Model,
                IPAddress
            FROM dbo.ITAssets
            WHERE IsActive = 1
              AND (
                    AssetName LIKE '%' + @Search + '%'
                    OR Make LIKE '%' + @Search + '%'
                    OR Model LIKE '%' + @Search + '%'
                    OR IPAddress LIKE '%' + @Search + '%'
                  )
            ORDER BY AssetName";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Search", search);

        await conn.OpenAsync();

        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new ITAssetQuickSearchRow
            {
                ITAssetId = r.GetInt32(0),
                AssetName = r.GetString(1),
                Make = r.IsDBNull(2) ? null : r.GetString(2),
                Model = r.IsDBNull(3) ? null : r.GetString(3),
                IPAddress = r.IsDBNull(4) ? null : r.GetString(4)
            });
        }

        return list;
    }
}
