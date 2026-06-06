using Microsoft.Data.SqlClient;
using System.Globalization;

namespace ChurchAssetTracker.Data;

public partial class SqlDataService
{
    public async Task<ReservationCalendarViewModel> GetReservationCalendarAsync(int year, int month)
    {
        var firstDay = new DateTime(year, month, 1);
        var calendarStart = firstDay.AddDays(-(int)firstDay.DayOfWeek);
        var calendarEnd = calendarStart.AddDays(42);

        var reservations = await GetReservationCalendarItemsAsync(calendarStart, calendarEnd);

        var model = new ReservationCalendarViewModel
        {
            Year = year,
            Month = month,
            FirstDayOfMonth = firstDay,
            MonthName = firstDay.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
            PreviousMonth = firstDay.AddMonths(-1),
            NextMonth = firstDay.AddMonths(1)
        };

        for (var d = calendarStart; d < calendarEnd; d = d.AddDays(1))
        {
            var day = new ReservationCalendarDay
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

    private async Task<List<ReservationCalendarItem>> GetReservationCalendarItemsAsync(DateTime start, DateTime end)
    {
        var list = new List<ReservationCalendarItem>();
        await using var conn = CreateConnection();

        const string sql = @"
            SELECT
                r.ReservationId,
                r.EventName,
                aa.AreaName,
                r.StartDateTime,
                r.EndDateTime,
                r.Status,
                r.IsPublicEvent
            FROM dbo.Reservations r
            LEFT JOIN dbo.AccessAreas aa ON r.AccessAreaId = aa.AccessAreaId
            WHERE r.StartDateTime < @End
              AND r.EndDateTime >= @Start
              AND r.Status IN ('Pending', 'Approved', 'Denied', 'Cancelled')
            ORDER BY r.StartDateTime, r.EventName";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Start", start);
        cmd.Parameters.AddWithValue("@End", end);

        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();

        while (await r.ReadAsync())
        {
            list.Add(new ReservationCalendarItem
            {
                ReservationId = r.GetInt32(0),
                EventName = r.GetString(1),
                AccessAreaName = r.IsDBNull(2) ? null : r.GetString(2),
                StartDateTime = r.GetDateTime(3),
                EndDateTime = r.GetDateTime(4),
                Status = r.GetString(5),
                IsPublicEvent = r.GetBoolean(6)
            });
        }

        return list;
    }
}