namespace Warehouse.Api.Entities;

public class Reservation
{
    public string ReservationId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsReleased { get; set; } = false;

    public List<ReservationItem> Items { get; set; } = new();
}

public class ReservationItem
{
    public int Id { get; set; }
    public string ReservationId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }

    public Reservation? Reservation { get; set; }
}
