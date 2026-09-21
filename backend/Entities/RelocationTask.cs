namespace backend.Entities;

public class RelocationTask
{
    public long Id { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public long UnitId { get; set; }
    public Unit Unit { get; set; } = null!;

    public long FromAddressId { get; set; }
    public Address FromAddress { get; set; } = null!;

    public long ToAddressId { get; set; }
    public Address ToAddress { get; set; } = null!;

    public long? CompletedByUserId { get; set; }
    public User? CompletedByUser { get; set; }
}
