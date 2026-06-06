using Microsoft.Data.SqlClient;
using System.Globalization;

namespace ChurchAssetTracker.Data;

public partial class SqlDataService
{
    public async Task<ReservationCalendarUpdate1ViewModel> GetReservationCalendarUpdate1Async(int year, int month, string visibility = "All", int? accessAreaId = null)
    {
        var firstDay = new DateTime(year, month, 1);
        var calendarStart = firstDay.AddDays(-(int)firstDay.DayOfWeek);
        var calendarEnd = calendarStart.AddDays(42);

        var reservations = await GetReservationCalendarUpdate1ItemsAsync(calendarStart, calendarEnd, visibility, accessAreaId);
        var areas = await GetReservationCalendarAreaOptionsAsync();

        var model = new ReservationCalendarUpdate1ViewModel
        {
            Year = year,
            Month = month,
            FirstDayOfMonth = firstDay,
            MonthName = firstDay.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
            PreviousMonth = firstDay.AddMonths(-1),
            NextMonth = firstDay.AddMonths(1),
            Visibility = visibility,
            AccessAreaId = accessAreaId,
            AccessAreas = areas
        };

        for (var d = calendarStart; d < calendarEnd; d = d.AddDays(1))
        {
            var day = new ReservationCalendarUpdate1Day
            {
                Date = d,
                IsCurrentMonth = d.Month == month,
                IsToday = d.Date == DateTime.Today
            };

            day.Reservations = reservations
                .Where(r => r.StartDateTime.Date <= d.Date && r.EndDateTime.Date >= d.Date)
                .OrderBy(r => r.StartDateTime)
                .ToList();

            model.Days.Add(day);
        }

        return model;
    }

    public async Task<ReservationDashboardSummary> GetReservationDashboardSummaryAsync()
    {
        var summary = new ReservationDashboardSummary();
        await using var conn = CreateConnection();

        const string sql = @"
            SELECT
                SUM(CASE WHEN Status = 'Approved' AND StartDateTime >= CAST(GETDATE() AS date) THEN 1 ELSE 0 END) AS UpcomingApproved,
                SUM(CASE WHEN Status = 'Pending' THEN 1 ELSE 0 END) AS PendingApproval,
                SUM(CASE WHEN CAST(StartDateTime AS date) = CAST(GETDATE() AS date) AND Status IN ('Pending','Approved') THEN 1 ELSE 0 END) AS TodayReservations,
                SUM(CASE WHEN Status = 'Approved' AND IsPublicEvent = 1 AND StartDateTime >= CAST(GETDATE() AS date) THEN 1 ELSE 0 END) AS PublicUpcoming
            FROM dbo.Reservations;";

        await using var cmd = new SqlCommand(sql, conn);
        await conn.OpenAsync();

        await using (var r = await cmd.ExecuteReaderAsync())
        {
            if (await r.ReadAsync())
            {
                summary.UpcomingApproved = r.IsDBNull(0) ? 0 : r.GetInt32(0);
                summary.PendingApproval = r.IsDBNull(1) ? 0 : r.GetInt32(1);
                summary.TodayReservations = r.IsDBNull(2) ? 0 : r.GetInt32(2);
                summary.PublicUpcoming = r.IsDBNull(3) ? 0 : r.GetInt32(3);
            }
        }

        const string upcomingSql = @"
            SELECT TOP 5 r.ReservationId, r.EventName, aa.AreaName, r.StartDateTime, r.Status, r.IsPublicEvent
            FROM dbo.Reservations r
            LEFT JOIN dbo.AccessAreas aa ON r.AccessAreaId = aa.AccessAreaId
            WHERE r.Status IN ('Pending', 'Approved')
              AND r.StartDateTime >= CAST(GETDATE() AS date)
            ORDER BY r.StartDateTime";

        await using var upcomingCmd = new SqlCommand(upcomingSql, conn);
        await using var ur = await upcomingCmd.ExecuteReaderAsync();

        while (await ur.ReadAsync())
        {
            summary.UpcomingItems.Add(new ReservationMiniRow
            {
                ReservationId = ur.GetInt32(0),
                EventName = ur.GetString(1),
                AccessAreaName = ur.IsDBNull(2) ? null : ur.GetString(2),
                StartDateTime = ur.GetDateTime(3),
                Status = ur.GetString(4),
                IsPublicEvent = ur.GetBoolean(5)
            });
        }

        return summary;
    }

    private async Task<List<ReservationCalendarUpdate1Item>> GetReservationCalendarUpdate1ItemsAsync(DateTime start, DateTime end, string visibility, int? accessAreaId)
    {
        var list = new List<ReservationCalendarUpdate1Item>();
        await using var conn = CreateConnection();

        var visibilityFilter = visibility switch
        {
            "Public" => "AND r.IsPublicEvent = 1",
            "Private" => "AND r.IsPublicEvent = 0",
            _ => ""
        };

        var areaFilter = accessAreaId.HasValue ? "AND r.AccessAreaId = @AccessAreaId" : "";

        var sql = $@"
            SELECT
                r.ReservationId,
                r.EventName,
                aa.AreaName,
                COALESCE(aa.CalendarColor, '#475569') AS CalendarColor,
                r.StartDateTime,
                r.EndDateTime,
                r.Status,
                r.IsPublicEvent
            FROM dbo.Reservations r
            LEFT JOIN dbo.AccessAreas aa ON r.AccessAreaId = aa.AccessAreaId
            WHERE r.StartDateTime < @End
              AND r.EndDateTime >= @Start
              AND r.Status IN ('Pending', 'Approved', 'Denied', 'Cancelled')
              {visibilityFilter}
              {areaFilter}
            ORDER BY r.StartDateTime, r.EventName";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Start", start);
        cmd.Parameters.AddWithValue("@End", end);
        if (accessAreaId.HasValue)
            cmd.Parameters.AddWithValue("@AccessAreaId", accessAreaId.Value);

        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();

        while (await r.ReadAsync())
        {
            list.Add(new ReservationCalendarUpdate1Item
            {
                ReservationId = r.GetInt32(0),
                EventName = r.GetString(1),
                AccessAreaName = r.IsDBNull(2) ? null : r.GetString(2),
                CalendarColor = r.IsDBNull(3) ? "#475569" : r.GetString(3),
                StartDateTime = r.GetDateTime(4),
                EndDateTime = r.GetDateTime(5),
                Status = r.GetString(6),
                IsPublicEvent = r.GetBoolean(7)
            });
        }

        return list;
    }

    private async Task<List<ReservationCalendarAreaOption>> GetReservationCalendarAreaOptionsAsync()
    {
        var list = new List<ReservationCalendarAreaOption>();
        await using var conn = CreateConnection();

        const string sql = @"
            SELECT AccessAreaId, AreaName, COALESCE(CalendarColor, '#475569')
            FROM dbo.AccessAreas
            WHERE IsActive = 1
            ORDER BY AreaName";

        await using var cmd = new SqlCommand(sql, conn);
        await conn.OpenAsync();

        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new ReservationCalendarAreaOption
            {
                AccessAreaId = r.GetInt32(0),
                AreaName = r.GetString(1),
                CalendarColor = r.GetString(2)
            });
        }

        return list;
    }
}