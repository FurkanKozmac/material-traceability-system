namespace backend.Entities;

public class Batch
{
    public long Id { get; set; }
    public string BatchNo { get; set; } = string.Empty;
    public string? Supplier { get; set; }
    public int InitialQuantity { get; set; }
    public DateTime? ExpirationDate { get; set; }

    public long ChemicalId { get; set; }
    public Chemical Chemical { get; set; } = null!;
    public ICollection<Unit> Units { get; set; } = [];
}
