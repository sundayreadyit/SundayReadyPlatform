using Microsoft.Data.SqlClient;
using System.Globalization;

namespace ChurchAssetTracker.Data;

public partial class SqlDataService
{
    public async Task<MoveReservationResult> MoveReservationToDateAsync(int reservationId, DateTime newDate, string username)
    {
        var reservation = await GetReservationAsync(reservationId);
        if (reservation == null)
        {
            return new MoveReservationResult
            {
                Success = false,
                Message = "Reservation was not found."
            };
        }

        if (reservation.Status != "Pending" && reservation.Status != "Approved")
        {
            return new MoveReservationResult
            {
                Success = false,
                Message = "Only pending or approved reservations can be moved from the calendar."
            };
        }

        var originalStart = reservation.StartDateTime;
        var originalEnd = reservation.EndDateTime;
        var newStart = newDate.Date
            .AddHours(originalStart.Hour)
            .AddMinutes(originalStart.Minute)
            .AddSeconds(originalStart.Second);

        var duration = originalEnd - originalStart;
        var newEnd = newStart.Add(duration);

        var conflicts = await GetReservationConflictsAsync(
            reservationId,
            reservation.AccessAreaId,
            newStart,
            newEnd);

        if (conflicts.Any())
        {
            return new MoveReservationResult
            {
                Success = false,
                Message = "Move blocked: another pending or approved reservation already uses this room/area during that time."
            };
        }

        await using var conn = CreateConnection();

        const string sql = @"
            UPDATE dbo.Reservations
            SET StartDateTime = @StartDateTime,
                EndDateTime = @EndDateTime,
                UpdatedDate = SYSDATETIME()
            WHERE ReservationId = @ReservationId";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ReservationId", reservationId);
        cmd.Parameters.AddWithValue("@StartDateTime", newStart);
        cmd.Parameters.AddWithValue("@EndDateTime", newEnd);

        await conn.OpenAsync();
        await cmd.ExecuteNonQueryAsync();

        await WriteDragDropAuditLogAsync(
            username,
            "Move",
            "Reservation",
            reservationId,
            $"Moved reservation '{reservation.EventName}' from {originalStart:g} to {newStart:g}.");

        return new MoveReservationResult
        {
            Success = true,
            Message = "Reservation moved successfully."
        };
    }

    private async Task WriteDragDropAuditLogAsync(string username, string actionType, string entityType, int entityId, string description)
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