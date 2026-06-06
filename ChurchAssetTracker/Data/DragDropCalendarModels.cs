namespace ChurchAssetTracker.Data;

public class MoveReservationRequest
{
    public int ReservationId { get; set; }
    public string NewDate { get; set; } = "";
}

public class MoveReservationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
}