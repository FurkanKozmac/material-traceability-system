namespace backend.Entities;

public class Unit
{
    public long Id { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public string Status { get; set; } = "InStock";
    public long Version { get; set; }
    public DateTime? ConsumedAt { get; set; }

    public long BatchId { get; set; }
    public Batch Batch { get; set; } = null!;

    public long? AddressId { get; set; }
    public Address? Address { get; set; }
}
